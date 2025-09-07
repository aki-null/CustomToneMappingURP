using CustomToneMapping.Baker.GT7;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CustomToneMapping.URP.Editor
{
    [BurstCompile]
    internal static class GT7CurvePreview
    {
        private const float ClippingLineDashLength = 4f;
        private const float ClippingLineGapLength = 4f;
        private const float OverflowThreshold = 85f;
        private const float GraphBoundsPadding = 1.2f;

        private static readonly Color ClippingLineColor = new(1.0f, 1.0f, 1.0f);
        private static readonly Color InvalidCurveColor = new(1.0f, 0.6f, 0.6f);

        public static void DrawCurvePreview(Rect rect, GT7Config config, float targetPeakNits, bool isHdr)
        {
            var graphRect = GTCurveDrawing.CreateGraphRect(rect);
            GTCurveDrawing.DrawBackground(graphRect);

            var peakIntensity = targetPeakNits / config.ReferenceLuminance;

            var (curve, hasValidParameters) = InitializeAndValidateCurve(config, peakIntensity);

            var sdrCorrection = CalculateSdrCorrection(isHdr, config);
            var displayMax = isHdr ? peakIntensity : 1.0f;

            var convergentX = hasValidParameters ? FindConvergentPoint(curve, displayMax / sdrCorrection) : 0f;

            var (xMax, yMax) = CalculateGraphBounds(hasValidParameters, convergentX, curve,
                sdrCorrection, displayMax, peakIntensity, isHdr);

            var xTickInfo = GTCurveDrawing.CalculateTickInterval(xMax, graphRect.width);
            var yTickInfo = GTCurveDrawing.CalculateTickInterval(yMax, graphRect.height);

            GTCurveDrawing.DrawGrid(graphRect, xTickInfo, yTickInfo);
            GTCurveDrawing.DrawAxisLabels(graphRect, xTickInfo, yTickInfo);

            GUI.BeginClip(graphRect);

            if (hasValidParameters)
            {
                var clippedRect = new Rect(0, 0, graphRect.width, graphRect.height);
                var peakInFb = config.TargetPeakNits / config.ReferenceLuminance;
                var linearEnd = config.CurveLinearSection * peakInFb;

                DrawGT7Curve(clippedRect, curve, xMax, yMax, sdrCorrection, config.CurveMidPoint, linearEnd);

                DrawKeyPoints(clippedRect, curve, config, xMax, yMax, sdrCorrection, displayMax, convergentX);
            }
            else
            {
                var warningStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = InvalidCurveColor }
                };

                var textRect = new Rect(0, graphRect.height * 0.1f, graphRect.width, 20);
                GUI.Label(textRect, "Invalid Parameters", warningStyle);
            }

            GUI.EndClip();

            DrawClippingLine(graphRect, displayMax, yMax);
        }

        [BurstCompile]
        private static unsafe void EvaluateGT7Curve(GTToneMappingCurveV2* curve,
            ref NativeArray<float3> outPoints, ref NativeArray<float4> outColors, float xMax, float yMax,
            float rectWidth, float rectHeight, float sdrCorrection, float midPoint, float linearEnd)
        {
            var sampleCount = outPoints.Length;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)(sampleCount - 1);
                var x = t * xMax;

                var y = curve->Evaluate(x) * sdrCorrection;

                // Convert to screen coordinates
                var px = x / xMax * rectWidth;
                var py = rectHeight - y / yMax * rectHeight;
                outPoints[i] = new float3(px, py, 0);

                // Calculate the section weights for color blending
                float w0, w1, w2;
                if (x >= linearEnd)
                {
                    w0 = 0;
                    w1 = 0;
                    w2 = 1; // Shoulder region
                }
                else
                {
                    var weightLinear = math.smoothstep(0.0f, midPoint, x);
                    var weightToe = 1.0f - weightLinear;
                    w0 = weightToe;
                    w1 = weightLinear;
                    w2 = 0;
                }

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

        private static unsafe void DrawGT7Curve(
            Rect rect,
            GTToneMappingCurveV2 curve,
            float xMax,
            float yMax,
            float sdrCorrection,
            float midPoint,
            float linearEnd)
        {
            var points = new NativeArray<float3>(GTCurveDrawing.CurveSampleCount, Allocator.Temp);
            var colors = new NativeArray<float4>(GTCurveDrawing.CurveSampleCount, Allocator.Temp);

            try
            {
                EvaluateGT7Curve(&curve, ref points, ref colors, xMax, yMax, rect.width, rect.height,
                    sdrCorrection, midPoint, linearEnd);
                GTCurveDrawing.DrawAAPolyline(points, colors, rect);
            }
            finally
            {
                points.Dispose();
                colors.Dispose();
            }
        }

        private static (GTToneMappingCurveV2 curve, bool isValid) InitializeAndValidateCurve(GT7Config config,
            float peakIntensity)
        {
            var curve = new GTToneMappingCurveV2();
            var k = (config.CurveLinearSection - 1.0f) / (config.CurveAlpha - 1.0f);

            // Check for problematic parameter combinations
            if (math.abs(k) < 1e-6f || config.CurveLinearSection >= 1.0f)
                return (curve, false);

            if (k != 0)
            {
                var exponent = config.CurveLinearSection / k;
                if (math.abs(exponent) > OverflowThreshold)
                    return (curve, false);
            }

            curve.Initialize(
                peakIntensity,
                config.CurveAlpha,
                config.CurveMidPoint,
                config.CurveLinearSection,
                config.CurveToeStrength
            );

            return (curve, true);
        }

        private static float CalculateSdrCorrection(bool isHdr, GT7Config config)
        {
            if (isHdr) return 1.0f;

            var fbPaperWhite = config.SdrPaperWhite / config.ReferenceLuminance;
            return 1.0f / fbPaperWhite;
        }

        private static (float xMax, float yMax) CalculateGraphBounds(bool hasValidParameters, float convergentX,
            GTToneMappingCurveV2 curve, float sdrCorrection, float displayMax, float peakIntensity, bool isHdr)
        {
            // X-axis bounds
            var xMax = 1.0f;
            if (hasValidParameters && convergentX > 0)
            {
                xMax = convergentX / 0.8f; // Place convergent point at 80% of X-axis
            }

            // Y-axis bounds
            var yMax = displayMax;
            if (hasValidParameters)
            {
                var curveMax = curve.Evaluate(xMax * 0.9f);
                yMax = math.max(curveMax * sdrCorrection * 1.1f, displayMax * 1.2f);
            }

            return (xMax, yMax);
        }

        private static void DrawClippingLine(Rect graphRect, float displayMax, float yMax)
        {
            if (displayMax > yMax) return;

            var py = graphRect.y + graphRect.height - displayMax / yMax * graphRect.height;

            Handles.color = ClippingLineColor;
            const float totalLength = ClippingLineDashLength + ClippingLineGapLength;

            for (var x = graphRect.x; x < graphRect.x + graphRect.width; x += totalLength)
            {
                var endX = math.min(x + ClippingLineDashLength, graphRect.x + graphRect.width);
                Handles.DrawLine(
                    new Vector3(x, py, 0),
                    new Vector3(endX, py, 0)
                );
            }
        }

        private static Vector3 CalculateCurveWeights(float x, float midPoint, float linearEnd)
        {
            if (x >= linearEnd) return new Vector3(0, 0, 1); // Shoulder region

            var weightLinear = math.smoothstep(0.0f, midPoint, x);
            var weightToe = 1.0f - weightLinear;
            return new Vector3(weightToe, weightLinear, 0);
        }

        private static void DrawKeyPoints(Rect rect, GTToneMappingCurveV2 curve, GT7Config config,
            float xMax, float yMax, float sdrCorrection, float displayMax, float convergentX)
        {
            var peakInFb = config.TargetPeakNits / config.ReferenceLuminance;

            // Linear start point (midpoint where linear weight = 0.5)
            var midX = config.CurveMidPoint;
            var midY = curve.Evaluate(midX) * sdrCorrection;
            GTCurveDrawing.DrawKeyPoint(rect, midX, midY, xMax, yMax, GTCurveDrawing.LinearColor);

            // Shoulder start point (where linear ends and shoulder begins)
            var shoulderStart = config.CurveLinearSection * peakInFb;
            var shoulderStartY = curve.Evaluate(shoulderStart) * sdrCorrection;
            GTCurveDrawing.DrawKeyPoint(rect, shoulderStart, shoulderStartY, xMax, yMax, GTCurveDrawing.ShoulderColor);

            // Shoulder convergent point
            if (convergentX > 0 && convergentX <= xMax * GraphBoundsPadding)
            {
                GTCurveDrawing.DrawKeyPoint(rect, convergentX, displayMax, xMax, yMax, ClippingLineColor);
            }
        }

        private static float FindConvergentPoint(GTToneMappingCurveV2 curve, float targetValue)
        {
            var ka = curve.KA;
            var kb = curve.KB;
            var kc = curve.KC;

            if (kb == 0 || kc == 0) return 0;

            var argument = (targetValue - ka) / kb;

            if (argument <= 0) return 0;

            var x = math.log(argument) / kc;

            // Verify the solution is valid and in reasonable range
            if (x <= 0) return 0;

            // Double-check the solution by evaluating the curve
            var verifyValue = curve.Evaluate(x);
            if (math.abs(verifyValue - targetValue) < 0.01f)
            {
                return x;
            }

            // Return 0 to indicate no valid convergent point found
            return 0;
        }
    }
}