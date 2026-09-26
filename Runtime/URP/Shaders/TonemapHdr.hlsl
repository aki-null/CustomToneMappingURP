{
    float3 uvw = saturate(CustomTonemapLinearToLogC(colorLinear));
    colorLinear = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
    // 1.0 is SDR white, shown at paper white. Built-in LUTs are baked for it (UrpBridge), so their peak lands on the
    // target peak.
    return RotateRec2020ToOutputSpace(colorLinear) * PaperWhite;
}
