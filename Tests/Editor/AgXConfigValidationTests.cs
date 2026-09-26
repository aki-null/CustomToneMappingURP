using CustomToneMapping.Baker.AgX;
using NUnit.Framework;
using UnityEngine;

namespace CustomToneMapping.Tests
{
    public class AgXConfigValidationTests
    {
        [Test]
        public void ValidConfigPassesValidation()
        {
            Assert.IsTrue(CreateConfig().TryValidate(out var error), error);
        }

        // A display dimmer than paper white is valid: the SDR image is scaled to its peak (PaperWhiteTests).
        [TestCase(true)]
        [TestCase(false)]
        public void AllowsPeakBelowPaperWhite(bool isHdrOutput)
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.AgXToneMapping>();
            volume.maxNits.value = 100.0f;

            var config = volume.ToConfig(volume.maxNits.value, 400.0f, isHdrOutput, 32);
            Object.DestroyImmediate(volume);

            Assert.IsTrue(config.TryValidate(out var error), error);
        }

        [Test]
        public void RejectsUnsupportedLookPreset()
        {
            var config = CreateConfig();
            config.LookConfig.LookPreset = (AgXLookPreset)99;

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("look preset", error);
        }

        private static AgXConfig CreateConfig() => new()
        {
            HdrMaxNits = 1000.0f,
            SdrMaxNits = 100.0f,
            HdrPurity = 0.5f,
            HdrExtraPowerFactor = 1.0f,
            LookConfig = AgXLookConfig.GetPreset(AgXLookPreset.None),
            LutSize = 32
        };
    }
}
