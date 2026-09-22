#ifndef CUSTOM_TONEMAP_PARAMS_INCLUDED
#define CUSTOM_TONEMAP_PARAMS_INCLUDED
// Included by the package's chain pass and, through the URP customization, by URP's LutBuilderHdr and UberPost
// right after Core.hlsl. It pulls in Core's color library so the two ACES output functions exist before they are
// hooked below. The include guard keeps a customization that includes this file twice harmless.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/LogC.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
#include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/Aces2.hlsl"

TEXTURE2D(_CustomTonemapLut);
float3 _CustomTonemap_Params; // (1/lut_width, 1/lut_height, lut_height-1)
// 0: RGB LUT modes (GT, GT7, AgX, Custom LUT, and the ACES 2.0 LDR strip) through _CustomTonemapLut.
// 1: ACES 2.0 evaluated from _Aces2Atlas.
// A uniform branch, not a keyword: selecting it by keyword in LutBuilderHdr would take another URP edit. On mobile
// GPUs the untaken ACES 2.0 branch leaves the LUT modes at full occupancy with no spilling, so the chain pass does not
// need its own keyword either.
int _CustomTonemapMode;

// Standard grading's output -> ACEScg, the input of CustomAces2TonemapAP1. Column-vector convention: mul(m, rgb).
// Rec.709: Core ACES.hlsl sRGB_2_AP0 (the literals Aces2SdrToneMap.Rec709ToAP0 uses for the LDR strip) followed by
// Aces2.hlsl AP0_TO_AP1, composed in double. Rec.2020: Core HDROutput.hlsl RotateRec2020ToRec709 before that.
static const float3x3 kCustomTonemapRec709ToAP1 = float3x3(
    0.613191768, 0.339512053, 0.0473666678,
    0.0702068849, 0.916335629, 0.0134499765,
    0.020618886, 0.109567236, 0.869606609);
static const float3x3 kCustomTonemapRec2020ToAP1 = float3x3(
    0.975057376, 0.0195207408, 0.00549237201,
    0.00220722715, 0.995501527, 0.00228373533,
    0.00480446888, 0.0245315452, 0.970456717);

// ACES-aware grading for ACES 2.0. The URP customization can add
// `#pragma multi_compile_local_fragment _ _CUSTOM_TONEMAP_ACES2` to LutBuilderHdr. The package then enables
// _TONEMAP_ACES so URP grades in its ACES spaces, and URP's ACES branch calls these two Core functions with the
// graded color in AP0. In variants of _CUSTOM_TONEMAP_ACES2 they evaluate ACES 2.0 instead. Every other variant
// compiles to the original call.
float3 CustomTonemapAcesHook(float3 aces)
{
#if defined(_CUSTOM_TONEMAP_ACES2)
    // UrpBridge sets mode 1 and enables this keyword only together, so the branch is always taken.
    // It is there for register pressure on mobile GPUs: without it the compiler can interleave ACES 2.0 with URP's
    // ACES grading and double the registers, halving occupancy. The branch costs about 2% more arithmetic.
    [branch] if (_CustomTonemapMode == 1)
        aces = CustomAces2Tonemap(aces);   // 100-nit Rec.709 display-linear, [0,1]
    return aces;
#else
    return AcesTonemap(aces);
#endif
}

float3 CustomTonemapHdrAcesHook(float3 aces, float hdrBoost, float minNits, float maxNits, int reductionMode, bool skipOETF = false)
{
#if defined(_CUSTOM_TONEMAP_ACES2)
    // No branch as in CustomTonemapAcesHook: this variant was measured at full occupancy on mobile GPUs without it.
    // The atlas is baked for the display peak with Rec.2020 limiting primaries, in 100-nit units.
    float3 nits = RotateRec2020ToOutputSpace(CustomAces2Tonemap(aces)) * 100.0;
    return skipOETF ? nits : OETF(nits, maxNits);
#else
    return HDRMappingACES(aces, hdrBoost, minNits, maxNits, reductionMode, skipOETF);
#endif
}

#define AcesTonemap CustomTonemapAcesHook
#define HDRMappingACES CustomTonemapHdrAcesHook
#endif
