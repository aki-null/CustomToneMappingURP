using System;
using CustomToneMapping.Baker;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    // Built-in LUT cache with two independent four-entry tables:
    // - ReadyEntries owns successfully baked textures.
    // - FailureEntries remembers invalid/unsupported requests.
    // Keeping failures out of the ready table guarantees that a bad configuration
    // cannot evict a reusable LUT. Both tables use bounded linear scans so warm
    // hits stay allocation-free and predictable.
    internal static partial class BuiltInLutCache
    {
        private const int Capacity = 4;

        // This layout is shared by both tables. Failure-only fields remain unused
        // in ReadyEntries; Texture remains null in FailureEntries.
        private struct CacheEntry
        {
            public BuiltInToneMappingKind Kind;
            public uint ConfigHash;
            public Texture2D Texture;
            public Vector3 LutParams;
            public ulong LastUsedStamp;
            public bool IsOccupied;
            public MaterialPreparationStatus FailureStatus;
            public string FailureMessage;
            public bool FailureWasReported;
        }

        private interface IBuiltInLutAdapter<TConfig>
            where TConfig : struct
        {
            bool TryValidate(in TConfig config, out string error);
            bool IsHdrOutput(in TConfig config);
            void Bake(in TConfig config, ref Texture2D texture);
        }

        // Each entry is paired by index with the corresponding typed snapshot
        // array in BuiltInLutCache.BuiltIns.cs. The snapshot makes hash matches
        // collision-safe without boxing the config.
        private static readonly CacheEntry[] ReadyEntries = new CacheEntry[Capacity];
        private static readonly CacheEntry[] FailureEntries = new CacheEntry[Capacity];
        private static ulong _accessStamp;
        private static int _lastReadySlot = -1;
        private static int _lastFailureSlot = -1;

        // Used by the renderer after a successful lookup/bake. Failure lookups
        // never change this to null or hide the most recently used ready LUT.
        internal static Texture2D CachedLutTexture =>
            _lastReadySlot >= 0 && ReadyEntries[_lastReadySlot].IsOccupied
                ? ReadyEntries[_lastReadySlot].Texture
                : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearCache();
            UrpBridge.ResetFailureState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCleanup()
        {
            Application.quitting -= ClearForShutdown;
            Application.quitting += ClearForShutdown;
        }

        private static void ClearForShutdown()
        {
            ClearCache();
            UrpBridge.ResetFailureState();
        }

        internal static void ClearCache()
        {
            for (var i = 0; i < Capacity; i++)
            {
                CoreUtils.Destroy(ReadyEntries[i].Texture);
                ReadyEntries[i] = default;
                FailureEntries[i] = default;
            }

            _accessStamp = 0;
            _lastReadySlot = -1;
            _lastFailureSlot = -1;
            ClearSnapshots();
        }

        private static MaterialPreparationStatus GetOrBakeCore<TConfig, TAdapter>(
            in TConfig config,
            BuiltInToneMappingKind kind,
            TConfig[] readySnapshots,
            TConfig[] failureSnapshots,
            out Texture2D texture,
            out Vector3 lutParams,
            out string error,
            out bool shouldReportFailure)
            where TConfig : struct, ILutConfig, IEquatable<TConfig>
            where TAdapter : struct, IBuiltInLutAdapter<TConfig>
        {
            texture = null;
            lutParams = default;
            error = null;
            shouldReportFailure = false;

            var configHash = config.ConfigHash;

            // Search successful results first. This is the normal per-frame path:
            // a hit updates its LRU stamp and returns before validation, format
            // probing, or baking occurs.
            if (TryFind(in config, kind, configHash, readySnapshots, ReadyEntries, true,
                    out texture, out lutParams, out error, out shouldReportFailure,
                    out var cachedStatus))
                return cachedStatus;

            // Search the separate negative cache second. A matching failure still
            // avoids validation and baking, while preserving report-once behavior.
            // This second lookup is necessary because failures must not consume a
            // slot in the ready-texture table.
            if (TryFind(in config, kind, configHash, failureSnapshots, FailureEntries, false,
                    out texture, out lutParams, out error, out shouldReportFailure,
                    out cachedStatus))
                return cachedStatus;

            // Only an entirely new request reaches this point. Keep the per-mapper
            // validation in its adapter/config; the cache only coordinates the
            // common ordering and storage policy.
            var adapter = default(TAdapter);
            if (!adapter.TryValidate(in config, out error))
            {
                return StoreFailure(kind, configHash, failureSnapshots, in config,
                    MaterialPreparationStatus.Invalid, error,
                    out texture, out lutParams, out shouldReportFailure);
            }

            if (!LutBaker.TryChooseFormat(adapter.IsHdrOutput(in config), out _, out error))
            {
                return StoreFailure(kind, configHash, failureSnapshots, in config,
                    MaterialPreparationStatus.Unsupported, error,
                    out texture, out lutParams, out shouldReportFailure);
            }

            // Validation and format support have succeeded, so evicting the ready
            // LRU is now permitted. Detach the old entry before baking so an
            // unexpected exception cannot leave metadata pointing at a destroyed
            // or partially replaced texture.
            var slot = SelectSlot(ReadyEntries, true);
            var candidate = ReadyEntries[slot].Texture;
            ReadyEntries[slot] = default;
            readySnapshots[slot] = default;

            try
            {
                adapter.Bake(in config, ref candidate);

                candidate.name = "ToneMappingLUT";
                candidate.hideFlags = HideFlags.HideAndDontSave;
                lutParams = GetLutParams(config.LutSize);

                readySnapshots[slot] = config;
                ReadyEntries[slot] = new CacheEntry
                {
                    Kind = kind,
                    ConfigHash = configHash,
                    Texture = candidate,
                    LutParams = lutParams,
                    LastUsedStamp = NextAccessStamp(),
                    IsOccupied = true
                };
                _lastReadySlot = slot;
                texture = candidate;
                return MaterialPreparationStatus.Ready;
            }
            catch (Exception)
            {
                CoreUtils.Destroy(candidate);
                ReadyEntries[slot] = default;
                readySnapshots[slot] = default;
                throw;
            }
        }

        private static bool TryFind<TConfig>(
            in TConfig config,
            BuiltInToneMappingKind kind,
            uint configHash,
            TConfig[] snapshots,
            CacheEntry[] entries,
            bool requireReadyTexture,
            out Texture2D texture,
            out Vector3 lutParams,
            out string error,
            out bool shouldReportFailure,
            out MaterialPreparationStatus status)
            where TConfig : struct, IEquatable<TConfig>
        {
            texture = null;
            lutParams = default;
            error = null;
            shouldReportFailure = false;
            status = default;

            // The same lookup routine serves both tables. For ReadyEntries,
            // requireReadyTexture rejects an empty/destroyed texture. For
            // FailureEntries it is false because failures intentionally have no
            // texture; the selected last-slot index also comes from the matching
            // table.
            var lastSlot = requireReadyTexture ? _lastReadySlot : _lastFailureSlot;
            if (lastSlot >= 0 &&
                Matches(ref entries[lastSlot], lastSlot, kind, configHash, snapshots,
                    requireReadyTexture, in config))
            {
                return UseEntry(ref entries[lastSlot], lastSlot, requireReadyTexture,
                    out texture, out lutParams, out error, out shouldReportFailure, out status);
            }

            for (var i = 0; i < Capacity; i++)
            {
                if (i == lastSlot)
                    continue;

                if (Matches(ref entries[i], i, kind, configHash, snapshots,
                        requireReadyTexture, in config))
                {
                    return UseEntry(ref entries[i], i, requireReadyTexture,
                        out texture, out lutParams, out error, out shouldReportFailure, out status);
                }
            }

            return false;
        }

        private static bool Matches<TConfig>(
            ref CacheEntry entry,
            int slot,
            BuiltInToneMappingKind kind,
            uint configHash,
            TConfig[] snapshots,
            bool requireReadyTexture,
            in TConfig config)
            where TConfig : struct, IEquatable<TConfig>
        {
            // Hash and mapper kind are cheap filters. Exact typed equality is the
            // final check because ConfigHash is only a lookup accelerator.
            if (!entry.IsOccupied ||
                entry.Kind != kind ||
                entry.ConfigHash != configHash)
                return false;

            if (requireReadyTexture && entry.Texture == null)
                return false;

            return snapshots[slot].Equals(config);
        }

        private static bool UseEntry(
            ref CacheEntry entry,
            int slot,
            bool isReady,
            out Texture2D texture,
            out Vector3 lutParams,
            out string error,
            out bool shouldReportFailure,
            out MaterialPreparationStatus status)
        {
            // Access stamps are global across mapper kinds and both tables. LRU
            // ordering is needed only within the selected table, so sharing the
            // counter keeps the bookkeeping small without coupling capacities.
            entry.LastUsedStamp = NextAccessStamp();
            if (isReady)
                _lastReadySlot = slot;
            else
                _lastFailureSlot = slot;
            texture = entry.Texture;
            lutParams = entry.LutParams;
            error = entry.FailureMessage;
            shouldReportFailure = !isReady && !entry.FailureWasReported;
            if (shouldReportFailure)
                entry.FailureWasReported = true;
            status = isReady ? MaterialPreparationStatus.Ready : entry.FailureStatus;
            return true;
        }

        private static int SelectSlot(CacheEntry[] entries, bool requireReadyTexture)
        {
            // Prefer unused slots. A ready slot whose Unity texture was destroyed
            // is also reusable; failure slots have no texture and only use the
            // occupancy bit.
            for (var i = 0; i < Capacity; i++)
            {
                if (!entries[i].IsOccupied ||
                    (requireReadyTexture && entries[i].Texture == null))
                {
                    return i;
                }
            }

            // All slots are occupied: replace the least recently used entry.
            var slot = 0;
            var oldestStamp = entries[0].LastUsedStamp;
            for (var i = 1; i < Capacity; i++)
            {
                if (entries[i].LastUsedStamp < oldestStamp)
                {
                    oldestStamp = entries[i].LastUsedStamp;
                    slot = i;
                }
            }

            return slot;
        }

        private static MaterialPreparationStatus StoreFailure<TConfig>(
            BuiltInToneMappingKind kind,
            uint configHash,
            TConfig[] snapshots,
            in TConfig config,
            MaterialPreparationStatus status,
            string error,
            out Texture2D texture,
            out Vector3 lutParams,
            out bool shouldReportFailure)
            where TConfig : struct
        {
            // Negative results have their own capacity and never destroy or alter
            // a generated ready LUT. Once this entry is evicted, the same bad
            // request may report again; bounded memory is intentional.
            var slot = SelectSlot(FailureEntries, false);
            snapshots[slot] = config;
            FailureEntries[slot] = new CacheEntry
            {
                Kind = kind,
                ConfigHash = configHash,
                LastUsedStamp = NextAccessStamp(),
                IsOccupied = true,
                FailureStatus = status,
                FailureMessage = error,
                FailureWasReported = true
            };
            _lastFailureSlot = slot;
            texture = null;
            lutParams = default;
            shouldReportFailure = true;
            return status;
        }

        private static ulong NextAccessStamp() => ++_accessStamp;

        private static Vector3 GetLutParams(int lutSize)
        {
            var lutWidth = LutBaker.GetLutWidth(lutSize);
            var lutHeight = LutBaker.GetLutHeight(lutSize);
            return new Vector3(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1);
        }

    }
}
