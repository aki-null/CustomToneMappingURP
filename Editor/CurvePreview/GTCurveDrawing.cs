using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CustomToneMapping.URP.Editor
{
    [BurstCompile]
    public static class GTCurveDrawing
    {
        private const float MinTickSpacing = 30f;
        private const float KeyPointRadius = 4f;
        private const float LabelMargin = 25f;
        private const float BottomMargin = 25f;

        public const int CurveSampleCount = 256;

        private static readonly Color BackgroundColor = new(0.1f, 0.1f, 0.1f, 1.0f);
        private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color AxisColor = new(0.5f, 0.5f, 0.5f, 1.0f);
        private static readonly Color LabelColor = new(0.7f, 0.7f, 0.7f, 1.0f);

        // Curve color
        public static readonly Color ToeColor = new(0.4f, 0.6f, 1f, 1f);
        public static readonly Color LinearColor = new(0.4f, 1f, 0.4f, 1f);
        public static readonly Color ShoulderColor = new(1f, 0.4f, 0.4f, 1f);

        public static Rect CreateGraphRect(Rect rect)
        {
            return new Rect(
                rect.x + LabelMargin,
                rect.y,
                rect.width - LabelMargin - 10f,
                rect.height - BottomMargin
            );
        }

        public static void DrawBackground(Rect graphRect)
        {
            EditorGUI.DrawRect(graphRect, BackgroundColor);
        }

        public struct TickInfo
        {
            public float Interval;
            public float Max;
            public int Decimals;

            public string FormatValue(float value)
            {
                // Round to avoid floating point display issues
                value = Mathf.Round(value * Mathf.Pow(10, Decimals)) / Mathf.Pow(10, Decimals);

                return value.ToString(Decimals == 0 ? "F0" : $"F{Decimals}");
            }
        }

        public static void DrawGrid(Rect graphRect, TickInfo xTicks, TickInfo yTicks)
        {
            Handles.color = GridColor;

            // Vertical grid lines
            for (var x = xTicks.Interval; x < xTicks.Max; x += xTicks.Interval)
            {
                var px = graphRect.x + x / xTicks.Max * graphRect.width;
                Handles.DrawLine(new Vector3(px, graphRect.y, 0), new Vector3(px, graphRect.y + graphRect.height, 0));
            }

            // Horizontal grid lines
            for (var y = yTicks.Interval; y < yTicks.Max; y += yTicks.Interval)
            {
                var py = graphRect.y + graphRect.height - y / yTicks.Max * graphRect.height;
                Handles.DrawLine(new Vector3(graphRect.x, py, 0), new Vector3(graphRect.x + graphRect.width, py, 0));
            }

            Handles.color = AxisColor;
            // X axis
            Handles.DrawLine(
                new Vector3(graphRect.x, graphRect.y + graphRect.height, 0),
                new Vector3(graphRect.x + graphRect.width, graphRect.y + graphRect.height, 0)
            );
            // Y axis
            Handles.DrawLine(
                new Vector3(graphRect.x, graphRect.y, 0),
                new Vector3(graphRect.x, graphRect.y + graphRect.height, 0)
            );
        }

        public static void DrawAxisLabels(Rect graphRect, TickInfo xTicks, TickInfo yTicks)
        {
            var xLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = LabelColor }
            };

            // X-axis tick labels (skip 0)
            for (var x = xTicks.Interval; x <= xTicks.Max; x += xTicks.Interval)
            {
                var px = graphRect.x + x / xTicks.Max * graphRect.width;
                var labelRect = new Rect(px - 15, graphRect.y + graphRect.height + 2, 30, 15);
                GUI.Label(labelRect, xTicks.FormatValue(x), xLabelStyle);
            }

            // Y-axis tick labels (skip 0)
            var yLabelStyle = new GUIStyle(xLabelStyle) { alignment = TextAnchor.MiddleRight };
            for (var y = yTicks.Interval; y <= yTicks.Max; y += yTicks.Interval)
            {
                var py = graphRect.y + graphRect.height - y / yTicks.Max * graphRect.height;
                var labelRect = new Rect(graphRect.x - 22, py - 7, 20, 15);
                GUI.Label(labelRect, yTicks.FormatValue(y), yLabelStyle);
            }

            // Add single 0 label at origin
            GUI.Label(
                new Rect(graphRect.x - 22, graphRect.y + graphRect.height + 2, 20, 15),
                "0",
                yLabelStyle
            );
        }

        public static TickInfo CalculateTickInterval(float maxValue, float size)
        {
            var maxTicks = Mathf.FloorToInt(size / MinTickSpacing);
            var roughInterval = maxValue / maxTicks;

            // Find a nice round interval
            var magnitude = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(roughInterval)));
            var normalized = roughInterval / magnitude;

            var interval = normalized switch
            {
                < 1.5f => 1f * magnitude,
                < 3f => 2f * magnitude,
                < 7f => 5f * magnitude,
                _ => 10f * magnitude
            };

            // Determine decimal places
            var decimals = 0;
            if (interval < 1f)
            {
                decimals = Mathf.CeilToInt(-Mathf.Log10(interval));
            }

            // Calculate actual max (rounded up to nearest interval)
            var actualMax = Mathf.Ceil(maxValue / interval) * interval;

            return new TickInfo
            {
                Interval = interval,
                Max = actualMax,
                Decimals = decimals
            };
        }

        public static void DrawKeyPoint(Rect rect, float x, float y, float xMax, float yMax, Color color)
        {
            var px = x / xMax * rect.width;
            var py = rect.height - y / yMax * rect.height;

            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(px, py, 0), Vector3.forward, KeyPointRadius);
        }

        #region Curve Drawing

        private static Material _cachedLineMaterial;
        private static Mesh _curveMesh;

        private static Material GetLineMaterial()
        {
            if (_cachedLineMaterial != null) return _cachedLineMaterial;
            _cachedLineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _cachedLineMaterial;
        }

        public static void DrawAAPolyline(NativeArray<float3> points, NativeArray<float4> colors, Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var width = Mathf.CeilToInt(rect.width);
            var height = Mathf.CeilToInt(rect.height);

            if (width <= 0 || height <= 0)
                return;

            // Create temporary render texture
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = false
            };
            var curveRenderTexture = RenderTexture.GetTemporary(descriptor);

            // Save current render target
            var oldRT = RenderTexture.active;
            RenderTexture.active = curveRenderTexture;

            try
            {
                GL.Clear(true, true, Color.clear);

                var lineMaterial = GetLineMaterial();
                lineMaterial.SetPass(0);

                const float lineWidth = 1f;
                const float aaWidth = 1.5f;
                const float halfWidth = lineWidth * 0.5f;
                const float totalHalfWidth = halfWidth + aaWidth;

                var isProjectLinear = PlayerSettings.colorSpace == ColorSpace.Linear;

                BuildCurveMesh(points, colors, halfWidth, totalHalfWidth, isProjectLinear);

                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);
                lineMaterial.SetPass(0);
                Graphics.DrawMeshNow(_curveMesh, Matrix4x4.identity);
                GL.PopMatrix();

                // Restore render target
                RenderTexture.active = oldRT;

                GUI.DrawTexture(rect, curveRenderTexture, ScaleMode.StretchToFill, true);
            }
            finally
            {
                RenderTexture.active = oldRT;
                RenderTexture.ReleaseTemporary(curveRenderTexture);
            }
        }

        [BurstCompile]
        private static void BuildCurveMeshInternal(ref NativeArray<float3> points, ref NativeArray<float4> colors,
            ref NativeArray<float3> outVertices, ref NativeArray<float4> outColors, ref NativeArray<int> outTriangles,
            float halfWidth, float totalHalfWidth, int segmentCount, bool isProjectLinear)
        {
            var vertexIndex = 0;
            var triangleIndex = 0;

            for (var i = 0; i < segmentCount; i++)
            {
                var p0 = points[i];
                var p1 = points[i + 1];
                var c0 = colors[i];
                var c1 = colors[i + 1];

                if (isProjectLinear)
                {
                    // Convert to linear space
                    c0.xyz = math.pow(c0.xyz, 2.2f);
                    c1.xyz = math.pow(c1.xyz, 2.2f);
                }

                var dir = math.normalize(p1 - p0);
                var perp = new float3(-dir.y, dir.x, 0);

                // Main body quad
                outVertices[vertexIndex] = p0 - perp * halfWidth;
                outVertices[vertexIndex + 1] = p0 + perp * halfWidth;
                outVertices[vertexIndex + 2] = p1 + perp * halfWidth;
                outVertices[vertexIndex + 3] = p1 - perp * halfWidth;

                outColors[vertexIndex] = c0;
                outColors[vertexIndex + 1] = c0;
                outColors[vertexIndex + 2] = c1;
                outColors[vertexIndex + 3] = c1;

                // Build triangles
                var baseIdx = vertexIndex;
                outTriangles[triangleIndex] = baseIdx;
                outTriangles[triangleIndex + 1] = baseIdx + 1;
                outTriangles[triangleIndex + 2] = baseIdx + 2;
                outTriangles[triangleIndex + 3] = baseIdx;
                outTriangles[triangleIndex + 4] = baseIdx + 2;
                outTriangles[triangleIndex + 5] = baseIdx + 3;

                vertexIndex += 4;
                triangleIndex += 6;

                // AA edges with gradual alpha fade for smoother transitions
                var c0Fade = new float4(c0.xyz, 0);
                var c1Fade = new float4(c1.xyz, 0);

                // Top AA edge quad
                outVertices[vertexIndex] = p0 + perp * halfWidth;
                outVertices[vertexIndex + 1] = p0 + perp * totalHalfWidth;
                outVertices[vertexIndex + 2] = p1 + perp * totalHalfWidth;
                outVertices[vertexIndex + 3] = p1 + perp * halfWidth;

                outColors[vertexIndex] = c0;
                outColors[vertexIndex + 1] = c0Fade;
                outColors[vertexIndex + 2] = c1Fade;
                outColors[vertexIndex + 3] = c1;

                baseIdx = vertexIndex;
                outTriangles[triangleIndex] = baseIdx;
                outTriangles[triangleIndex + 1] = baseIdx + 1;
                outTriangles[triangleIndex + 2] = baseIdx + 2;
                outTriangles[triangleIndex + 3] = baseIdx;
                outTriangles[triangleIndex + 4] = baseIdx + 2;
                outTriangles[triangleIndex + 5] = baseIdx + 3;

                vertexIndex += 4;
                triangleIndex += 6;

                // Bottom AA edge quad
                outVertices[vertexIndex] = p0 - perp * totalHalfWidth;
                outVertices[vertexIndex + 1] = p0 - perp * halfWidth;
                outVertices[vertexIndex + 2] = p1 - perp * halfWidth;
                outVertices[vertexIndex + 3] = p1 - perp * totalHalfWidth;

                outColors[vertexIndex] = c0Fade;
                outColors[vertexIndex + 1] = c0;
                outColors[vertexIndex + 2] = c1;
                outColors[vertexIndex + 3] = c1Fade;

                baseIdx = vertexIndex;
                outTriangles[triangleIndex] = baseIdx;
                outTriangles[triangleIndex + 1] = baseIdx + 1;
                outTriangles[triangleIndex + 2] = baseIdx + 2;
                outTriangles[triangleIndex + 3] = baseIdx;
                outTriangles[triangleIndex + 4] = baseIdx + 2;
                outTriangles[triangleIndex + 5] = baseIdx + 3;

                vertexIndex += 4;
                triangleIndex += 6;
            }
        }

        private static void BuildCurveMesh(NativeArray<float3> points, NativeArray<float4> colors, float halfWidth,
            float totalHalfWidth, bool isProjectLinear)
        {
            if (_curveMesh == null)
            {
                _curveMesh = new Mesh
                {
                    name = "Curve Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            const int segmentCount = CurveSampleCount - 1;
            const int quadCount = segmentCount * 3; // Main body + 2 AA edges per segment
            const int vertexCount = quadCount * 4;
            const int triangleCount = quadCount * 6;

            var nativeVertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var nativeMeshColors = new NativeArray<float4>(vertexCount, Allocator.Temp);
            var nativeTriangles = new NativeArray<int>(triangleCount, Allocator.Temp);

            try
            {
                BuildCurveMeshInternal(ref points, ref colors, ref nativeVertices, ref nativeMeshColors,
                    ref nativeTriangles, halfWidth, totalHalfWidth, segmentCount, isProjectLinear);
                _curveMesh.Clear();
                _curveMesh.SetVertices(nativeVertices, 0, vertexCount);
                _curveMesh.SetColors(nativeMeshColors, 0, vertexCount);
                _curveMesh.SetIndices(nativeTriangles, 0, triangleCount, MeshTopology.Triangles, 0);
            }
            finally
            {
                nativeVertices.Dispose();
                nativeMeshColors.Dispose();
                nativeTriangles.Dispose();
            }
        }

        #endregion
    }
}