using CustomToneMapping.Baker.GT;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CustomToneMapping.URP.Editor
{
    [BurstCompile]
    public static class GTCurvePreview
    {
        private const float ConvergenceTarget = 0.999f;
        private const float XMaxFallbackMultiplier = 3.0f;
        private const float XMaxUpperBoundMultiplier = 50f;

        public static void DrawCurvePreview(Rect rect, GTConfig config, float peakNits, bool isHdr)
        {
            var graphRect = GTCurveDrawing.CreateGraphRect(rect);
            GTCurveDrawing.DrawBackground(graphRect);
            var mapper = new GTToneMapping();
            config.TargetPeakNits = peakNits;
            config.IsHdrOutput = isHdr;

            if (isHdr)
                mapper.InitializeAsHdr(config);
            else
                mapper.InitializeAsSdr(config);

            var maxValue = peakNits / config.ReferenceLuminance;
            var displayMax = isHdr ? maxValue : 1.0f;

            // Use the actual framebuffer target that the tone mapper uses
            var framebufferTarget = isHdr
                ? config.TargetPeakNits / config.ReferenceLuminance
                : config.SdrPaperWhite / config.ReferenceLuminance;

            var targetOutput = displayMax * ConvergenceTarget;

            // Account for SDR correction in convergence calculation
            var sdrCorrection = isHdr ? 1.0f : (config.ReferenceLuminance / config.SdrPaperWhite);
            var rawTargetOutput = targetOutput / sdrCorrection;

            var xMax = FindShoulderConvergencePoint(config, rawTargetOutput, framebufferTarget);

            if (xMax <= 0 || xMax > maxValue * XMaxUpperBoundMultiplier)
                xMax = maxValue * XMaxFallbackMultiplier;

            var xTickInfo = GTCurveDrawing.CalculateTickInterval(xMax, graphRect.width);
            var yTickInfo = GTCurveDrawing.CalculateTickInterval(displayMax, graphRect.height);

            GTCurveDrawing.DrawGrid(graphRect, xTickInfo, yTickInfo);
            GTCurveDrawing.DrawAxisLabels(graphRect, xTickInfo, yTickInfo);

            GUI.BeginClip(graphRect);
            var clippedRect = new Rect(0, 0, graphRect.width, graphRect.height);

            var m = config.LinearSectionStart;
            var l0 = (framebufferTarget - m) * config.LinearSectionLength / config.Contrast;
            DrawGTCurve(clippedRect, mapper, xMax, displayMax, m, l0);

            DrawKeyPoints(clippedRect, config, mapper, xMax, displayMax, framebufferTarget);
            GUI.EndClip();
        }

        [BurstCompile]
        private static unsafe void EvaluateGTCurve(GTToneMapping* mapper, ref NativeArray<float3> outPoints,
            ref NativeArray<float4> outColors, float xMax, float displayMax, float rectWidth, float rectHeight, float m,
            float l0)
        {
            var sampleCount = outPoints.Length;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)(sampleCount - 1);
                var x = t * xMax;

                var outputValue = mapper->ApplyToneMap(new float3(x, x, x)).x;
                var y = math.min(outputValue, displayMax);

                // Convert to screen coordinates
                var px = x / xMax * rectWidth;
                var py = rectHeight - y / displayMax * rectHeight;
                outPoints[i] = new float3(px, py, 0);

                // Calculate section weights for color blending
                var clampedInput = math.max(0, x);
                var w0 = 1.0f - math.smoothstep(0, m, clampedInput);
                var w2 = clampedInput > m + l0 ? 1.0f : 0.0f;
                var w1 = 1.0f - w0 - w2;

                var totalWeight = w0 + w1 + w2;
                if (totalWeight > 0f)
                {
                    w0 /= totalWeight;
                    w1 /= totalWeight;
                    w2 /= totalWeight;
                }

                var toeColor = new float4(GTCurveDrawing.ToeColor.r, GTCurveDrawing.ToeColor.g,
                    GTCurveDrawing.ToeColor.b, GTCurveDrawing.ToeColor.a);
                var linearColor = new float4(GTCurveDrawing.LinearColor.r, GTCurveDrawing.LinearColor.g,
                    GTCurveDrawing.LinearColor.b, GTCurveDrawing.LinearColor.a);
                var shoulderColor = new float4(GTCurveDrawing.ShoulderColor.r, GTCurveDrawing.ShoulderColor.g,
                    GTCurveDrawing.ShoulderColor.b, GTCurveDrawing.ShoulderColor.a);

                outColors[i] = toeColor * w0 + linearColor * w1 + shoulderColor * w2;
            }
        }

        private static unsafe void DrawGTCurve(Rect rect, GTToneMapping mapper, float xMax, float displayMax, float m,
            float l0)
        {
            var points = new NativeArray<float3>(GTCurveDrawing.CurveSampleCount, Allocator.Temp);
            var colors = new NativeArray<float4>(GTCurveDrawing.CurveSampleCount, Allocator.Temp);

            try
            {
                EvaluateGTCurve(&mapper, ref points, ref colors, xMax, displayMax, rect.width, rect.height, m, l0);
                GTCurveDrawing.DrawAAPolyline(points, colors, rect);
            }
            finally
            {
                points.Dispose();
                colors.Dispose();
            }
        }

        private static float FindShoulderConvergencePoint(GTConfig config, float targetOutput, float framebufferTarget)
        {
            var m = config.LinearSectionStart;
            var P = framebufferTarget;
            var a = config.Contrast;
            var l0 = (P - m) * config.LinearSectionLength / a;
            var S0 = m + l0;
            var S1 = m + a * l0;
            var C2 = a * P / (P - S1);

            // For the shoulder region, solve: P - (P - S1) * e^(-C2 * (x - S0) / P) = targetOutput
            // Solving for x: x = S0 - (P / C2) * ln((P - targetOutput) / (P - S1))
            if (C2 > 0 && P - targetOutput > 0 && P - S1 > 0)
            {
                var ratio = (P - targetOutput) / (P - S1);
                if (ratio > 0)
                {
                    var x = S0 - P / C2 * Mathf.Log(ratio);
                    return x;
                }
            }

            return -1f;
        }

        private static void DrawKeyPoints(Rect rect, GTConfig config, GTToneMapping mapper, float xMax, float yMax,
            float framebufferTarget)
        {
            var m = config.LinearSectionStart;
            var P = framebufferTarget;
            var a = config.Contrast;
            var l0 = (P - m) * config.LinearSectionLength / a;
            var S0 = m + l0;

            // Linear section start point - get actual output value after tone mapping
            var linearStartOutput = mapper.ApplyToneMap(new float3(m, m, m)).x;
            linearStartOutput = Mathf.Min(linearStartOutput, yMax);
            GTCurveDrawing.DrawKeyPoint(rect, m, linearStartOutput, xMax, yMax, GTCurveDrawing.LinearColor);

            // Shoulder start point
            if (S0 > 0 && S0 <= xMax)
            {
                var outputValue = mapper.ApplyToneMap(new float3(S0, S0, S0)).x;
                outputValue = Mathf.Min(outputValue, yMax);
                GTCurveDrawing.DrawKeyPoint(rect, S0, outputValue, xMax, yMax, GTCurveDrawing.ShoulderColor);
            }
        }
    }
}