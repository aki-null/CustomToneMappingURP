Shader "Hidden/CustomToneMapping/Tests/LogCSampling"
{
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapParams.hlsl"
        float3 _TestInput;

        // Isolate HDR LUT addressing from display gamut rotation and paper white.
        #define PaperWhite 1.0
        float3 RotateRec2020ToOutputSpace(float3 color) { return color; }

        float4 FragSdr(Varyings varyings) : SV_Target
        {
            float3 colorLinear = _TestInput;
            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/Tonemap.hlsl"
            return float4(colorLinear, 1);
        }

        float3 SampleHdr(float3 colorLinear)
        {
            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapHdr.hlsl"
        }

        float4 FragHdr(Varyings varyings) : SV_Target
        {
            return float4(SampleHdr(_TestInput), 1);
        }

        float4 FragLdr(Varyings varyings) : SV_Target
        {
            float3 input = _TestInput;
            #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapLdr.hlsl"
            return float4(input, 1);
        }

        float4 FragEncoder(Varyings varyings) : SV_Target
        {
            return float4(saturate(CustomTonemapLinearToLogC(_TestInput)), 1);
        }
        ENDHLSL

        Pass
        {
            Name "SDR"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSdr
            ENDHLSL
        }
        Pass
        {
            Name "HDR"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHdr
            ENDHLSL
        }
        Pass
        {
            Name "LDR"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLdr
            ENDHLSL
        }
        Pass
        {
            Name "Encoder"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragEncoder
            ENDHLSL
        }
    }
}
