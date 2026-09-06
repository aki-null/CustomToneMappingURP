#ifndef CUSTOM_TONEMAPPING_LOGC_INCLUDED
#define CUSTOM_TONEMAPPING_LOGC_INCLUDED

// ARRI LogC3, SUP 3.x, EI 1000, linear scene exposure factors.
// Keep in sync with LutBaker.AlexaLogC. Unity's LinearToLogC omits the
// linear toe, so it must not be used to address our standard LogC LUTs.
// This shaper is independent of URP's internal grading LUT encoding.
// https://www.arri.com/resource/blob/31918/66f56e6abb6e5b6553929edf9aa7483e/2017-03-alexa-logc-curve-in-vfx-data.pdf
float3 CustomTonemapLinearToLogC(float3 colorLinear)
{
    float3 linearSegment = 5.301883 * colorLinear + 0.092814;
    // Keep the unused logarithmic branch finite for zero/negative inputs.
    float3 logSegment = 0.244161 * log10(max(5.555556 * colorLinear + 0.047996, 1e-6)) + 0.386036;
    return float3(
        colorLinear.x > 0.011361 ? logSegment.x : linearSegment.x,
        colorLinear.y > 0.011361 ? logSegment.y : linearSegment.y,
        colorLinear.z > 0.011361 ? logSegment.z : linearSegment.z);
}

#endif
