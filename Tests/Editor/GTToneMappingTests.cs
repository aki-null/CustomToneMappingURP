using NUnit.Framework;
using Unity.Mathematics;
using CustomToneMapping.Baker.GT;
using System.Collections.Generic;

namespace CustomToneMapping.Tests
{
    /// <summary>
    /// Unit tests for GT Tone Mapping implementation
    /// 
    /// Tests against reference data computed from:
    /// https://www.desmos.com/calculator/gslcdxvipg
    /// </summary>
    [TestFixture]
    public class GTToneMappingTests : ToneMappingTestBase<GTConfig, GTReferenceData.TestCase, GTToneMapping>
    {
        protected override GTConfig CreateConfig(string configName, bool isHdr, float targetPeakNits)
        {
            return new GTConfig
            {
                TargetPeakNits = targetPeakNits,
                IsHdrOutput = isHdr,
                ReferenceLuminance = 100.0f,
                SdrPaperWhite = 100.0f,

                // Default curve parameters
                Contrast = 1.0f,
                LinearSectionStart = 0.22f,
                LinearSectionLength = 0.4f,
                BlackTightness = 1.33f,
                BlackOffset = 0.0f
            };
        }

        protected override float GetToleranceForTestCase(GTReferenceData.TestCase testCase)
        {
            return 1e-4f;
        }

        protected override IEnumerable<GTReferenceData.TestCase> GetAllTestCases() => GTReferenceData.TestCases;

        protected override string GetConfigName(GTReferenceData.TestCase testCase) => testCase.ConfigName;
        protected override string GetDescription(GTReferenceData.TestCase testCase) => testCase.Description;
        protected override float3 GetInput(GTReferenceData.TestCase testCase) => testCase.Input;
        protected override float3 GetExpected(GTReferenceData.TestCase testCase) => testCase.Expected;

        protected override GTToneMapping CreateAndInitializeMapper(GTConfig config, bool isHdr)
        {
            var mapper = new GTToneMapping();
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

        protected override float3 ApplyToneMap(GTToneMapping mapper, float3 input) => mapper.ApplyToneMap(input);

        [Test]
        public void TestAgainstReferenceData_SDR()
        {
            RunTestForConfiguration("SDR", "SDR", false, 250.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR1000()
        {
            RunTestForConfiguration("HDR1000", "HDR1000", true, 1000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR4000()
        {
            RunTestForConfiguration("HDR4000", "HDR4000", true, 4000.0f);
        }

        [Test]
        public void TestAgainstReferenceData_HDR10000()
        {
            RunTestForConfiguration("HDR10000", "HDR10000", true, 10000.0f);
        }
    }
}