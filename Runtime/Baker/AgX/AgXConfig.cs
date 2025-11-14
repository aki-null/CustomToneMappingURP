namespace CustomToneMapping.Baker.AgX
{
    public struct AgXConfig : ILutConfig
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
                h = HashUtil.Hash32(h, (uint)LutSize);
                return h;
            }
        }

        public bool IsHdrOutput { get; set; }
        public int LutSize { get; set; }
    }
}