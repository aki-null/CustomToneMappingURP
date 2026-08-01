using NUnit.Framework;
using Unity.Mathematics;

namespace CustomToneMapping.Tests
{
    public class TestHelpersTests
    {
        [Test]
        public void VectorComparisonRejectsNonFiniteValues()
        {
            Assert.IsFalse(TestHelpers.AreVectorsEqual(
                new float3(float.NaN, 0.0f, 0.0f),
                new float3(float.NaN, 0.0f, 0.0f),
                1e-4f));
        }
    }
}
