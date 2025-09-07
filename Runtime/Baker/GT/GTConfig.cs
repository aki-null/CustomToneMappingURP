namespace CustomToneMapping.Baker.GT
{
    public struct GTConfig : ILutConfig
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

        public uint ConfigHash
        {
            get
            {
                var h = HashUtil.Fnv1A32Offset;
                h = HashUtil.Hash32(h, 0u); // tone map type: GT
                h = HashUtil.Hash32(h, IsHdrOutput ? 1u : 0u);
                h = HashUtil.Hash32(h, TargetPeakNits);
                h = HashUtil.Hash32(h, ReferenceLuminance);
                h = HashUtil.Hash32(h, SdrPaperWhite);
                h = HashUtil.Hash32(h, Contrast);
                h = HashUtil.Hash32(h, LinearSectionStart);
                h = HashUtil.Hash32(h, LinearSectionLength);
                h = HashUtil.Hash32(h, BlackTightness);
                h = HashUtil.Hash32(h, BlackOffset);
                return h;
            }
        }
    }
}