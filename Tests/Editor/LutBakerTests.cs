using System;
using CustomToneMapping.Baker;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;

namespace CustomToneMapping.Tests
{
    public class LutBakerTests
    {
        [Test]
        public void HdrFormatNeverFallsBackToUnorm()
        {
            if (LutBaker.TryChooseFormat(true, out var format))
                Assert.AreNotEqual(GraphicsFormat.R8G8B8A8_UNorm, format);
        }

        // These low-end grid points exercise the linear toe and distinguish
        // ARRI's EI 1000 offset from Unity's copied EI 1280 offset after fp16 storage.
        [TestCase(32, 3)]
        [TestCase(32, 4)]
        [TestCase(32, 5)]
        [TestCase(65, 6)]
        [TestCase(65, 7)]
        [TestCase(65, 10)]
        public void BakedGridUsesStandardLogC3Ei1000(int size, int coordinate)
        {
            using var pixels = new NativeArray<half4>(size * size * size, Allocator.TempJob);
            var job = new LutBaker.LutJob<IdentityToneMap>
            {
                Width = size * size,
                LutSize = size,
                Tonemapper = new IdentityToneMap(),
                LutInputColorspace = Colorspace.Rec709,
                LutOutputColorspace = Colorspace.Rec709,
                Output = pixels
            };
            var index = coordinate * size * size + coordinate * size + coordinate;
            job.Execute(index);

            // Independent double-precision reference from ARRI's specification.
            var encoded = coordinate / (double)(size - 1);
            var expected = encoded > 5.301883 * 0.011361 + 0.092814
                ? (Math.Pow(10, (encoded - 0.386036) / 0.244161) - 0.047996) / 5.555556
                : (encoded - 0.092814) / 5.301883;
            var stored = (float)(half)(float)expected;
            Assert.AreEqual(stored, (float)pixels[index].x);
            Assert.AreEqual(stored, (float)pixels[index].y);
            Assert.AreEqual(stored, (float)pixels[index].z);
        }

        private struct IdentityToneMap : IToneMap
        {
            public float3 ApplyToneMap(float3 rgb) => rgb;
            public bool IsHDROutput => false;
            public Colorspace InputColorspace => Colorspace.Rec709;
            public Colorspace OutputColorspace => Colorspace.Rec709;
        }
    }
}
