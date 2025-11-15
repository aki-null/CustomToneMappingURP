namespace CustomToneMapping.Baker.GT7
{
    public struct GT7Config : ILutConfig
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
    }
}