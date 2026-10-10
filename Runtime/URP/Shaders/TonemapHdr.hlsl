{
    if (_CustomTonemapMode == 1)
    {
        // ACES 2.0 on URP's HDR grading output (Rec.2020 linear), to nits. Paper white is exposure, like URP's ACES,
        // so 100 gives the Academy output. Evaluated per texel, not from a strip; Aces2LutBaker says why.
        float3 aces2Input = mul(kCustomTonemapRec2020ToAP1, colorLinear) * (PaperWhite * 0.01);
        colorLinear = RotateRec2020ToOutputSpace(CustomAces2TonemapAP1(aces2Input)) * 100.0;
    }
    else
    {
        float3 uvw = saturate(CustomTonemapLinearToLogC(colorLinear));
        colorLinear = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
        // 1.0 is SDR white, shown at paper white. Built-in LUTs are baked for it (UrpBridge), so their peak lands on the
        // target peak.
        colorLinear = RotateRec2020ToOutputSpace(colorLinear) * PaperWhite;
    }
}
