#include "Packages/net.aki-null.tonemapping/Runtime/URP/Shaders/LogC.hlsl"

TEXTURE2D(_CustomTonemapLut);
float3 _CustomTonemap_Params; // (1/lut_width, 1/lut_height, lut_height-1)
