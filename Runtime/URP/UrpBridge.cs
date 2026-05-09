using CustomToneMapping.Baker;
using CustomToneMapping.URP.GT;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    public static class UrpBridge
    {
        private const int MaxCacheEntries = 4;

        private struct CacheEntry
        {
            public uint Key;
            public Texture2D Texture;
            public int LastUsedFrame;
        }

        private static readonly CacheEntry[] Cache = new CacheEntry[MaxCacheEntries];
        private static int _cacheCount;
        private static int _lastUsedSlot;

        private const string TonemapCustomKeyword = "_TONEMAP_CUSTOM";
        private static readonly int CustomTonemapLut = Shader.PropertyToID("_CustomTonemapLut");
        private static readonly int CustomTonemapParams = Shader.PropertyToID("_CustomTonemap_Params");

        // Public getter for cached LUT (used by editor tools)
        public static Texture2D CachedLutTexture =>
            _cacheCount > 0 && _lastUsedSlot < _cacheCount ? Cache[_lastUsedSlot].Texture : null;

        private static Vector3 GetLutParams(int lutSize)
        {
            var lutWidth = LutBaker.GetLutWidth(lutSize);
            var lutHeight = LutBaker.GetLutHeight(lutSize);
            return new Vector3(
                1.0f / lutWidth,
                1.0f / lutHeight,
                lutHeight - 1
            );
        }

        private static bool TryGetOrBakeLut<T>(T config, out Texture2D tex, out Vector3 lutParamsSample) where T : ILutConfig
        {
            tex = null;
            lutParamsSample = GetLutParams(config.LutSize);
            var key = config.ConfigHash;
            var frame = Time.frameCount;

            // Search existing entries for a cache hit
            for (var i = 0; i < _cacheCount; i++)
            {
                if (Cache[i].Key == key && Cache[i].Texture != null)
                {
                    Cache[i].LastUsedFrame = frame;
                    _lastUsedSlot = i;
                    tex = Cache[i].Texture;
                    return true;
                }
            }

            // Cache miss — find a slot to bake into
            int slot;
            if (_cacheCount < MaxCacheEntries)
            {
                // Free slot available
                slot = _cacheCount++;
            }
            else
            {
                // Prefer slots with destroyed textures (Unity may have cleaned them up)
                slot = -1;
                for (var i = 0; i < _cacheCount; i++)
                {
                    if (Cache[i].Texture == null)
                    {
                        slot = i;
                        break;
                    }
                }

                // Otherwise evict LRU
                if (slot < 0)
                {
                    slot = 0;
                    var oldest = Cache[0].LastUsedFrame;
                    for (var i = 1; i < MaxCacheEntries; i++)
                    {
                        if (Cache[i].LastUsedFrame < oldest)
                        {
                            oldest = Cache[i].LastUsedFrame;
                            slot = i;
                        }
                    }
                }
            }

            // Bake into slot. BakeStripLut reuses the texture if dimensions/format
            // match, or calls CoreUtils.Destroy + recreates if they don't.
            var texture = Cache[slot].Texture;
            LutBaker.BakeStripLut(config, ref texture);

            if (texture != null)
            {
                texture.name = "ToneMappingLUT";
                texture.hideFlags = HideFlags.HideAndDontSave;
            }

            Cache[slot] = new CacheEntry
            {
                Key = key,
                Texture = texture,
                LastUsedFrame = frame
            };

            _lastUsedSlot = slot;
            tex = texture;
            return tex != null;
        }

        private static bool TryGetOrBakeLut(GT7ToneMapping vol, HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo,
            int lutSize, out Texture2D tex, out Vector3 lutParamsSample)
        {
            tex = null;
            lutParamsSample = default;

            if (vol == null) return false;

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.targetPeakNits.value;
            if (hdr && vol.detectPeakNits.value)
            {
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;
            }

            var config = vol.ToConfig(targetPeakNits, hdr, lutSize);
            return TryGetOrBakeLut(config, out tex, out lutParamsSample);
        }

        private static bool TryGetOrBakeLut(AgXToneMapping vol, HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo,
            int lutSize, out Texture2D tex, out Vector3 lutParamsSample)
        {
            tex = null;
            lutParamsSample = default;

            if (vol == null) return false;

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.maxNits.value;
            if (hdr && vol.detectBrightnessLimits.value)
            {
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;
            }

            var config = vol.ToConfig(targetPeakNits, hdr, lutSize);
            return TryGetOrBakeLut(config, out tex, out lutParamsSample);
        }

        private static bool TryGetOrBakeLut(GTToneMapping vol, HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo,
            int lutSize, out Texture2D tex, out Vector3 lutParamsSample)
        {
            tex = null;
            lutParamsSample = default;

            if (vol == null) return false;

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.targetPeakNits.value;
            if (hdr && vol.detectPeakNits.value)
            {
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;
            }

            var config = vol.ToConfig(targetPeakNits, hdr, lutSize);
            return TryGetOrBakeLut(config, out tex, out lutParamsSample);
        }

        public static bool PrepareMaterial(Material material, HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo)
        {
            var customMode = VolumeManager.instance.stack.GetComponent<CustomToneMapping>();

            if (customMode.mode.value == ToneMappingMode.None)
            {
                // Do nothing for None mode - let URP handle tone mapping normally
                return false;
            }

            var lutSize = customMode.lutSize.value;
            Texture2D lut;
            switch (customMode.mode.value)
            {
                case ToneMappingMode.GT:
                    {
                        var gt = VolumeManager.instance.stack.GetComponent<GTToneMapping>();
                        if (TryGetOrBakeLut(gt, hdrDisplayInfo, lutSize, out lut, out var sample))
                        {
                            SetupMaterial(material, lut, sample);
                        }

                        break;
                    }
                case ToneMappingMode.GT7:
                    {
                        var gt7 = VolumeManager.instance.stack.GetComponent<GT7ToneMapping>();
                        if (TryGetOrBakeLut(gt7, hdrDisplayInfo, lutSize, out lut, out var sample))
                        {
                            SetupMaterial(material, lut, sample);
                        }

                        break;
                    }
                case ToneMappingMode.AgX:
                    {
                        var agx = VolumeManager.instance.stack.GetComponent<AgXToneMapping>();
                        if (TryGetOrBakeLut(agx, hdrDisplayInfo, lutSize, out lut, out var sample))
                        {
                            SetupMaterial(material, lut, sample);
                        }

                        break;
                    }
                case ToneMappingMode.CustomLUT:
                    {
                        lut = customMode.lutTexture.value as Texture2D;
                        if (lut != null)
                        {
                            SetupMaterial(material, lut, new Vector3(1.0f / lut.width, 1.0f / lut.height, lut.height - 1));
                        }
                        else
                        {
                            return false;
                        }

                        break;
                    }
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(customMode.mode.value), customMode.mode.value,
                        null);
            }

            return true;
        }

        private static void SetupMaterial(Material material, Texture2D lut, Vector3 sample)
        {
            material.EnableKeyword(TonemapCustomKeyword);
            material.SetTexture(CustomTonemapLut, lut);
            material.SetVector(CustomTonemapParams, sample);
        }
    }
}
