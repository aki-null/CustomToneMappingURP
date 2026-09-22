using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using CustomToneMapping.Baker.ACES2;
using CustomToneMapping.URP;

namespace CustomToneMapping.Tests
{
    // The ACES 2.0 transform itself: the managed port against vectors frozen from the official CTL, Aces2.hlsl against
    // the port, and the parallel bake against the serial one.
    public class Aces2Tests
    {
        [Test]
        public void ExistingSerializedModesKeepTheirValues()
        {
            Assert.AreEqual(4, (int)ToneMappingMode.CustomLUT);
            Assert.AreEqual(5, (int)ToneMappingMode.ACES2);
        }

        [Test]
        public void NaNHdrPeakIsRejected() => Assert.IsFalse(new Aces2Config(float.NaN, true).TryValidate(out _));

        [TestCase(0, 100)] [TestCase(10001, 10000)] [TestCase(float.PositiveInfinity, 10000)]
        public void HdrPeakIsClampedToTheAcademyRange(float peak, float expected)
        {
            var config = new Aces2Config(peak, true);
            Assert.AreEqual(expected, config.PeakNits);
            Assert.IsTrue(config.TryValidate(out _));
        }

        [Test]
        public void SdrUsesTheSameSystemAt100Nits()
        {
            var a = new Aces2Config(1000, false);
            Assert.AreEqual(100, a.PeakNits);
            Assert.AreEqual(a, new Aces2Config(4000, false));
            Assert.AreNotEqual(a, new Aces2Config(100, true));
        }

        // The production atlas (Aces2LutBaker's parallel parameters and jobs) against the serial reference: the generated
        // init_ODTParams and the atlas builders run in order. Bisections to 1e-7 can flip on last-bit libm differences
        // between Burst and Mono, hence a tolerance.
        [TestCase(100)] [TestCase(4000)]
        public void ParallelBakeMatchesTheSerialReference(int peak)
        {
            bool hdr = peak != 100;
            var primaries = Aces2LutBaker.Primaries(hdr);
            var serial = AcademyTransform.init_ODTParams(peak, primaries);
            var parallel = Aces2LutBaker.InitParametersParallel(peak, primaries);
            var expected = new NativeArray<float4>(Aces2Atlas.Width * Aces2Atlas.Height, Allocator.Temp);
            NativeArray<float4> actual = default;
            try
            {
                double aInMax = Aces2Atlas.InputAMax(serial);
                for (int x = 0; x < Aces2Atlas.Width; x++) Aces2Atlas.BuildColumn(serial, expected, x, aInMax);
                Aces2Atlas.BuildPieces(serial, expected);
                actual = Aces2LutBaker.BuildPixels(parallel);
                for (int i = 0; i < expected.Length; i++)
                for (int c = 0; c < 4; c++)
                    Assert.That(actual[i][c], Is.EqualTo(expected[i][c]).Within(1e-4).Percent, $"atlas texel {i} channel {c}");

                var expectedConstants = new Vector4[Aces2Atlas.ConstantVectors];
                var actualConstants = new Vector4[Aces2Atlas.ConstantVectors];
                Aces2Atlas.BuildConstants(serial, expectedConstants, aInMax);
                Aces2Atlas.BuildConstants(parallel, actualConstants, Aces2Atlas.InputAMax(parallel));
                for (int i = 0; i < expectedConstants.Length; i++)
                for (int c = 0; c < 4; c++)
                    Assert.That(actualConstants[i][c], Is.EqualTo(expectedConstants[i][c]).Within(1e-4).Percent, $"constant {i * 4 + c}");
                Assert.AreEqual(serial.hue_linearity_search_range, parallel.hue_linearity_search_range);
            }
            finally
            {
                expected.Dispose();
                if (actual.IsCreated) actual.Dispose();
                Aces2LutBaker.Dispose(ref parallel);
            }
        }

