{
    if (_CustomTonemapMode == 1)
    {
        // ACES 2.0 on Rec.2020 scene-linear, which URP's standard grading branch produces for HDR output.
        // The output is absolute nits. URP applies the display encoding afterwards, and its paper white does not
        // rescale it. This evaluates the transform per texel instead of sampling a strip; Aces2LutBaker says why.
        colorLinear = RotateRec2020ToOutputSpace(CustomAces2TonemapAP1(mul(kCustomTonemapRec2020ToAP1, colorLinear))) * 100.0;
    }
    else
    {
        float3 uvw = saturate(CustomTonemapLinearToLogC(colorLinear));
        colorLinear = ApplyLut2D(TEXTURE2D_ARGS(_CustomTonemapLut, sampler_LinearClamp), uvw, _CustomTonemap_Params);
        colorLinear = RotateRec2020ToOutputSpace(colorLinear) * PaperWhite;
    }
}
