using System;
using CustomToneMapping.Baker.AgX;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/AgX Tone Mapping")]
    public sealed class AgXToneMapping : VolumeComponent
    {
        [Tooltip("Automatically detect display peak luminance for HDR.")]
        public BoolParameter detectBrightnessLimits = new(true);

        [Tooltip("Target peak luminance in nits for HDR tonemapping.")]
        public ClampedFloatParameter maxNits = new(1000.0f, 100.0f, 10000.0f);

        [Tooltip("SDR paper white (nits)")] [InspectorName("SDR Paper White")]
        public ClampedFloatParameter sdrPaperWhite = new(250.0f, 50.0f, 1000.0f);

        public AgXLookParameter look = new(AgXLookPreset.None);

        [Tooltip("Look intensity/blend amount (0-1). 0 = no look applied, 1 = full look intensity.")]
        public ClampedFloatParameter lookIntensity = new(1.0f, 0.0f, 1.0f);

        [Tooltip("HDR color purity/saturation (0-1).")] [InspectorName("Purity")]
        public ClampedFloatParameter hdrPurity = new(0.5f, 0.0f, 1.0f);

        [Tooltip("Extra shoulder power factor for HDR highlights.")] [InspectorName("Extra Power Factor")]
        public ClampedFloatParameter hdrExtraPowerFactor = new(2.0f, 1.0f, 4.0f);

        [Tooltip("Apply P3 gamut limiting.")] [InspectorName("Limit to DCI-P3")]
        public BoolParameter useP3Limit = new(false);

        private AgXToneMapping()
        {
            displayName = "AgX Tone Mapping";
        }

        public AgXConfig ToConfig(float peakNits, bool isHdrOutput, int lutSize)
        {
            // Configure look
            var lookConfig = AgXLookConfig.GetPreset(look.value);
            // Override intensity based on preset - None preset should have no effect
            lookConfig.Intensity = look.value == AgXLookPreset.None ? 0.0f : lookIntensity.value;

            return new AgXConfig
            {
                HdrMaxNits = peakNits,
                SdrMaxNits = sdrPaperWhite.value,
                HdrPurity = hdrPurity.value,
                HdrExtraPowerFactor = hdrExtraPowerFactor.value,
                UseP3Limit = useP3Limit.value,
                LookConfig = lookConfig,
                IsHdrOutput = isHdrOutput,
                LutSize = lutSize
            };
        }
    }
}