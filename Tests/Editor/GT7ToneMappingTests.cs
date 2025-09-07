using NUnit.Framework;
using Unity.Mathematics;
using CustomToneMapping.Baker.GT7;
using System.Collections.Generic;

namespace CustomToneMapping.Tests
{
    /// <summary>
    /// Tests based on reference implementation result
    /// </summary>
    [TestFixture]
    public class GT7ToneMappingTests : ToneMappingTestBase<GT7Config, GT7ReferenceData.TestCase, GT7ToneMapping>
    {
        protected override GT7Config CreateConfig(string configName, bool isHdr, float targetPeakNits)
        {
            // Extract UCS mode from config name
            var ucsMode = configName.Contains("JzAzBz") ? UcsMode.JzAzBz : UcsMode.ICtCp;

            return new GT7Config
            {
                TargetPeakNits = targetPeakNits,
                IsHdrOutput = isHdr,
                ReferenceLuminance = 100.0f,
                SdrPaperWhite = 250.0f,
                Ucs = ucsMode,
                JzazbzExponentScaleFactor = 1.7f,
                CurveAlpha = 0.25f,
                CurveMidPoint = 0.538f,
                CurveLinearSection = 0.444f,
                CurveToeStrength = 1.280f,
                BlendRatio = 0.6f,
                FadeStart = 0.98f,
                FadeEnd = 1.16f,
            };
        }

        protected override float GetToleranceForTestCase(GT7ReferenceData.TestCase testCase)
        {
            // Use differentiated tolerance based on color space complexity
            if (testCase.ConfigName.Contains("ICtCp"))
            {
                return 1e-3f; // 0.1% tolerance for ICtCp (higher numerical complexity)
            }

            return 5e-4f; // 0.05% tolerance for JzAzBz
        }

        protected override IEnumerable<GT7ReferenceData.TestCase> GetAllTestCases() => GT7ReferenceData.TestCases;
        protected override string GetConfigName(GT7ReferenceData.TestCase testCase) => testCase.ConfigName;
        protected override string GetDescription(GT7ReferenceData.TestCase testCase) => testCase.Description;
        protected override float3 GetInput(GT7ReferenceData.TestCase testCase) => testCase.Input;
        protected override float3 GetExpected(GT7ReferenceData.TestCase testCase) => testCase.Expected;

        protected override GT7ToneMapping CreateAndInitializeMapper(GT7Config config, bool isHdr)
        {
            var mapper = new GT7ToneMapping();
            if (isHdr)
            {
                mapper.InitializeAsHdr(config);
            }
            else
            {
                mapper.InitializeAsSdr(config);
            }

            return mapper;
        }

        protected override float3 ApplyToneMap(GT7ToneMapping mapping, float3 input) => mapping.ApplyToneMap(input);

        [Test]
        public void TestAgainstReferenceData_SDR_ICtCp()
        {
            RunTestForConfiguration("SDR_ICtCp", "SDR_ICtCp", false, 250.0f);
        }

        [Test]
        public void TestAgainstReferenceData_SDR_JzAzBz()
        {
            RunTestForConfiguration("SDR_JzAzBz", "SDR_JzAzBz", false, 250.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR1000_ICtCp()
        {
            RunTestForConfiguration("HDR1000_ICtCp", "HDR1000_ICtCp", true, 1000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR1000_JzAzBz()
        {
            RunTestForConfiguration("HDR1000_JzAzBz", "HDR1000_JzAzBz", true, 1000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR4000_ICtCp()
        {
            RunTestForConfiguration("HDR4000_ICtCp", "HDR4000_ICtCp", true, 4000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR4000_JzAzBz()
        {
            RunTestForConfiguration("HDR4000_JzAzBz", "HDR4000_JzAzBz", true, 4000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR10000_ICtCp()
        {
            RunTestForConfiguration("HDR10000_ICtCp", "HDR10000_ICtCp", true, 10000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR10000_JzAzBz()
        {
            RunTestForConfiguration("HDR10000_JzAzBz", "HDR10000_JzAzBz", true, 10000.0f);
        }
    }
}