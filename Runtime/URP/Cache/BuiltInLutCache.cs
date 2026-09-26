using System;
using CustomToneMapping.Baker;
using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    // What the built-in modes bake, kept per config type (GT, GT7, AgX), up to Capacity configs each, least recently
    // used replaced first.
    //
    // Entries age by frames that tone map, not by time: Tick runs only when a post-processed camera reaches the
    // package. While only cameras without post-processing render (a UI camera over a paused 3D view), nothing ages,
    // so coming back never pays for a rebake.
    internal static class BuiltInLutCache
    {
        internal const int Capacity = 4;
        internal const int StaleLifetimeFrames = 600;
        // A config whose bake threw is retried after this many tone-mapped frames. The window doubles while it keeps
        // failing, up to MaxFailureRetryFrames, so a persistent failure of a slow bake does not hitch on a fixed cadence.
        internal const int FailureRetryFrames = StaleLifetimeFrames;
        internal const int MaxFailureRetryFrames = FailureRetryFrames * 16;

        // Tests only: while set, every bake throws it, to drive the failure path. ClearCache resets it.
        internal static Exception InjectedBakeFailure;

        private static event Action<bool> Sweep; // true clears, false purges stale entries
        private static int _lastFrameCount = -1;
        private static int _frame;

        static BuiltInLutCache()
        {
            // Register once per domain, including edit mode and play mode without domain reload. Application.quitting
            // also fires on leaving play mode, when this domain can remain alive. Entering play mode is covered by the
            // SubsystemRegistration attribute below.
            Application.quitting += ClearForLifecycleChange;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearForLifecycleChange;
            UnityEditor.EditorApplication.quitting += ClearForLifecycleChange;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearForLifecycleChange()
        {
            ClearCache();
            UrpBridge.ResetFailureState();
        }

        internal static void ClearCache()
        {
            InjectedBakeFailure = null;
            Sweep?.Invoke(true);
        }

        // Counts a frame that tone maps, once however many cameras do, and drops entries unused for too long.
        internal static void Tick() => Tick(Time.frameCount);

        internal static void Tick(int frameCount)
        {
            if (frameCount == _lastFrameCount)
                return;
            _lastFrameCount = frameCount;
            _frame++;
            Sweep?.Invoke(false);
        }

        internal static MaterialPreparationStatus GetOrBake<TConfig>(in TConfig config, out Texture2D lut, out string error)
            where TConfig : struct, IStripLutConfig, IEquatable<TConfig> =>
            Entries<TConfig, StripBaker<TConfig>>.GetOrBake(config, out lut, out error);

        // The most recently used LUT of one mode.
        internal static Texture2D GetCachedLut(ToneMappingMode mode) => mode switch
        {
            ToneMappingMode.GT => Entries<GTConfig, StripBaker<GTConfig>>.MostRecent,
            ToneMappingMode.GT7 => Entries<GT7Config, StripBaker<GT7Config>>.MostRecent,
            ToneMappingMode.AgX => Entries<AgXConfig, StripBaker<AgXConfig>>.MostRecent,
            _ => null
        };

        private interface IBaker<TConfig>
        {
            // Ready, or why the config cannot be baked. Only a config the cache does not hold gets here.
            MaterialPreparationStatus Check(in TConfig config, out string error);
            // `texture` is null or an evicted entry's, which the baker reuses or replaces.
            void Bake(in TConfig config, ref Texture2D texture);
        }

        private static class Entries<TConfig, TBaker>
            where TConfig : struct, IEquatable<TConfig>
            where TBaker : struct, IBaker<TConfig>
        {
            private struct Entry
            {
                public TConfig Config;
                public Texture2D Texture; // null once destroyed, also by something outside the cache
                public int LastUsed;
            }

            private static readonly Entry[] Items = new Entry[Capacity];
            // The last config whose bake threw, when, and its current retry window, so a transient failure (memory
            // pressure, a device hiccup) does not switch tone mapping off until the next clear.
            private static TConfig? _failed;
            private static string _failedError;
            private static int _failedFrame, _failedWindow;
            private static bool _overflowLogged;

            static Entries() => Sweep += Purge;

            private static int MostRecentIndex()
            {
                var best = -1;
                for (var i = 0; i < Capacity; i++)
                    if (Items[i].Texture != null && (best < 0 || Items[i].LastUsed > Items[best].LastUsed))
                        best = i;
                return best;
            }

            internal static Texture2D MostRecent
            {
                get
                {
                    var best = MostRecentIndex();
                    return best < 0 ? null : Items[best].Texture;
                }
            }

            internal static MaterialPreparationStatus GetOrBake(in TConfig config, out Texture2D texture, out string error)
            {
                error = null;
                var slot = 0;
                for (var i = 0; i < Capacity; i++)
                {
                    ref var item = ref Items[i];
                    if (item.Texture != null && item.Config.Equals(config))
                    {
                        item.LastUsed = _frame;
                        texture = item.Texture;
                        return MaterialPreparationStatus.Ready;
                    }

                    // Replace an empty slot first, then the least recently used.
                    if (Items[slot].Texture != null && (item.Texture == null || item.LastUsed < Items[slot].LastUsed))
                        slot = i;
                }

                texture = null;
                var retrying = _failed.HasValue && _failed.Value.Equals(config);
                if (retrying && _frame - _failedFrame < _failedWindow)
                {
                    error = _failedError;
                    return MaterialPreparationStatus.Invalid;
                }

                var baker = default(TBaker);
                var status = baker.Check(in config, out error);
                if (status != MaterialPreparationStatus.Ready)
                    return status;

                // Every slot was used this frame: more configs than Capacity are live, so one rebakes every frame.
                if (Items[slot].Texture != null && Items[slot].LastUsed == _frame && !_overflowLogged)
                {
                    _overflowLogged = true;
                    Debug.LogWarning($"Custom tone mapping: more than {Capacity} configurations of one mode are in use in the same frame, so LUTs are rebaked every frame. Use fewer distinct settings across cameras.");
                }

                // Detach the evicted entry first, so an exception cannot leave the cache pointing at it; the baker
                // reuses its texture. A retry of a config that already failed bakes into a fresh texture instead and
                // evicts only once it succeeds, so a persistent failure never destroys a working LUT.
                if (!retrying)
                {
                    texture = Items[slot].Texture;
                    Items[slot] = default;
                }
                try
                {
                    if (InjectedBakeFailure != null)
                        throw InjectedBakeFailure;
                    baker.Bake(in config, ref texture);
                }
                catch (Exception e)
                {
                    // The stack trace once per distinct failure, then the caller's once-only warning with the
                    // message. The same failure again only lengthens the window.
                    var message = $"Bake failed: {e.Message}";
                    if (!retrying || message != _failedError)
                        Debug.LogException(e);
                    CoreUtils.Destroy(texture);
                    texture = null;
                    _failedWindow = retrying ? Math.Min(_failedWindow * 2, MaxFailureRetryFrames) : FailureRetryFrames;
                    _failed = config;
                    _failedFrame = _frame;
                    _failedError = message;
                    error = message;
                    return MaterialPreparationStatus.Invalid;
                }

                if (retrying)
                {
                    CoreUtils.Destroy(Items[slot].Texture);
                    _failed = null;
                }
                Items[slot] = new Entry { Config = config, Texture = texture, LastUsed = _frame };
                return MaterialPreparationStatus.Ready;
            }

            private static void Purge(bool all)
            {
                if (all)
                {
                    _failed = null;
                    _overflowLogged = false;
                }
                for (var i = 0; i < Capacity; i++)
                {
                    if (Items[i].Texture == null || (!all && _frame - Items[i].LastUsed <= StaleLifetimeFrames))
                        continue;
                    CoreUtils.Destroy(Items[i].Texture);
                    Items[i] = default;
                }
            }
        }

        private static MaterialPreparationStatus CheckLut(bool valid, bool hdr, ref string error)
        {
            if (!valid)
                return MaterialPreparationStatus.Invalid;
            return LutBaker.TryChooseFormat(hdr, out _, out error)
                ? MaterialPreparationStatus.Ready
                : MaterialPreparationStatus.Unsupported;
        }

        private static void Name(Texture2D lut)
        {
            lut.name = "ToneMappingLUT";
            lut.hideFlags = HideFlags.HideAndDontSave;
        }

        private readonly struct StripBaker<TConfig> : IBaker<TConfig> where TConfig : struct, IStripLutConfig
        {
            public MaterialPreparationStatus Check(in TConfig config, out string error) =>
                CheckLut(config.TryValidate(out error), config.HdrOutput, ref error);
            public void Bake(in TConfig config, ref Texture2D lut) { config.BakeStripLut(ref lut); Name(lut); }
        }
    }
}
