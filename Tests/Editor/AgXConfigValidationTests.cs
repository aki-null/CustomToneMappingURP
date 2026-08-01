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

        [Test]
        public void VolumeCanProduceInvalidHdrRange()
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.AgXToneMapping>();
            volume.maxNits.value = 100.0f;
            volume.sdrPaperWhite.value = 1000.0f;

            var config = volume.ToConfig(volume.maxNits.value, true, 32);
            Object.DestroyImmediate(volume);

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("at least", error);
        }

        [Test]
        public void AllowsVolumeHdrRangeBelowSdrRangeForSdrOutput()
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.AgXToneMapping>();
            volume.maxNits.value = 100.0f;
            volume.sdrPaperWhite.value = 1000.0f;

            var config = volume.ToConfig(volume.maxNits.value, false, 32);
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
