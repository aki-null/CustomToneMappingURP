// Fixture for Aces2UrpTests: UberPost's tonemap keyword set (with _TONEMAP_CUSTOM as the customization adds it), so the bridge
// recognises the material as URP's LDR per-pixel path. Outputs 1 for _TONEMAP_ACES, 2 for _TONEMAP_CUSTOM.
Shader "Hidden/CustomToneMapping/Tests/ACES2Uber"
{
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ _HDR_GRADING _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_CUSTOM
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
                return v.xxxx;
            }
            ENDHLSL
        }
    }
}
