using System;

namespace CustomToneMapping.Baker.ACES2
{
    // SDR and HDR use the same Academy transform with different parameters. SDR is always 100 nits. An HDR peak
    // outside the Academy range of 100 to 10000 nits gets the nearest end.
    internal readonly struct Aces2Config : IEquatable<Aces2Config>
    {
        public const float MinPeakNits = 100f;
        public const float MaxPeakNits = 10000f;

        public readonly float PeakNits;
        public readonly bool IsHdrOutput;

        public Aces2Config(float peakNits, bool isHdrOutput)
        {
            PeakNits = isHdrOutput ? Math.Clamp(peakNits, MinPeakNits, MaxPeakNits) : MinPeakNits;
            IsHdrOutput = isHdrOutput;
        }

        public bool TryValidate(out string error)
        {
            if (float.IsNaN(PeakNits))
            {
                error = "ACES 2.0 peak luminance must be a number.";
                return false;
            }

            error = null;
            return true;
        }

        public bool Equals(Aces2Config other) =>
            HashUtil.FloatBitsEqual(PeakNits, other.PeakNits) && IsHdrOutput == other.IsHdrOutput;

        public override bool Equals(object obj) => obj is Aces2Config other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(PeakNits, IsHdrOutput);
    }
}
