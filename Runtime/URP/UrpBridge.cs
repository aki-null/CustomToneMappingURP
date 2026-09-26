using CustomToneMapping.Baker;
using AgXConfig = CustomToneMapping.Baker.AgX.AgXConfig;
using AgXVolume = CustomToneMapping.URP.AgXToneMapping;
using GT7Config = CustomToneMapping.Baker.GT7.GT7Config;
using GT7Volume = CustomToneMapping.URP.GT7ToneMapping;
using GTConfig = CustomToneMapping.Baker.GT.GTConfig;
using GTVolume = CustomToneMapping.URP.GT.GTToneMapping;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    public enum MaterialPreparationStatus
    {
        Ready,
        Disabled,
        Invalid,
        Unsupported
    }

    public static class UrpBridge
    {
        private const string TonemapCustomKeyword = "_TONEMAP_CUSTOM";
        private static readonly int CustomTonemapLut = Shader.PropertyToID("_CustomTonemapLut");
        private static readonly int CustomTonemapParams = Shader.PropertyToID("_CustomTonemap_Params");

        // The last few distinct failures, each logged once. A failure pushed out by newer ones is logged again.
        private static readonly (ToneMappingMode, MaterialPreparationStatus, string)?[] ReportedFailures =
            new (ToneMappingMode, MaterialPreparationStatus, string)?[4];
        private static int _reportedFailureCursor;

        // The most recently used LUT of one mode. There is no single "last baked" texture: every mode keeps its own.
        public static Texture2D GetCachedLut(ToneMappingMode mode) => BuiltInLutCache.GetCachedLut(mode);

        [System.Obsolete("Use GetCachedLut(mode). This returns the LUT of the active volume stack's mode.")]
        public static Texture2D CachedLutTexture
        {
            get
            {
                var mode = VolumeManager.instance?.stack?.GetComponent<CustomToneMapping>();
                return mode != null ? GetCachedLut(mode.mode.value) : null;
            }
        }

        public static void ClearCache()
        {
            BuiltInLutCache.ClearCache();
            ResetFailureState();
        }

        internal static void ResetFailureState()
        {
            System.Array.Clear(ReportedFailures, 0, ReportedFailures.Length);
            _reportedFailureCursor = 0;
        }

        public static bool TryValidateCustomLut(Texture2D lut, out string error)
        {
            if (lut == null)
                error = "Assign a 2D LUT texture.";
            else if (lut.height < 2 || lut.width != lut.height * lut.height)
                error = "Custom LUT must use a square-strip layout: width = height × height.";
            else
                error = null;
            return error == null;
        }

        // URP customization call sites (ColorGradingLutPass under HDR grading, RenderUberPost under LDR grading) and
        // the Renderer Feature's chain pass.
        public static bool PrepareMaterial(Material material,
            HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo)
        {
            return PrepareMaterialWithStatus(material, hdrDisplayInfo) == MaterialPreparationStatus.Ready;
        }

        public static MaterialPreparationStatus PrepareMaterialWithStatus(Material material,
            HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo)
        {
            if (material == null)
                return MaterialPreparationStatus.Invalid;

            BuiltInLutCache.Tick();
            var stack = VolumeManager.instance?.stack;
            var customMode = stack?.GetComponent<CustomToneMapping>();
            if (customMode == null || customMode.mode.value == ToneMappingMode.None)
            {
                DisableCustomKeyword(material);
                return MaterialPreparationStatus.Disabled;
            }

            var mode = customMode.mode.value;
            var lutSize = customMode.lutSize.value;
            var hdr = hdrDisplayInfo.HasValue;
            // TonemapHdr.hlsl multiplies HDR LUTs by paper white, so they are baked for it. Their peak then lands on the
            // target peak.
            var paperWhite = hdr ? PaperWhiteNits(hdrDisplayInfo.Value) : 0f;
            if (hdr && !(paperWhite > 0f))
            {
                // URP allows 0, which blacks out its own HDR output. The chain pass would divide by it.
                DisableCustomKeyword(material);
                LogFailure(mode, MaterialPreparationStatus.Invalid, "URP's paper white must be above 0 nits (Tonemapping component, or the display's value with Detect Paper White on)");
                return MaterialPreparationStatus.Invalid;
            }
            Texture2D lut = null;
            string error;
            MaterialPreparationStatus status;

            switch (mode)
            {
                case ToneMappingMode.GT:
                {
                    var vol = stack.GetComponent<GTVolume>();
                    status = vol == null
                        ? MissingVolume(mode, out error)
                        : BuiltInLutCache.GetOrBake(vol.ToConfig(
                            PeakNits(vol.targetPeakNits.value, vol.detectPeakNits.value, hdrDisplayInfo), paperWhite, hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.GT7:
                {
                    var vol = stack.GetComponent<GT7Volume>();
                    status = vol == null
                        ? MissingVolume(mode, out error)
                        : BuiltInLutCache.GetOrBake(vol.ToConfig(
                            PeakNits(vol.targetPeakNits.value, vol.detectPeakNits.value, hdrDisplayInfo), paperWhite, hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.AgX:
                {
                    var vol = stack.GetComponent<AgXVolume>();
                    status = vol == null
                        ? MissingVolume(mode, out error)
                        : BuiltInLutCache.GetOrBake(vol.ToConfig(
                            PeakNits(vol.maxNits.value, vol.detectBrightnessLimits.value, hdrDisplayInfo), paperWhite, hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.CustomLUT:
                    lut = customMode.lutTexture.value as Texture2D;
                    status = TryValidateCustomLut(lut, out error)
                        ? MaterialPreparationStatus.Ready
                        : MaterialPreparationStatus.Invalid;
                    break;
                default:
                    status = MaterialPreparationStatus.Invalid;
                    error = $"Unsupported tone mapping mode: {mode}.";
                    break;
            }

            if (status != MaterialPreparationStatus.Ready)
            {
                DisableCustomKeyword(material);
                LogFailure(mode, status, error);
                return status;
            }

            material.SetTexture(CustomTonemapLut, lut);
            // ApplyLut2D's sampling parameters for an N^2 x N strip.
            material.SetVector(CustomTonemapParams, new Vector3(1f / lut.width, 1f / lut.height, lut.height - 1));
            SetKeyword(material, TonemapCustomKeyword, true);
            return MaterialPreparationStatus.Ready;
        }

        // The paper white URP's LutBuilderHdr uses. This assembly cannot see URP's Tonemapping component, because a
        // customized URP references this assembly, so the Renderer Feature assembly registers the resolver (UrpHdrOutput).
        // Without it, the display's value is used, as URP does with Detect Paper White on.
        internal static System.Func<HDROutputUtils.HDRDisplayInformation, float> PaperWhiteResolver;

        internal static float PaperWhiteNits(HDROutputUtils.HDRDisplayInformation info) =>
            PaperWhiteResolver?.Invoke(info) ?? info.paperWhiteNits;

        // A display that reports no peak keeps the manual value.
        private static float PeakNits(float target, bool detect, HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo) =>
            hdrDisplayInfo.HasValue && detect && hdrDisplayInfo.Value.maxToneMapLuminance > 0
                ? hdrDisplayInfo.Value.maxToneMapLuminance
                : target;

        private static MaterialPreparationStatus MissingVolume(ToneMappingMode mode, out string error)
        {
            error = $"{mode} volume component is missing.";
            return MaterialPreparationStatus.Invalid;
        }

        internal static void LogFailure(ToneMappingMode mode, MaterialPreparationStatus status, string error)
        {
            var failure = (mode, status, error);
            foreach (var reported in ReportedFailures)
                if (reported == failure)
                    return;

            ReportedFailures[_reportedFailureCursor] = failure;
            _reportedFailureCursor = (_reportedFailureCursor + 1) % ReportedFailures.Length;
            Debug.LogWarning($"Custom tone mapping disabled for {mode}: {error ?? status.ToString()}.");
        }

        private static void DisableCustomKeyword(Material material) => SetKeyword(material, TonemapCustomKeyword, false);

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled)
                return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
