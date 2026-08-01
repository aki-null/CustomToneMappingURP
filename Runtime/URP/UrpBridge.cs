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

        private struct IntegrationFailure
        {
            public ToneMappingMode Mode;
            public MaterialPreparationStatus Status;
            public string Error;
            public bool HasFailure;
            public bool HasBeenReported;
        }

        private static readonly IntegrationFailure[] IntegrationFailures = new IntegrationFailure[4];
        private static int _integrationFailureCursor;

        public static Texture2D CachedLutTexture => BuiltInLutCache.CachedLutTexture;

        public static void ClearCache()
        {
            BuiltInLutCache.ClearCache();
            ResetFailureState();
        }

        internal static void ResetFailureState()
        {
            for (var i = 0; i < IntegrationFailures.Length; i++)
                IntegrationFailures[i] = default;
            _integrationFailureCursor = 0;
            CustomLutCache.ClearCache();
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(GT7Config config,
            out Texture2D tex, out Vector3 lutParamsSample, out string error,
            out bool shouldReportFailure)
        {
            return BuiltInLutCache.GetOrBake(config, out tex, out lutParamsSample, out error,
                out shouldReportFailure);
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(AgXConfig config,
            out Texture2D tex, out Vector3 lutParamsSample, out string error,
            out bool shouldReportFailure)
        {
            return BuiltInLutCache.GetOrBake(config, out tex, out lutParamsSample, out error,
                out shouldReportFailure);
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(GTConfig config,
            out Texture2D tex, out Vector3 lutParamsSample, out string error,
            out bool shouldReportFailure)
        {
            return BuiltInLutCache.GetOrBake(config, out tex, out lutParamsSample, out error,
                out shouldReportFailure);
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(GT7Volume vol,
            HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo, int lutSize, out Texture2D tex,
            out Vector3 lutParamsSample, out string error, out bool shouldReportFailure)
        {
            tex = null;
            lutParamsSample = default;
            error = null;
            shouldReportFailure = false;
            if (vol == null)
            {
                error = "GT7 volume component is missing.";
                shouldReportFailure = ShouldReportIntegrationFailure(
                    ToneMappingMode.GT7, MaterialPreparationStatus.Invalid, error);
                return MaterialPreparationStatus.Invalid;
            }

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.targetPeakNits.value;
            if (hdr && vol.detectPeakNits.value)
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;

            return TryGetOrBakeLut(vol.ToConfig(targetPeakNits, hdr, lutSize), out tex,
                out lutParamsSample, out error, out shouldReportFailure);
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(AgXVolume vol,
            HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo, int lutSize, out Texture2D tex,
            out Vector3 lutParamsSample, out string error, out bool shouldReportFailure)
        {
            tex = null;
            lutParamsSample = default;
            error = null;
            shouldReportFailure = false;
            if (vol == null)
            {
                error = "AgX volume component is missing.";
                shouldReportFailure = ShouldReportIntegrationFailure(
                    ToneMappingMode.AgX, MaterialPreparationStatus.Invalid, error);
                return MaterialPreparationStatus.Invalid;
            }

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.maxNits.value;
            if (hdr && vol.detectBrightnessLimits.value)
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;

            return TryGetOrBakeLut(vol.ToConfig(targetPeakNits, hdr, lutSize), out tex,
                out lutParamsSample, out error, out shouldReportFailure);
        }

        private static MaterialPreparationStatus TryGetOrBakeLut(GTVolume vol,
            HDROutputUtils.HDRDisplayInformation? hdrDisplayInfo, int lutSize, out Texture2D tex,
            out Vector3 lutParamsSample, out string error, out bool shouldReportFailure)
        {
            tex = null;
            lutParamsSample = default;
            error = null;
            shouldReportFailure = false;
            if (vol == null)
            {
                error = "GT volume component is missing.";
                shouldReportFailure = ShouldReportIntegrationFailure(
                    ToneMappingMode.GT, MaterialPreparationStatus.Invalid, error);
                return MaterialPreparationStatus.Invalid;
            }

            var hdr = hdrDisplayInfo.HasValue;
            var targetPeakNits = vol.targetPeakNits.value;
            if (hdr && vol.detectPeakNits.value)
                targetPeakNits = hdrDisplayInfo.Value.maxToneMapLuminance;

            return TryGetOrBakeLut(vol.ToConfig(targetPeakNits, hdr, lutSize), out tex,
                out lutParamsSample, out error, out shouldReportFailure);
        }

        public static bool TryValidateCustomLut(Texture2D lut, out string error)
        {
            return CustomLutConfig.TryValidate(lut, out error);
        }

        private static bool TryPrepareCustomLut(Texture2D lut, out Vector3 sample,
            out string error, out bool shouldReportFailure)
        {
            return CustomLutCache.TryGetOrValidate(lut, out sample, out error,
                out shouldReportFailure);
        }

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

            var volumeManager = VolumeManager.instance;
            var stack = volumeManager?.stack;
            var customMode = stack?.GetComponent<CustomToneMapping>();
            if (customMode == null || customMode.mode.value == ToneMappingMode.None)
            {
                DisableCustomKeyword(material);
                return MaterialPreparationStatus.Disabled;
            }

            var lutSize = customMode.lutSize.value;
            Texture2D lut = null;
            Vector3 sample = default;
            string error = null;
            var shouldReportFailure = false;
            MaterialPreparationStatus status;

            switch (customMode.mode.value)
            {
                case ToneMappingMode.GT:
                    status = TryGetOrBakeLut(stack.GetComponent<GTVolume>(), hdrDisplayInfo, lutSize,
                        out lut, out sample, out error, out shouldReportFailure);
                    break;
                case ToneMappingMode.GT7:
                    status = TryGetOrBakeLut(stack.GetComponent<GT7Volume>(), hdrDisplayInfo, lutSize,
                        out lut, out sample, out error, out shouldReportFailure);
                    break;
                case ToneMappingMode.AgX:
                    status = TryGetOrBakeLut(stack.GetComponent<AgXVolume>(), hdrDisplayInfo, lutSize,
                        out lut, out sample, out error, out shouldReportFailure);
                    break;
                case ToneMappingMode.CustomLUT:
                    lut = customMode.lutTexture.value as Texture2D;
                    status = TryPrepareCustomLut(lut, out sample, out error, out shouldReportFailure)
                        ? MaterialPreparationStatus.Ready
                        : MaterialPreparationStatus.Invalid;
                    break;
                default:
                    status = MaterialPreparationStatus.Invalid;
                    error = $"Unsupported tone mapping mode: {customMode.mode.value}.";
                    shouldReportFailure = ShouldReportIntegrationFailure(
                        customMode.mode.value, status, error);
                    break;
            }

            if (status != MaterialPreparationStatus.Ready)
            {
                DisableCustomKeyword(material);
                LogFailure(customMode.mode.value, status, error, shouldReportFailure);
                return status;
            }

            SetupMaterial(material, lut, sample);
            return MaterialPreparationStatus.Ready;
        }

        private static bool ShouldReportIntegrationFailure(ToneMappingMode mode,
            MaterialPreparationStatus status, string error)
        {
            for (var i = 0; i < IntegrationFailures.Length; i++)
            {
                ref var failure = ref IntegrationFailures[i];
                if (!failure.HasFailure || failure.Mode != mode || failure.Status != status ||
                    failure.Error != error)
                    continue;

                if (failure.HasBeenReported)
                    return false;

                failure.HasBeenReported = true;
                return true;
            }

            for (var i = 0; i < IntegrationFailures.Length; i++)
            {
                ref var failure = ref IntegrationFailures[i];
                if (failure.HasFailure)
                    continue;

                failure.Mode = mode;
                failure.Status = status;
                failure.Error = error;
                failure.HasFailure = true;
                failure.HasBeenReported = true;
                return true;
            }

            var replacement = _integrationFailureCursor++ % IntegrationFailures.Length;
            IntegrationFailures[replacement] = new IntegrationFailure
            {
                Mode = mode,
                Status = status,
                Error = error,
                HasFailure = true,
                HasBeenReported = true
            };
            return true;
        }

        private static void LogFailure(ToneMappingMode mode, MaterialPreparationStatus status,
            string error, bool shouldReportFailure)
        {
            if (!shouldReportFailure)
                return;

            Debug.LogWarning($"Custom tone mapping disabled for {mode}: {error ?? status.ToString()}.");
        }

        private static void SetupMaterial(Material material, Texture2D lut, Vector3 sample)
        {
            if (!material.IsKeywordEnabled(TonemapCustomKeyword))
                material.EnableKeyword(TonemapCustomKeyword);
            material.SetTexture(CustomTonemapLut, lut);
            material.SetVector(CustomTonemapParams, sample);
        }

        private static void DisableCustomKeyword(Material material)
        {
            if (material.IsKeywordEnabled(TonemapCustomKeyword))
                material.DisableKeyword(TonemapCustomKeyword);
        }
    }
}
