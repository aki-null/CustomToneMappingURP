using System;
using System.Runtime.CompilerServices;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using CustomToneMapping.Baker.AgX;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace CustomToneMapping.Baker
{
    [BurstCompile]
    public static class LutBaker
    {
        public const int MinLutSize = 32;
        public const int MaxLutSize = 65;

        private static readonly ProfilerMarker BakeLutMarker = new("CustomToneMapping.BakeLUT");

        public static int GetLutWidth(int lutSize) => lutSize * lutSize;
        public static int GetLutHeight(int lutSize) => lutSize;

        public static void BakeStripLut(ILutConfig config, ref Texture2D texture)
        {
            switch (config)
            {
                case GTConfig gtConfig:
                    GTLutBaker.BakeStripLut(gtConfig, ref texture);
                    break;
                case GT7Config gt7Config:
                    GT7LutBaker.BakeStripLut(gt7Config, ref texture);
                    break;
                case AgXConfig agxConfig:
                    AgXLutBaker.BakeStripLut(agxConfig, ref texture);
                    break;
                default:
                    throw new ArgumentException("Unsupported tone mapping configuration", nameof(config));
            }
        }

        private static GraphicsFormat ChooseFormat()
        {
            const GraphicsFormatUsage usage = GraphicsFormatUsage.Sample;

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, usage))
                return GraphicsFormat.R16G16B16A16_SFloat;

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, usage))
                return GraphicsFormat.R32G32B32A32_SFloat;

            // Fallback to 8-bit format
            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        internal static void BakeStripLut<T>(T toneMap, bool isHdrOutput, int lutSize, ref Texture2D texture)
            where T : struct, IToneMap
        {
            if (lutSize < MinLutSize || lutSize > MaxLutSize)
            {
                throw new ArgumentOutOfRangeException(nameof(lutSize),
                    $"LUT size must be between {MinLutSize} and {MaxLutSize}");
            }

            using (BakeLutMarker.Auto())
            {
                var h = GetLutHeight(lutSize);
                var w = GetLutWidth(lutSize);

                // Determine the format to use
                var format = ChooseFormat();

                if (!IsTextureReusable(texture, w, h, format))
                {
                    CoreUtils.Destroy(texture);
                    texture = null;
                }

                if (texture == null)
                {
                    const TextureCreationFlags flags = TextureCreationFlags.None |
                                                       TextureCreationFlags.DontUploadUponCreate |
                                                       TextureCreationFlags.DontInitializePixels;
                    texture = new Texture2D(w, h, format, flags)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        anisoLevel = 0
                    };
                }

                var pixelCount = w * h;
                using var pixels =
                    new NativeArray<half4>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var job = new LutJob<T>
                {
                    Width = w,
                    LutSize = lutSize,
                    Tonemapper = toneMap,
                    Output = pixels,
                    LutInputColorspace = isHdrOutput ? Colorspace.Rec2020 : Colorspace.Rec709,
                    LutOutputColorspace = isHdrOutput ? Colorspace.Rec2020 : Colorspace.Rec709
                };

                var handle = job.Schedule(pixelCount, 64);
                handle.Complete();

                SetPixelDataWithConversion(texture, pixels, format);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            }
        }

        private static void SetPixelDataWithConversion(Texture2D texture, NativeArray<half4> sourcePixels,
            GraphicsFormat format)
        {
            // For R16G16B16A16_SFloat, no conversion needed - direct copy
            if (format == GraphicsFormat.R16G16B16A16_SFloat)
            {
                texture.SetPixelData(sourcePixels, 0);
                return;
            }

            // For other formats, convert the pixel data
            var pixelCount = sourcePixels.Length;

            switch (format)
            {
                case GraphicsFormat.R32G32B32A32_SFloat:
                {
                    var converted = new NativeArray<float4>(pixelCount, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        for (var i = 0; i < pixelCount; i++)
                        {
                            converted[i] = new float4(sourcePixels[i]);
                        }

                        texture.SetPixelData(converted, 0);
                    }
                    finally
                    {
                        converted.Dispose();
                    }

                    break;
                }

                case GraphicsFormat.R8G8B8A8_UNorm:
                {
                    var converted = new NativeArray<byte>(pixelCount * 4, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        for (var i = 0; i < pixelCount; i++)
                        {
                            var pixel = sourcePixels[i];
                            var baseIdx = i * 4;
                            converted[baseIdx + 0] = (byte)math.clamp(pixel.x * 255f + 0.5f, 0, 255);
                            converted[baseIdx + 1] = (byte)math.clamp(pixel.y * 255f + 0.5f, 0, 255);
                            converted[baseIdx + 2] = (byte)math.clamp(pixel.z * 255f + 0.5f, 0, 255);
                            converted[baseIdx + 3] = (byte)math.clamp(pixel.w * 255f + 0.5f, 0, 255);
                        }

                        texture.SetPixelData(converted, 0);
                    }
                    finally
                    {
                        converted.Dispose();
                    }

                    break;
                }

                default:
                    throw new NotSupportedException($"Texture format {format} is not supported for LUT baking");
            }
        }

        private static bool IsTextureReusable(Texture2D texture, int expectedWidth, int expectedHeight,
            GraphicsFormat expectedFormat)
        {
            return texture != null &&
                   texture.width == expectedWidth &&
                   texture.height == expectedHeight &&
                   texture.graphicsFormat == expectedFormat &&
                   texture.isReadable;
        }

        [BurstCompile]
        public struct LutJob<T> : IJobParallelFor where T : struct, IToneMap
        {
            public int Width;
            public int LutSize;
            public T Tonemapper;

            public Colorspace LutInputColorspace;
            public Colorspace LutOutputColorspace;

            [WriteOnly] public NativeArray<half4> Output;

            public void Execute(int index)
            {
                var y = index / Width;
                var x = index - y * Width;

                var colorLutSpace = GetLutStripValue(x, y, LutSize);

                // Decode to linear
                var colorLinear = AlexaLogC.LogCToLinear(colorLutSpace);

                if (LutInputColorspace != Tonemapper.InputColorspace)
                {
                    colorLinear = LutInputColorspace switch
                    {
                        Colorspace.Rec709 => ColorUtility.Rec709ToRec2020(colorLinear),
                        Colorspace.Rec2020 => ColorUtility.Rec2020ToRec709(colorLinear),
                        _ => colorLinear
                    };
                }

                colorLinear = math.max(colorLinear, 0);

                var toneMapped = Tonemapper.ApplyToneMap(colorLinear);

                if (LutOutputColorspace != Tonemapper.OutputColorspace)
                {
                    toneMapped = LutOutputColorspace switch
                    {
                        Colorspace.Rec709 => ColorUtility.Rec2020ToRec709(toneMapped),
                        Colorspace.Rec2020 => ColorUtility.Rec709ToRec2020(toneMapped),
                        _ => toneMapped
                    };
                }

                toneMapped = math.max(toneMapped, 0);

                if (!Tonemapper.IsHDROutput)
                {
                    toneMapped = math.min(toneMapped, 1);
                }

                Output[index] = new half4(new half3(toneMapped), (half)1.0f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 GetLutStripValue(int pixelX, int pixelY, int lutSize)
        {
            float h = lutSize;

            // Determine which LUT strip this pixel belongs to
            // The texture is w x h where w = h * h
            // Each strip is h pixels wide
            var stripIndex = pixelX / lutSize;
            var xWithinStrip = pixelX % lutSize;

            var r = xWithinStrip / (h - 1);
            var g = pixelY / (h - 1);
            var b = stripIndex / (h - 1);

            return new float3(r, g, b);
        }

        // Alexa LogC (El 1000) converters
        private static class AlexaLogC
        {
            // Constants from URP HLSL (Color.hlsl)
            private const float Cut = 0.011361f;
            private const float A = 5.555556f;
            private const float B = 0.047996f;
            private const float C = 0.244161f;
            private const float D = 0.386036f;
            private const float E = 5.301883f;
            private const float F = 0.092819f;

            [BurstCompile]
            public static float3 LogCToLinear(float3 x)
            {
                // Piecewise precise path from URP Color.hlsl
                var hi = (Pow10((x - new float3(D)) / C) - new float3(B)) / A;
                var lo = (x - new float3(F)) / E;
                // threshold is on LogC domain: e*cut + f
                var th = E * Cut + F;
                var useHi = x > new float3(th);
                return math.select(lo, hi, useHi);
            }

            [BurstCompile]
            private static float3 Pow10(float3 v)
            {
                // 10^v = exp(v * ln(10))
                const float ln10 = 2.302585092994046f;
                return math.exp(v * ln10);
            }
        }
    }
}