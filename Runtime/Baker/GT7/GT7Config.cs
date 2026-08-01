using System;

namespace CustomToneMapping.Baker.GT7
{
    public struct GT7Config : ILutConfig, IEquatable<GT7Config>
    {
        // Target peak luminance in cd/m^2 for HDR initialize
        public float TargetPeakNits; // e.g., 1000, 2000, etc.

        // 1.0 linear == this many cd/m^2
        public float ReferenceLuminance;

        // cd/m^2
        public float SdrPaperWhite;

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

        public uint ConfigHash
        {
            get
            {
                var h = HashUtil.Fnv1A32Offset;
                h = HashUtil.Hash32(h, 1u); // tone map type: GT7
                h = HashUtil.Hash32(h, IsHdrOutput ? 1u : 0u);
                h = HashUtil.Hash32(h, TargetPeakNits);
                h = HashUtil.Hash32(h, ReferenceLuminance);
                h = HashUtil.Hash32(h, SdrPaperWhite);
                h = HashUtil.Hash32(h, (int)Ucs);
                h = HashUtil.Hash32(h, JzazbzExponentScaleFactor);
                h = HashUtil.Hash32(h, CurveAlpha);
                h = HashUtil.Hash32(h, CurveMidPoint);
                h = HashUtil.Hash32(h, CurveLinearSection);
                h = HashUtil.Hash32(h, CurveToeStrength);
                h = HashUtil.Hash32(h, BlendRatio);
                h = HashUtil.Hash32(h, FadeStart);
                h = HashUtil.Hash32(h, FadeEnd);
                h = HashUtil.Hash32(h, LutSize);
                return h;
            }
        }

        public bool IsHdrOutput { get; set; }
        public int LutSize { get; set; }

        public bool TryValidate(out string error)
        {
            if (!LutLayout.IsValidSize(LutSize))
            {
                error = $"LUT size must be between {LutLayout.MinSize} and {LutLayout.MaxSize}.";
                return false;
            }

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

            var k = (CurveLinearSection - 1.0f) / (CurveAlpha - 1.0f);
            if (!ValidationPrimitives.IsFinite(k) || Math.Abs(k) <= 1e-6f)
            {
                error = "GT7 curve parameters produce an invalid shoulder constant.";
                return false;
            }

            error = null;
            return true;
        }

        public bool Equals(GT7Config other)
        {
            return HashUtil.FloatBitsEqual(TargetPeakNits, other.TargetPeakNits) &&
                   HashUtil.FloatBitsEqual(ReferenceLuminance, other.ReferenceLuminance) &&
                   HashUtil.FloatBitsEqual(SdrPaperWhite, other.SdrPaperWhite) &&
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