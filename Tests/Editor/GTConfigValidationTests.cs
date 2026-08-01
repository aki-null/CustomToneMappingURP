using CustomToneMapping.Baker.GT;
using NUnit.Framework;
using UnityEngine;

namespace CustomToneMapping.Tests
{
    public class GTConfigValidationTests
    {
        [Test]
        public void ValidConfigPassesValidation()
        {
            Assert.IsTrue(CreateConfig().TryValidate(out var error), error);
        }

        [Test]
        public void VolumeCanProduceDegenerateLinearSection()
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.GT.GTToneMapping>();
            volume.linearSectionLength.value = 1.0f;

            var config = volume.ToConfig(volume.targetPeakNits.value, false, 32);
            Object.DestroyImmediate(volume);

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("out-of-range", error);
        }

        [Test]
        public void VolumeCanProduceInvalidShoulderDenominator()
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.GT.GTToneMapping>();
            volume.sdrPaperWhite.value = 50.0f;
            volume.referenceLuminance.value = 1000.0f;
            volume.linearSectionStart.value = 0.1f;

            var config = volume.ToConfig(volume.targetPeakNits.value, false, 32);
            Object.DestroyImmediate(volume);

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("denominator", error);
        }

        [Test]
        public void RejectsNonFiniteParameters()
        {
            var config = CreateConfig();
            config.Contrast = float.NaN;

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("non-finite", error);
        }

        private static GTConfig CreateConfig() => new()
        {
            TargetPeakNits = 1000.0f,
            IsHdrOutput = false,
            ReferenceLuminance = 203.0f,
            SdrPaperWhite = 100.0f,
            Contrast = 1.0f,
            LinearSectionStart = 0.18f,
            LinearSectionLength = 0.18f,
            BlackTightness = 1.0f,
            BlackOffset = 0.0f,
            LutSize = 32
        };
    }
}
