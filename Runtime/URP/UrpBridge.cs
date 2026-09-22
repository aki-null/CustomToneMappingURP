using CustomToneMapping.Baker;
using CustomToneMapping.Baker.ACES2;
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
        private const string TonemapAcesKeyword = "_TONEMAP_ACES";
        // Declared by the optional URP customization line in LutBuilderHdr,
        // `#pragma multi_compile_local_fragment _ _CUSTOM_TONEMAP_ACES2` (README, "Upgrading the URP customization").
        private const string Aces2GradingKeyword = "_CUSTOM_TONEMAP_ACES2";
        // Declared by UberPost only. URP hands that material to the bridge under LDR grading (per-pixel tone map).
        private const string UberSignatureKeyword = "_HDR_GRADING";
        private static readonly int CustomTonemapLut = Shader.PropertyToID("_CustomTonemapLut");
        private static readonly int CustomTonemapParams = Shader.PropertyToID("_CustomTonemap_Params");
        private static readonly int CustomTonemapMode = Shader.PropertyToID("_CustomTonemapMode");
        internal const string UrpCustomizationUpgradeUrl = "https://github.com/aki-null/CustomToneMappingURP#upgrading-the-urp-customization";
        internal const string Aces2StandardGradingNotice =
            "ACES 2.0 is grading in URP's standard spaces: this URP customization does not declare _CUSTOM_TONEMAP_ACES2. " +
            "Add the one line from the upgrade steps, or turn off 'ACES-aware grading' on the ACES 2.0 Tone Mapping component.";
        private const string Aces2StandardGradingLog = Aces2StandardGradingNotice + " " + UrpCustomizationUpgradeUrl;
        internal const string Aces2LdrGradingNotice =
            "ACES 2.0 is grading in URP's standard spaces: ACES-aware grading needs HDR Color Grading in the URP Asset. " +
            "Switch to HDR Color Grading, or turn off 'ACES-aware grading' on the ACES 2.0 Tone Mapping component.";

        // The last few distinct failures, each logged once. A failure pushed out by newer ones is logged again.
        private static readonly (ToneMappingMode, MaterialPreparationStatus, string)?[] ReportedFailures =
            new (ToneMappingMode, MaterialPreparationStatus, string)?[4];
        private static int _reportedFailureCursor;
        // The ACES 2.0 notices are not failures, so they stay out of the failure ring and can never be evicted and logged again.
        private static bool _aces2LdrNoticeLogged, _aces2LayoutNoticeLogged;

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

        // The customization layout comes from the compiled shader, so C# never has to guess it.
        internal static bool DeclaresAces2Grading(Shader shader) =>
            shader != null && shader.keywordSpace.FindKeyword(Aces2GradingKeyword).isValid;

        public static void ClearCache()
        {
            BuiltInLutCache.ClearCache();
            ResetFailureState();
        }

        internal static void ResetFailureState()
        {
            System.Array.Clear(ReportedFailures, 0, ReportedFailures.Length);
            _reportedFailureCursor = 0;
            _aces2LdrNoticeLogged = _aces2LayoutNoticeLogged = false;
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
            Texture2D lut = null;
            Vector4[] aces2Constants = null; // set only when ACES 2.0 runs per texel from its atlas (`lut`)
            Aces2ToneMapping aces2 = null;
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
                            PeakNits(vol.targetPeakNits.value, vol.detectPeakNits.value, hdrDisplayInfo), hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.GT7:
                {
                    var vol = stack.GetComponent<GT7Volume>();
                    status = vol == null
                        ? MissingVolume(mode, out error)
                        : BuiltInLutCache.GetOrBake(vol.ToConfig(
                            PeakNits(vol.targetPeakNits.value, vol.detectPeakNits.value, hdrDisplayInfo), hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.AgX:
                {
                    var vol = stack.GetComponent<AgXVolume>();
                    status = vol == null
                        ? MissingVolume(mode, out error)
                        : BuiltInLutCache.GetOrBake(vol.ToConfig(
                            PeakNits(vol.maxNits.value, vol.detectBrightnessLimits.value, hdrDisplayInfo), hdr, lutSize),
                            out lut, out error);
                    break;
                }
                case ToneMappingMode.CustomLUT:
                    lut = customMode.lutTexture.value as Texture2D;
                    status = TryValidateCustomLut(lut, out error)
                        ? MaterialPreparationStatus.Ready
                        : MaterialPreparationStatus.Invalid;
                    break;
                case ToneMappingMode.ACES2:
                    aces2 = stack.GetComponent<Aces2ToneMapping>();
                    if (aces2 == null)
                        status = MissingVolume(mode, out error);
                    // LDR grading: URP tone-maps per pixel in UberPost, so ACES 2.0 is sampled from a LogC strip
                    // exactly like the RGB LUT modes, cached and baked the same way.
                    else if (material.shader.keywordSpace.FindKeyword(UberSignatureKeyword).isValid)
                    {
                        if (aces2.acesAwareGrading.value)
                            LogAces2Notice(ref _aces2LdrNoticeLogged, Aces2LdrGradingNotice);
                        status = BuiltInLutCache.GetOrBakeAces2Strip(lutSize, out lut, out error);
                    }
                    else
                        status = BuiltInLutCache.GetOrBake(new Aces2Config(
                            PeakNits(aces2.targetPeakNits.value, aces2.detectPeakNits.value, hdrDisplayInfo), hdr),
                            out lut, out aces2Constants, out error);
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

            if (aces2Constants != null)
            {
                BindAces2(material, lut, aces2Constants, aces2.acesAwareGrading.value);
                return MaterialPreparationStatus.Ready;
            }

            material.SetInteger(CustomTonemapMode, 0);
            material.SetTexture(CustomTonemapLut, lut);
            // ApplyLut2D's sampling parameters for an N^2 x N strip.
            material.SetVector(CustomTonemapParams, new Vector3(1f / lut.width, 1f / lut.height, lut.height - 1));
            SetKeyword(material, TonemapAcesKeyword, false);
            SetKeyword(material, Aces2GradingKeyword, false);
            SetKeyword(material, TonemapCustomKeyword, true);
            return MaterialPreparationStatus.Ready;
        }

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

        // ACES 2.0 evaluated per LUT texel from the atlas: URP's LutBuilderHdr or the chain pass.
        private static void BindAces2(Material material, Texture2D atlas, Vector4[] constants, bool wantsAcesGrading)
        {
            Aces2LutBaker.Bind(material, atlas, constants);
            material.SetInteger(CustomTonemapMode, 1); // TonemapParams.hlsl: ACES 2.0 instead of the RGB LUT

            // With the keyword, URP's ACES grading branch runs and its two ACES output calls resolve to ACES 2.0
            // through TonemapParams.hlsl. A LutBuilderHdr without it declares only _TONEMAP_ACES, so it gets the
            // notice. The chain shader declares neither and always grades in standard spaces.
            var acesGrading = wantsAcesGrading && DeclaresAces2Grading(material.shader);
            if (wantsAcesGrading && !acesGrading && material.shader.keywordSpace.FindKeyword(TonemapAcesKeyword).isValid)
                LogAces2Notice(ref _aces2LayoutNoticeLogged, Aces2StandardGradingLog);

            SetKeyword(material, TonemapCustomKeyword, !acesGrading);
            SetKeyword(material, TonemapAcesKeyword, acesGrading);
            SetKeyword(material, Aces2GradingKeyword, acesGrading);
        }

        // Once per session: why ACES-aware grading is not honoured.
        private static void LogAces2Notice(ref bool logged, string notice)
        {
            if (logged)
                return;
            logged = true;
            Debug.LogWarning($"Custom tone mapping (ACES2): {notice}");
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

        private static void DisableCustomKeyword(Material material)
        {
            SetKeyword(material, TonemapCustomKeyword, false);
            SetKeyword(material, TonemapAcesKeyword, false);
            SetKeyword(material, Aces2GradingKeyword, false);
        }

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
