using System;

namespace CustomToneMapping.Baker.GT7
{
    public struct GT7Config : ILutConfig, IStripLutConfig, IEquatable<GT7Config>
    {
        // Target peak luminance in cd/m^2 for HDR initialize
        public float TargetPeakNits; // e.g., 1000, 2000, etc.

        // 1.0 linear == this many cd/m^2
        public float ReferenceLuminance;

        // cd/m^2
        public float SdrPaperWhite;

        // HDR only: URP's paper white, the nits of LUT value 1.0.
        public float PaperWhiteNits;

        // UCS selection (ICtCp or Jzazbz)
        public UcsMode Ucs;

        // Exponent scale factor for Jzazbz PQ exponent adjustment
        public float JzazbzExponentScaleFactor;

        // GTToneMappingCurveV2 parameters
        public float CurveAlpha;
        public float CurveMidPoint;
        public float CurveLinearSection;
        public float CurveToeStrength;

        // Blending and chroma fade parameters
        public float BlendRatio;
        public float FadeStart;
        public float FadeEnd;

        public bool IsHdrOutput { get; set; }
        public int LutSize { get; set; }

        // Nits of framebuffer 1.0, also used by the UCS conversions. In HDR, SDR white lands on PaperWhiteNits, so
        // midtones match SDR. PaperWhiteNits == SdrPaperWhite gives the reference mapping.
        public float EffectiveReferenceLuminance =>
            IsHdrOutput ? ReferenceLuminance * PaperWhiteNits / SdrPaperWhite : ReferenceLuminance;

        // Curve peak in framebuffer units: the display peak (HDR) or SDR white (SDR).
        public float FramebufferTarget => (IsHdrOutput ? TargetPeakNits : SdrPaperWhite) / EffectiveReferenceLuminance;

        // Framebuffer to LUT value, the same in SDR and HDR.
        // HDR peak = FramebufferTarget * OutputScale * PaperWhiteNits = TargetPeakNits.
        public float OutputScale => 1.0f / (SdrPaperWhite / ReferenceLuminance);

        public bool TryValidate(out string error)
        {
            if (!LutLayout.TryValidateSize(LutSize, out error))
                return false;

            if (!ValidationPrimitives.IsFinite(TargetPeakNits) ||
                !ValidationPrimitives.IsFinite(ReferenceLuminance) ||
                !ValidationPrimitives.IsFinite(SdrPaperWhite) ||
                !ValidationPrimitives.IsFinite(JzazbzExponentScaleFactor) ||
                !ValidationPrimitives.IsFinite(CurveAlpha) ||
                !ValidationPrimitives.IsFinite(CurveMidPoint) ||
                !ValidationPrimitives.IsFinite(CurveLinearSection) ||
                !ValidationPrimitives.IsFinite(CurveToeStrength) ||
                !ValidationPrimitives.IsFinite(BlendRatio) ||
                !ValidationPrimitives.IsFinite(FadeStart) ||
                !ValidationPrimitives.IsFinite(FadeEnd))
            {
                error = "GT7 tone mapping contains a non-finite parameter.";
                return false;
            }

            if (Ucs != UcsMode.ICtCp && Ucs != UcsMode.JzAzBz)
            {
                error = "GT7 tone mapping contains an unsupported UCS mode.";
                return false;
            }

            if (TargetPeakNits <= 0.0f || ReferenceLuminance <= 0.0f ||
                SdrPaperWhite <= 0.0f || JzazbzExponentScaleFactor <= 0.0f ||
                CurveAlpha <= 0.0f || CurveAlpha >= 1.0f ||
                CurveMidPoint <= 0.0f || CurveLinearSection <= 0.0f ||
                CurveLinearSection >= 1.0f || CurveToeStrength <= 0.0f ||
                BlendRatio < 0.0f || BlendRatio > 1.0f ||
                FadeEnd <= FadeStart)
            {
                error = "GT7 tone mapping contains an invalid curve or fade range.";
                return false;
            }

            if (IsHdrOutput && (!ValidationPrimitives.IsFinite(PaperWhiteNits) || PaperWhiteNits <= 0.0f))
            {
                error = "GT7 tone mapping needs a positive paper white for HDR output.";
                return false;
            }

            var k = (CurveLinearSection - 1.0f) / (CurveAlpha - 1.0f);
            if (!ValidationPrimitives.IsFinite(k) || Math.Abs(k) <= 1e-6f)
            {
                error = "GT7 curve parameters produce an invalid shoulder constant.";
                return false;
            }

            error = null;
            return true;
        }

        bool IStripLutConfig.HdrOutput => IsHdrOutput;
        void IStripLutConfig.BakeStripLut(ref UnityEngine.Texture2D texture) => GT7LutBaker.BakeStripLut(this, ref texture);

        public bool Equals(GT7Config other)
        {
            return HashUtil.FloatBitsEqual(TargetPeakNits, other.TargetPeakNits) &&
                   HashUtil.FloatBitsEqual(ReferenceLuminance, other.ReferenceLuminance) &&
                   HashUtil.FloatBitsEqual(SdrPaperWhite, other.SdrPaperWhite) &&
                   HashUtil.FloatBitsEqual(PaperWhiteNits, other.PaperWhiteNits) &&
                   Ucs == other.Ucs &&
                   HashUtil.FloatBitsEqual(JzazbzExponentScaleFactor, other.JzazbzExponentScaleFactor) &&
                   HashUtil.FloatBitsEqual(CurveAlpha, other.CurveAlpha) &&
                   HashUtil.FloatBitsEqual(CurveMidPoint, other.CurveMidPoint) &&
                   HashUtil.FloatBitsEqual(CurveLinearSection, other.CurveLinearSection) &&
                   HashUtil.FloatBitsEqual(CurveToeStrength, other.CurveToeStrength) &&
                   HashUtil.FloatBitsEqual(BlendRatio, other.BlendRatio) &&
                   HashUtil.FloatBitsEqual(FadeStart, other.FadeStart) &&
                   HashUtil.FloatBitsEqual(FadeEnd, other.FadeEnd) &&
                   IsHdrOutput == other.IsHdrOutput &&
                   LutSize == other.LutSize;
        }
    }
}