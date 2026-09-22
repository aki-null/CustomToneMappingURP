using System;
using CustomToneMapping.Baker;
using CustomToneMapping.Baker.ACES2;
using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    // What the built-in modes bake, kept per config type (GT, GT7, AgX, the ACES 2.0 parameters, the ACES 2.0 LDR
    // strip), up to Capacity configs each, least recently used replaced first.
    //
    // Entries age by frames that tone map, not by time: Tick runs only when a post-processed camera reaches the
    // package. While only cameras without post-processing render (a UI camera over a paused 3D view), nothing ages,
    // so coming back never pays for a rebake.
    internal static class BuiltInLutCache
    {
        internal const int Capacity = 4;
        internal const int StaleLifetimeFrames = 600;

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

        internal static void ClearCache() => Sweep?.Invoke(true);

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

        // GT, GT7 and AgX.
        internal static MaterialPreparationStatus GetOrBake<TConfig>(in TConfig config, out Texture2D lut, out string error)
            where TConfig : struct, IStripLutConfig, IEquatable<TConfig> =>
            Entries<TConfig, StripBaker<TConfig>>.GetOrBake(config, out lut, out _, out error);

        internal static MaterialPreparationStatus GetOrBakeAces2Strip(int lutSize, out Texture2D lut, out string error) =>
            Entries<int, Aces2StripBaker>.GetOrBake(lutSize, out lut, out _, out error);

        internal static MaterialPreparationStatus GetOrBake(in Aces2Config config, out Texture2D atlas, out Vector4[] constants,
            out string error) =>
            Entries<Aces2Config, Aces2Baker>.GetOrBake(config, out atlas, out constants, out error);

        // The most recently used LUT of one mode. For ACES 2.0 that is the LDR strip: its parameter atlas is no LUT.
        internal static Texture2D GetCachedLut(ToneMappingMode mode) => mode switch
        {
            ToneMappingMode.GT => Entries<GTConfig, StripBaker<GTConfig>>.MostRecent,
            ToneMappingMode.GT7 => Entries<GT7Config, StripBaker<GT7Config>>.MostRecent,
            ToneMappingMode.AgX => Entries<AgXConfig, StripBaker<AgXConfig>>.MostRecent,
            ToneMappingMode.ACES2 => Entries<int, Aces2StripBaker>.MostRecent,
            _ => null
        };

        private interface IBaker<TConfig>
        {
            // Ready, or why the config cannot be baked. Only a config the cache does not hold gets here.
            MaterialPreparationStatus Check(in TConfig config, out string error);
            // `texture` and `constants` are null or an evicted entry's, which the baker reuses or replaces. Only ACES 2.0
            // has constants.
            void Bake(in TConfig config, ref Texture2D texture, ref Vector4[] constants);
            // Aging never drops the most recently used entry. Only for bakes too slow to repeat after a mode switch.
            bool KeepsMostRecent { get; }
        }

        private static class Entries<TConfig, TBaker>
            where TConfig : struct, IEquatable<TConfig>
            where TBaker : struct, IBaker<TConfig>
        {
            private struct Entry
            {
                public TConfig Config;
                public Texture2D Texture; // null once destroyed, also by something outside the cache
                public Vector4[] Constants;
                public int LastUsed;
            }

            private static readonly Entry[] Items = new Entry[Capacity];
            // The last config whose bake threw, so it is not baked and logged again every frame. Kept until a clear.
            private static TConfig? _failed;
            private static string _failedError;
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

            internal static MaterialPreparationStatus GetOrBake(in TConfig config, out Texture2D texture,
                out Vector4[] constants, out string error)
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
                        constants = item.Constants;
                        return MaterialPreparationStatus.Ready;
                    }

                    // Replace an empty slot first, then the least recently used.
                    if (Items[slot].Texture != null && (item.Texture == null || item.LastUsed < Items[slot].LastUsed))
                        slot = i;
                }

                texture = null;
                constants = null;
                if (_failed.HasValue && _failed.Value.Equals(config))
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

                // Detach the evicted entry first, so an exception cannot leave the cache pointing at it.
                texture = Items[slot].Texture;
                constants = Items[slot].Constants;
                Items[slot] = default;
                try
                {
                    baker.Bake(in config, ref texture, ref constants);
                }
                catch (Exception e)
                {
                    // The stack trace once, then the caller's once-only failure warning with the message.
                    Debug.LogException(e);
                    CoreUtils.Destroy(texture);
                    texture = null;
                    constants = null;
                    _failed = config;
                    _failedError = $"Bake failed: {e.Message}";
                    error = _failedError;
                    return MaterialPreparationStatus.Invalid;
                }

                Items[slot] = new Entry { Config = config, Texture = texture, Constants = constants, LastUsed = _frame };
                return MaterialPreparationStatus.Ready;
            }

            private static void Purge(bool all)
            {
                if (all)
                {
                    _failed = null;
                    _overflowLogged = false;
                }
                var keep = !all && default(TBaker).KeepsMostRecent ? MostRecentIndex() : -1;
                for (var i = 0; i < Capacity; i++)
                {
                    if (Items[i].Texture == null || i == keep || (!all && _frame - Items[i].LastUsed <= StaleLifetimeFrames))
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
            public void Bake(in TConfig config, ref Texture2D lut, ref Vector4[] _) { config.BakeStripLut(ref lut); Name(lut); }
            public bool KeepsMostRecent => false;
        }

        // Keyed by LUT size alone: LDR grading means SDR output, always Rec.709/D65 at 100 nits.
        private readonly struct Aces2StripBaker : IBaker<int>
        {
            // The strip bake reads the parameter tables on the CPU, so it needs only the LUT format, not float sampling.
            public MaterialPreparationStatus Check(in int lutSize, out string error) =>
                CheckLut(LutLayout.TryValidateSize(lutSize, out error), false, ref error);
            public void Bake(in int lutSize, ref Texture2D lut, ref Vector4[] _) { Aces2LutBaker.BakeStrip(lutSize, ref lut); Name(lut); }
            // The strip runs the full transform per texel on the CPU. Only the current LUT Size is kept, so trying
            // other sizes does not pile up strips: those age out like any other entry.
            public bool KeepsMostRecent => true;
        }

        private readonly struct Aces2Baker : IBaker<Aces2Config>
        {
            public MaterialPreparationStatus Check(in Aces2Config config, out string error)
            {
                if (!config.TryValidate(out error))
                    return MaterialPreparationStatus.Invalid;
                return Aces2LutBaker.IsSupported(out error)
                    ? MaterialPreparationStatus.Ready
                    : MaterialPreparationStatus.Unsupported;
            }

            // Always a fresh atlas: it is not CPU-readable, so it cannot be refilled in place.
            public void Bake(in Aces2Config config, ref Texture2D atlas, ref Vector4[] constants)
            {
                CoreUtils.Destroy(atlas);
                atlas = Aces2LutBaker.Bake(config, out constants);
            }
            public bool KeepsMostRecent => false;
        }
    }
}
