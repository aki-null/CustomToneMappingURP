{
    float3 uvw = saturate(LinearToLogC(colorLinear));
    colorLinear = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
}
