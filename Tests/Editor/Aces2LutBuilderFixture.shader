// Fixture for Aces2UrpTests: the 1.3 LutBuilderHdr keyword layout (URP's tonemap set plus _CUSTOM_TONEMAP_ACES2).
// Outputs which keywords the selected variant carries (1: _TONEMAP_ACES, 2: _TONEMAP_CUSTOM, 4: _CUSTOM_TONEMAP_ACES2).
Shader "Hidden/CustomToneMapping/Tests/ACES2LutBuilder"
{
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_CUSTOM
            #pragma multi_compile_local_fragment _ _CUSTOM_TONEMAP_ACES2
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 Frag(Varyings input) : SV_Target
            {
                float v = 0;
                #ifdef _TONEMAP_ACES
                v += 1;
                #endif
                #ifdef _TONEMAP_CUSTOM
                v += 2;
                #endif
                #ifdef _CUSTOM_TONEMAP_ACES2
                v += 4;
                #endif
                return v.xxxx;
            }
            ENDHLSL
        }
    }
}
