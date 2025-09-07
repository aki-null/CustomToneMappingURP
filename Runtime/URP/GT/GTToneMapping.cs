using System;
using CustomToneMapping.Baker.GT;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP.GT
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/GT Tone Mapping")]
    public sealed class GTToneMapping : VolumeComponent
    {
        public const float DefaultReferenceLuminance = 100.0f;
        public const float DefaultSdrPaperWhite = 100.0f;
        public const float DefaultContrast = 1.0f;
        public const float DefaultLinearSectionStart = 0.22f;
        public const float DefaultLinearSectionLength = 0.4f;
        public const float DefaultBlackTightness = 1.33f;
        public const float DefaultBlackOffset = 0.0f;

        [Tooltip("Automatically detect display peak luminance for HDR.")]
        public BoolParameter detectPeakNits = new(true);

        [Tooltip("Target peak luminance in nits for HDR tonemapping.")]
        public ClampedFloatParameter targetPeakNits = new(1000.0f, 100.0f, 10000.0f);

        [Tooltip("SDR paper white (nits).")] [InspectorName("SDR Paper White")]
        public ClampedFloatParameter sdrPaperWhite = new(DefaultSdrPaperWhite, 50.0f, 1000.0f);

        [Tooltip("1.0 linear in frame-buffer corresponds to this reference luminance (nits).")]
        public ClampedFloatParameter referenceLuminance = new(DefaultReferenceLuminance, 1.0f, 1000.0f);

        [Tooltip("Contrast control (a parameter)")]
        public ClampedFloatParameter contrast = new(DefaultContrast, 0.1f, 3.0f);

        [Tooltip("Linear section start point (m parameter)")]
        public ClampedFloatParameter linearSectionStart = new(DefaultLinearSectionStart, 0.01f, 0.5f);

        [Tooltip("Linear section length (l parameter)")]
        public ClampedFloatParameter linearSectionLength = new(DefaultLinearSectionLength, 0.1f, 1.0f);

        [Tooltip("Black/toe tightness (c parameter)")]
        public ClampedFloatParameter blackTightness = new(DefaultBlackTightness, 0.1f, 3.0f);

        [Tooltip("Black level offset (b parameter)")]
        public ClampedFloatParameter blackOffset = new(DefaultBlackOffset, 0.0f, 0.1f);

        public GTConfig ToConfig(float peakNits, bool isHdrOutput)
        {
            return new GTConfig
            {
                TargetPeakNits = peakNits,
                IsHdrOutput = isHdrOutput,
                ReferenceLuminance = referenceLuminance.value,
                SdrPaperWhite = sdrPaperWhite.value,
                Contrast = contrast.value,
                LinearSectionStart = linearSectionStart.value,
                LinearSectionLength = linearSectionLength.value,
                BlackTightness = blackTightness.value,
                BlackOffset = blackOffset.value
            };
        }
    }
}