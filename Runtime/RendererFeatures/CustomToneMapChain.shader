Shader "Hidden/CustomToneMapChain"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma multi_compile_local_fragment _ HDR_COLORSPACE_CONVERSION
        #pragma multi_compile_local_fragment _ LEGACY_RENDER_PATH

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #if defined(HDR_COLORSPACE_CONVERSION)
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
        #endif

        #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapParams.hlsl"

        #if defined(HDR_COLORSPACE_CONVERSION)
        float4 _HDROutputLuminanceParams; // xy: brightness min/max, z: paper white brightness, w: 1.0 / brightness max
        #define PaperWhite _HDROutputLuminanceParams.z
        #endif

        TEXTURE2D(_MainTex);

        #if defined(HDR_COLORSPACE_CONVERSION)
        // Helper function to reverse URP's HDR processing before custom tonemapping
        float3 ReverseHDRProcessing(float3 hdrColor)
        {
            // Reverse paper white scaling (convert from nits back to linear)
            hdrColor /= PaperWhite;

            // Reverse colorspace rotation to get back to Rec2020
            if (_HDRColorspace == HDRCOLORSPACE_REC709)
            {
                hdrColor = RotateRec709ToRec2020(hdrColor);
            }
            else if (_HDRColorspace == HDRCOLORSPACE_P3D65)
            {
                hdrColor = RotateP3D65ToRec2020(hdrColor);
            }
            // else: already in Rec2020, no rotation needed

            return hdrColor;
        }
        #endif
        ENDHLSL

        Pass
        {
            Name "Tone Map"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #if !defined(LEGACY_RENDER_PATH)
            FRAMEBUFFER_INPUT_X_HALF(0);
            #endif

            float3 ToneMap(float3 color)
            {
                #if defined(HDR_COLORSPACE_CONVERSION)
                // HDR Display Output: Reverse URP's HDR processing
                // The internal LUT contains colors that have been rotated to output space and scaled by paper white
                half3 colorLinear = ReverseHDRProcessing(color);
                #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/TonemapHdr.hlsl"
                #else
                // SDR Output
                half3 colorLinear = color;
                #include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/Tonemap.hlsl"
                return colorLinear;
                #endif
            }

            float4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 color;
                #if defined(LEGACY_RENDER_PATH)
                // Legacy path: Sample from _MainTex
                color = LOAD_TEXTURE2D(_MainTex, input.positionCS.xy).rgb;
                #else
                // RenderGraph path: Use framebuffer fetch
                color = LOAD_FRAMEBUFFER_X_INPUT(0, input.positionCS.xy).rgb;
                #endif

                return float4(ToneMap(color), 1.0);
            }
            ENDHLSL
        }
    }
}