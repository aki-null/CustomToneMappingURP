{
    if (_CustomTonemapMode == 1)
    {
        // ACES 2.0 on Unity scene-linear Rec.709 (URP's standard grading branch).
        colorLinear = CustomAces2TonemapAP1(mul(kCustomTonemapRec709ToAP1, colorLinear));   // 100-nit Rec.709 display-linear, [0,1]
    }
    else
    {
        float3 uvw = saturate(CustomTonemapLinearToLogC(colorLinear));
        colorLinear = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
    }
}
