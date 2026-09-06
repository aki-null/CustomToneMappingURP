using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.Tests
{
    public class LogCSamplingTests
    {
        [TestCase(0, TestName = "SdrCustomLutUsesStandardLogC3Ei1000")]
        [TestCase(1, TestName = "HdrCustomLutUsesStandardLogC3Ei1000")]
        [TestCase(2, TestName = "LdrCustomLutUsesStandardLogC3Ei1000")]
        [TestCase(3, TestName = "LogC3Ei1000EncoderMatchesArriSpecification")]
        public void CustomLutCoordinatesMatchArriSpecification(int pass)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("LogC sampling regression requires a graphics device.");
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                Assert.Ignore("LogC sampling regression requires float textures and render targets.");

            var shader = Shader.Find("Hidden/CustomToneMapping/Tests/LogCSampling");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "LogC test shader failed compilation.");
            Assert.IsTrue(shader.isSupported);

            const int size = 32;
            var lut = new Texture2D(size * size, size, TextureFormat.RGBAFloat, false, true)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var material = new Material(shader);
            var target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try
            {
                // Imported-style LUT storing its own coordinates: trilinear
                // interpolation returns the encoder result without involving our baker.
                var pixels = new Color[size * size * size];
                for (var b = 0; b < size; b++)
                for (var g = 0; g < size; g++)
                for (var r = 0; r < size; r++)
                    pixels[g * size * size + b * size + r] =
                        new Color(r / (size - 1f), g / (size - 1f), b / (size - 1f), 1);
                lut.SetPixels(pixels);
                lut.Apply();
                material.SetTexture("_CustomTonemapLut", lut);
                material.SetVector("_CustomTonemap_Params", new Vector4(1f / (size * size), 1f / size, size - 1, 0));
                Assert.IsTrue(target.Create());

                // Mixed channels catch vector selection mistakes; values straddle
                // the toe join and include black, negative input, grey and HDR input.
                var inputs = new[]
                {
                    new Vector3(0, .001f, .01f),
                    new Vector3(.011360f, .011361f, .011362f),
                    new Vector3(.18f, 1, 16),
                    new Vector3(-.02f, -.001f, 100)
                };
                foreach (var input in inputs)
                {
                    material.SetVector("_TestInput", input);
                    using (var cmd = new CommandBuffer())
                    {
                        cmd.SetRenderTarget(target);
                        cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
                        Graphics.ExecuteCommandBuffer(cmd);
                    }
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    readback.Apply();
                    var actual = readback.GetPixel(0, 0);
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var x = (double)input[channel];
                        var encoded = x > .011361
                            ? .244161 * Math.Log10(5.555556 * x + .047996) + .386036
                            : 5.301883 * x + .092814;
                        var expected = Math.Max(0, Math.Min(1, encoded));
                        // Texture filtering weights can have fixed-point precision.
                        // Allow one 1/256-texel step on a 32-point coordinate ramp;
                        // test the encoder itself separately at float precision.
                        var tolerance = pass == 3 ? 0.000001 : 1.0 / (256 * (size - 1)) + 0.000001;
                        Assert.That(actual[channel], Is.EqualTo(expected).Within(tolerance),
                            $"pass={pass}, input={input}, channel={channel}");
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(lut);
            }
        }
    }
}