        [TestCase(100)] [TestCase(1000)] [TestCase(4000)] [TestCase(10000)]
        public void PortMatchesTheAcademyReference(int peak)
        {
            var p = Aces2TestSupport.Parameters(peak, peak != 100);
            double tolerance = peak / 100.0 * 2e-4; // Tools/ACES2~/validate-reference.cjs's bound
            var inputs = Aces2ReferenceData.Inputs;
            var reference = Aces2ReferenceData.ForPeak(peak);
            for (int i = 0; i < inputs.Length; i++)
            {
                // The frozen vectors are the CTL output before the display clamp.
                var actual = AcademyTransform.outputTransform_fwd(Aces2TestSupport.AP1ToAP0(inputs[i]), p);
                for (int c = 0; c < 3; c++) Assert.That(actual[c], Is.EqualTo(reference[i][c]).Within(tolerance), $"{peak} nits, {inputs[i]}, channel {c}");
            }
        }

        // Aces2.hlsl against the port it is generated from, over the frozen vectors, rays through the gamut corners and
        // random colors from deep shadow to far past the peak, in one draw.
        [TestCase(100)] [TestCase(1000)] [TestCase(4000)] [TestCase(10000)]
        public void ShaderMatchesThePort(int peak)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var shader = Shader.Find("Hidden/CustomToneMapping/Tests/ACES2Reference");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            bool hdr = peak != 100;
            var p = Aces2TestSupport.Parameters(peak, hdr);
            const int size = 64;
            var inputs = ShaderInputs(size * size);
            Texture2D atlas = null;
            var material = new Material(shader);
            var source = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point };
            var target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            try
            {
                Assert.IsTrue(target.Create());
                var pixels = new Color[inputs.Length];
                for (int i = 0; i < inputs.Length; i++) pixels[i] = new Color(inputs[i].x, inputs[i].y, inputs[i].z, i);
                source.SetPixels(pixels);
                source.Apply(false, false);
                atlas = Aces2LutBaker.Bake(new Aces2Config(peak, hdr), out var constants);
                Aces2LutBaker.Bind(material, atlas, constants);
                material.SetTexture("_TestInputs", source);
                material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0)); // Blit.hlsl Vert: full texcoords
                Aces2TestSupport.Draw(material, target);
                var seen = new bool[inputs.Length];
                int failures = 0;
                double worst = 0;
                string worstSample = null;
                foreach (var gpu in Aces2TestSupport.ReadPixels(target))
                {
                    int i = (int)gpu.a;
                    seen[i] = true;
                    var expected = Aces2TestSupport.Aces2FromAP1(inputs[i], p);
                    for (int c = 0; c < 3; c++)
                    {
                        double error = Math.Abs(gpu[c] - expected[c]);
                        double excess = error / Tolerance(math.cmax(expected), peak);
                        if (excess > 1) failures++;
                        if (excess > worst) { worst = excess; worstSample = $"{inputs[i]} channel {c}: {gpu[c]} vs {expected[c]}"; }
                    }
                }
                Assert.That(seen, Is.All.True, "every sample drawn");
                Assert.AreEqual(0, failures, $"{peak} nits: {failures} channels out of tolerance, worst {worst:F2}x at {worstSample}");
            }
            finally
            {
                CoreUtils.Destroy(atlas);
                foreach (var o in new UnityEngine.Object[] { material, source, target }) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        // Relative to the pixel's brightest channel: a dim channel next to a bright one inherits the bright one's float
        // error, while a dark pixel must stay accurate. Measured worst: about a third of this.
        private static double Tolerance(double brightest, int peak) => 2e-5 * peak / 100.0 + 1e-3 * brightest;

        // The frozen vectors, the corner rays at a range of scales, then random colors spread over 30 stops around
        // mid grey, like Tools/ACES2~/validate-reference.cjs.
        private static Vector3[] ShaderInputs(int count)
        {
            var inputs = new System.Collections.Generic.List<Vector3>(Aces2ReferenceData.Inputs);
            foreach (var v in new[] { 1e-5f, .002f, .18f, 1, 16, 59, 100, 1000, 10000 })
            foreach (var ray in new[] { new Vector3(1, 1, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 1, 0), new Vector3(1, 0, 1), new Vector3(0, 1, 1) })
                inputs.Add(ray * v);
            uint seed = 0xace520;
            float Random() { seed = seed * 1664525 + 1013904223; return seed / 4294967296f; }
            while (inputs.Count < count)
                inputs.Add(new Vector3(.18f * Mathf.Pow(2, -14 + 30 * Random()), .18f * Mathf.Pow(2, -14 + 30 * Random()), .18f * Mathf.Pow(2, -14 + 30 * Random())));
            return inputs.ToArray();
        }
    }
}
