using System;

namespace CustomToneMapping.Baker.AgX
{
    public struct AgXConfig : ILutConfig, IEquatable<AgXConfig>
    {
        public float HdrMaxNits;
        public float SdrMaxNits;
        public float HdrPurity;
        public float HdrExtraPowerFactor;

        public bool UseP3Limit;

        public AgXLookConfig LookConfig;

        public uint ConfigHash
        {
            get
            {
                var h = HashUtil.Fnv1A32Offset;
                h = HashUtil.Hash32(h, 2u); // tone map type: AgX
                h = HashUtil.Hash32(h, IsHdrOutput ? 1u : 0u);
                h = HashUtil.Hash32(h, HdrMaxNits);
                h = HashUtil.Hash32(h, SdrMaxNits);
                h = HashUtil.Hash32(h, HdrPurity);
                h = HashUtil.Hash32(h, HdrExtraPowerFactor);
                h = HashUtil.Hash32(h, UseP3Limit ? 1u : 0u);
                h = HashUtil.Hash32(h, (int)LookConfig.LookPreset);
                h = HashUtil.Hash32(h, LookConfig.Intensity);
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