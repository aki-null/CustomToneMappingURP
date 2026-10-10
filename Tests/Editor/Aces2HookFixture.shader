// Fixture for Aces2UrpTests: LutBuilderHdr's include order and its two ACES output calls, to test the TonemapParams.hlsl hooks:
// with _TONEMAP_ACES + _CUSTOM_TONEMAP_ACES2 those calls must evaluate ACES 2.0; with _TONEMAP_ACES alone, URP's ACES.
Shader "Hidden/CustomToneMapping/Tests/ACES2Hook"
{
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            // No #pragma target, like LutBuilderHdr: the hooks must compile at URP's default target.
            #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_CUSTOM
            #pragma multi_compile_local_fragment _ _CUSTOM_TONEMAP_ACES2
            #pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapParams.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #if defined(HDR_COLORSPACE_CONVERSION)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
            #endif
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _HDROutputLuminanceParams; // xy: brightness min/max, z: paper white, w: 1/max
            float4 _HDROutputGradingParams;   // x: range reduction mode
            float3 _TestInput;                // ACEScg, as URP's ACES grading branch hands it over
            float4 Frag(Varyings input) : SV_Target
            {
                float3 aces = ACEScg_to_ACES(_TestInput);
                #if _TONEMAP_ACES
                    #ifdef HDR_COLORSPACE_CONVERSION
                    return float4(HDRMappingACES(aces, _HDROutputLuminanceParams.z, _HDROutputLuminanceParams.x, _HDROutputLuminanceParams.y, (int)_HDROutputGradingParams.x, true), 1);
                    #else
                    return float4(AcesTonemap(aces), 1);
                    #endif
                #else
                return float4(_TestInput, 1);
                #endif
            }
            ENDHLSL
        }
    }
}
