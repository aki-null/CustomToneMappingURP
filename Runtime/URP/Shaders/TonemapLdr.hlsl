{
    float3 uvw = saturate(CustomTonemapLinearToLogC(input));
    input = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
}
