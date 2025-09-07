using NUnit.Framework;
using Unity.Mathematics;
using System.Linq;
using System.Collections.Generic;

namespace CustomToneMapping.Tests
{
    /// <summary>
    /// Base class for tone mapping tests with shared implementation patterns
    /// </summary>
    public abstract class ToneMappingTestBase<TConfig, TTestCase, TMapper>
        where TConfig : struct
        where TMapper : struct
    {
        /// <summary>
        /// Creates a configuration for the specified test scenario
        /// </summary>
        protected abstract TConfig CreateConfig(string configName, bool isHdr, float targetPeakNits);

        /// <summary>
        /// Gets the tolerance for a specific test case
        /// </summary>
        protected abstract float GetToleranceForTestCase(TTestCase testCase);

        /// <summary>
        /// Gets all test cases from the reference data
        /// </summary>
        protected abstract IEnumerable<TTestCase> GetAllTestCases();

        /// <summary>
        /// Gets the config name from a test case
        /// </summary>
        protected abstract string GetConfigName(TTestCase testCase);

        /// <summary>
        /// Gets the description from a test case
        /// </summary>
        protected abstract string GetDescription(TTestCase testCase);

        /// <summary>
        /// Gets the input value from a test case
        /// </summary>
        protected abstract float3 GetInput(TTestCase testCase);

        /// <summary>
        /// Gets the expected output from a test case
        /// </summary>
        protected abstract float3 GetExpected(TTestCase testCase);

        /// <summary>
        /// Creates and initializes a mapper with the given configuration
        /// </summary>
        protected abstract TMapper CreateAndInitializeMapper(TConfig config, bool isHdr);

        /// <summary>
        /// Applies the tone mapping to an input value
        /// </summary>
        protected abstract float3 ApplyToneMap(TMapper mapper, float3 input);

        /// <summary>
        /// Runs tests for a specific configuration
        /// </summary>
        protected void RunTestForConfiguration(string configName, string filterName, bool isHdr, float peakNits)
        {
            var config = CreateConfig(configName, isHdr, peakNits);
            var mapper = CreateAndInitializeMapper(config, isHdr);

            var testCases = GetAllTestCases()
                .Where(tc => GetConfigName(tc) == filterName)
                .ToList();

            Assert.That(testCases.Count, Is.GreaterThan(0), $"Should have {filterName} test cases");

            foreach (var testCase in testCases)
            {
                var input = GetInput(testCase);
                var expected = GetExpected(testCase);
                var result = ApplyToneMap(mapper, input);
                var tolerance = GetToleranceForTestCase(testCase);

                Assert.That(TestHelpers.AreVectorsEqual(expected, result, tolerance), Is.True,
                    $"Test case '{GetDescription(testCase)}' failed:\n" +
                    $"  Expected: ({expected.x:F6}, {expected.y:F6}, {expected.z:F6})\n" +
                    $"  Actual: ({result.x:F6}, {result.y:F6}, {result.z:F6})");
            }
        }
    }
}