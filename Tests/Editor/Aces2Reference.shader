// Runs Aces2.hlsl for Aces2Tests on a texture of ACEScg values (alpha: sample index, passed through so the test does
// not depend on the target's orientation), at URP's default shader target like the shipped shaders.
Shader "Hidden/CustomToneMapping/Tests/ACES2Reference"
{
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/Aces2.hlsl"
            TEXTURE2D_FLOAT(_TestInputs);
            float4 Frag(Varyings input) : SV_Target
            {
                float4 sample = SAMPLE_TEXTURE2D_LOD(_TestInputs, sampler_PointClamp, input.texcoord, 0);
                return float4(CustomAces2TonemapAP1(sample.rgb), sample.a);
            }
            ENDHLSL
        }
    }
}
