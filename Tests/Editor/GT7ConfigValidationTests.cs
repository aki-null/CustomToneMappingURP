using CustomToneMapping.Baker.GT7;
using NUnit.Framework;
using UnityEngine;

namespace CustomToneMapping.Tests
{
    public class GT7ConfigValidationTests
    {
        [Test]
        public void ValidConfigPassesValidation()
        {
            Assert.IsTrue(CreateConfig().TryValidate(out var error), error);
        }

        [Test]
        public void VolumeCanProduceNonIncreasingFadeRange()
        {
            var volume = ScriptableObject.CreateInstance<CustomToneMapping.URP.GT7ToneMapping>();
            volume.fadeStart.value = 2.0f;
            volume.fadeEnd.value = 0.0f;

            var config = volume.ToConfig(volume.targetPeakNits.value, false, 32);
            Object.DestroyImmediate(volume);

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("fade range", error);
        }

        [Test]
        public void RejectsUnsupportedUcsMode()
        {
            var config = CreateConfig();
            config.Ucs = (UcsMode)99;

            Assert.IsFalse(config.TryValidate(out var error));
            StringAssert.Contains("UCS", error);
        }

        private static GT7Config CreateConfig() => new()
        {
            TargetPeakNits = 1000.0f,
            IsHdrOutput = false,
            ReferenceLuminance = 203.0f,
            SdrPaperWhite = 100.0f,
            Ucs = UcsMode.ICtCp,
            JzazbzExponentScaleFactor = 1.0f,
            CurveAlpha = 0.5f,
            CurveMidPoint = 0.5f,
            CurveLinearSection = 0.18f,
            CurveToeStrength = 0.2f,
            BlendRatio = 0.5f,
            FadeStart = 0.1f,
            FadeEnd = 0.9f,
            LutSize = 32
        };
    }
}
