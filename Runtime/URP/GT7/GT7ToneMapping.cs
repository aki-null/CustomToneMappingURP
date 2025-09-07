using System;
using CustomToneMapping.Baker.GT7;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/GT7 Tone Mapping")]
    public sealed class GT7ToneMapping : VolumeComponent
    {
        public const float DefaultReferenceLuminance = 100.0f;
        public const float DefaultSdrPaperWhite = 100.0f;
        public const UcsMode DefaultUcs = UcsMode.ICtCp;
        public const float DefaultJzazbzExponentScale = 1.7f;
        public const float DefaultCurveAlpha = 0.01f;
        public const float DefaultCurveMidPoint = 0.22f;
        public const float DefaultCurveLinearSection = 0.527623f;
        public const float DefaultCurveToeStrength = 1.33f;
        public const float DefaultBlendRatio = 0.6f;
        public const float DefaultFadeStart = 0.98f;
        public const float DefaultFadeEnd = 1.16f;

        [Tooltip("Automatically detect display peak luminance for HDR.")]
        public BoolParameter detectPeakNits = new(true);

        [Tooltip("Target peak luminance in nits for HDR tonemapping.")]
        public ClampedFloatParameter targetPeakNits = new(1000.0f, 100.0f, 10000.0f);

        [Tooltip("SDR paper white (nits). Only used if integrating SDR path.")] [InspectorName("SDR Paper White")]
        public ClampedFloatParameter sdrPaperWhite = new(DefaultSdrPaperWhite, 50.0f, 1000.0f);

        [Tooltip("1.0 linear in frame-buffer corresponds to this reference luminance (nits).")]
        public ClampedFloatParameter referenceLuminance = new(DefaultReferenceLuminance, 1.0f, 1000.0f);

        [InspectorName("Alpha")] public ClampedFloatParameter curveAlpha = new(DefaultCurveAlpha, 0.01f, 0.99f);

        [InspectorName("Mid Point")]
        public ClampedFloatParameter curveMidPoint = new(DefaultCurveMidPoint, 0.01f, 1.0f);

        [InspectorName("Linear Section")]
        public ClampedFloatParameter curveLinearSection = new(DefaultCurveLinearSection, 0.01f, 0.99f);

        [InspectorName("Toe Strength")]
        public ClampedFloatParameter curveToeStrength = new(DefaultCurveToeStrength, 1.0f, 5.0f);

        [InspectorName("UCS Mode")] public UcsModeParameter ucs = new(DefaultUcs);

        [InspectorName("JzAzBz Exponent Scale")]
        public ClampedFloatParameter jzazbzExponentScale = new(DefaultJzazbzExponentScale, 0.25f, 4.0f);

        public ClampedFloatParameter blendRatio = new(DefaultBlendRatio, 0.0f, 1.0f);
        public ClampedFloatParameter fadeStart = new(DefaultFadeStart, 0.0f, 2.0f);
        public ClampedFloatParameter fadeEnd = new(DefaultFadeEnd, 0.0f, 4.0f);

        public GT7Config ToConfig(float peakNits, bool isHdrOutput)
        {
            return new GT7Config
            {
                TargetPeakNits = peakNits,
                IsHdrOutput = isHdrOutput,
                ReferenceLuminance = referenceLuminance.value,
                SdrPaperWhite = sdrPaperWhite.value,
                Ucs = ucs.value,
                JzazbzExponentScaleFactor = jzazbzExponentScale.value,
                CurveAlpha = curveAlpha.value,
                CurveMidPoint = curveMidPoint.value,
                CurveLinearSection = curveLinearSection.value,
                CurveToeStrength = curveToeStrength.value,
                BlendRatio = blendRatio.value,
                FadeStart = fadeStart.value,
                FadeEnd = fadeEnd.value,
            };
        }
    }
}