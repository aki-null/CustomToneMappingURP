using System;

namespace CustomToneMapping.Baker.GT
{
    public struct GTConfig : ILutConfig, IStripLutConfig, IEquatable<GTConfig>
    {
        public float TargetPeakNits;
        public bool IsHdrOutput;
        public float ReferenceLuminance;
        public float SdrPaperWhite;

        // Curve parameters
        public float Contrast; // a parameter
        public float LinearSectionStart; // m parameter
        public float LinearSectionLength; // l parameter
        public float BlackTightness; // c parameter
        public float BlackOffset; // b parameter

        public int LutSize { get; set; }

        public bool TryValidate(out string error)
        {
            if (!LutLayout.TryValidateSize(LutSize, out error))
                return false;

            if (!ValidationPrimitives.IsFinite(TargetPeakNits) ||
                !ValidationPrimitives.IsFinite(ReferenceLuminance) ||
                !ValidationPrimitives.IsFinite(SdrPaperWhite) ||
                !ValidationPrimitives.IsFinite(Contrast) ||
                !ValidationPrimitives.IsFinite(LinearSectionStart) ||
                !ValidationPrimitives.IsFinite(LinearSectionLength) ||
                !ValidationPrimitives.IsFinite(BlackTightness) ||
                !ValidationPrimitives.IsFinite(BlackOffset))
            {
                error = "GT tone mapping contains a non-finite parameter.";
                return false;
            }

            if (TargetPeakNits <= 0.0f || ReferenceLuminance <= 0.0f ||
                SdrPaperWhite <= 0.0f || Contrast <= 0.0f ||
                LinearSectionStart <= 0.0f || LinearSectionLength <= 0.0f ||
                LinearSectionLength >= 1.0f || BlackTightness <= 0.0f ||
                BlackOffset < 0.0f)
            {
                error = "GT tone mapping contains an out-of-range parameter.";
                return false;
            }

            var physicalTarget = IsHdrOutput ? TargetPeakNits : SdrPaperWhite;
            var framebufferTarget = physicalTarget / ReferenceLuminance;
            var denominator = (framebufferTarget - LinearSectionStart) *
                              (1.0f - LinearSectionLength);

            if (!ValidationPrimitives.IsFinite(framebufferTarget) ||
                framebufferTarget <= LinearSectionStart ||
                !ValidationPrimitives.IsFinite(denominator) ||
                Math.Abs(denominator) <= 1e-6f)
            {
                error = "GT curve parameters produce an invalid shoulder denominator.";
                return false;
            }

            error = null;
            return true;
        }

        bool IStripLutConfig.HdrOutput => IsHdrOutput;
        void IStripLutConfig.BakeStripLut(ref UnityEngine.Texture2D texture) => GTLutBaker.BakeStripLut(this, ref texture);

        public bool Equals(GTConfig other)
        {
            return HashUtil.FloatBitsEqual(TargetPeakNits, other.TargetPeakNits) &&
                   IsHdrOutput == other.IsHdrOutput &&
                   HashUtil.FloatBitsEqual(ReferenceLuminance, other.ReferenceLuminance) &&
                   HashUtil.FloatBitsEqual(SdrPaperWhite, other.SdrPaperWhite) &&
                   HashUtil.FloatBitsEqual(Contrast, other.Contrast) &&
                   HashUtil.FloatBitsEqual(LinearSectionStart, other.LinearSectionStart) &&
                   HashUtil.FloatBitsEqual(LinearSectionLength, other.LinearSectionLength) &&
                   HashUtil.FloatBitsEqual(BlackTightness, other.BlackTightness) &&
                   HashUtil.FloatBitsEqual(BlackOffset, other.BlackOffset) &&
                   LutSize == other.LutSize;
        }
    }
}