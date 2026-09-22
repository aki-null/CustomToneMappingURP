using System;

namespace CustomToneMapping.Baker.AgX
{
    public struct AgXConfig : ILutConfig, IStripLutConfig, IEquatable<AgXConfig>
    {
        public float HdrMaxNits;
        public float SdrMaxNits;
        public float HdrPurity;
        public float HdrExtraPowerFactor;

        public bool UseP3Limit;

        public AgXLookConfig LookConfig;

        public bool IsHdrOutput { get; set; }
        public int LutSize { get; set; }

        public bool TryValidate(out string error)
        {
            if (!LutLayout.TryValidateSize(LutSize, out error))
                return false;

            if (!ValidationPrimitives.IsFinite(HdrMaxNits) ||
                !ValidationPrimitives.IsFinite(SdrMaxNits) ||
                !ValidationPrimitives.IsFinite(HdrPurity) ||
                !ValidationPrimitives.IsFinite(HdrExtraPowerFactor))
            {
                error = "AgX tone mapping contains a non-finite parameter.";
                return false;
            }

            if (HdrMaxNits <= 0.0f || SdrMaxNits <= 0.0f ||
                HdrPurity < 0.0f || HdrPurity > 1.0f ||
                HdrExtraPowerFactor <= 0.0f)
            {
                error = "AgX tone mapping contains an out-of-range parameter.";
                return false;
            }

            if (IsHdrOutput && HdrMaxNits < SdrMaxNits)
            {
                error = "AgX HDR maximum luminance must be at least the SDR maximum luminance.";
                return false;
            }

            if (!LookConfig.TryValidate(out error))
                return false;

            error = null;
            return true;
        }

        bool IStripLutConfig.HdrOutput => IsHdrOutput;
        void IStripLutConfig.BakeStripLut(ref UnityEngine.Texture2D texture) => AgXLutBaker.BakeStripLut(this, ref texture);

        public bool Equals(AgXConfig other)
        {
            return HashUtil.FloatBitsEqual(HdrMaxNits, other.HdrMaxNits) &&
                   HashUtil.FloatBitsEqual(SdrMaxNits, other.SdrMaxNits) &&
                   HashUtil.FloatBitsEqual(HdrPurity, other.HdrPurity) &&
                   HashUtil.FloatBitsEqual(HdrExtraPowerFactor, other.HdrExtraPowerFactor) &&
                   UseP3Limit == other.UseP3Limit &&
                   LookConfig.Equals(other.LookConfig) &&
                   IsHdrOutput == other.IsHdrOutput &&
                   LutSize == other.LutSize;
        }
    }
}