using Unity.Mathematics;

namespace CustomToneMapping.Tests
{
    /// <summary>
    /// Shared test helper utilities for tone mapping tests
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Compares two float3 vectors with tolerance.
        /// Uses relative tolerance for values > threshold, absolute tolerance otherwise.
        /// </summary>
        /// <param name="expected">Expected vector value</param>
        /// <param name="actual">Actual vector value</param>
        /// <param name="tolerance">Tolerance value (relative for large values, absolute for small)</param>
        /// <param name="absoluteThreshold">Threshold below which absolute tolerance is used (default 1e-3)</param>
        /// <returns>True if vectors are equal within tolerance</returns>
        public static bool AreVectorsEqual(float3 expected, float3 actual, float tolerance,
            float absoluteThreshold = 1e-3f)
        {
            for (var i = 0; i < 3; i++)
            {
                var exp = expected[i];
                var act = actual[i];

                if (math.abs(exp) > absoluteThreshold)
                {
                    // Use relative tolerance for values above threshold
                    var relativeError = math.abs((act - exp) / exp);
                    if (relativeError >= tolerance) return false;
                }
                else
                {
                    // Use absolute tolerance for values at or below threshold
                    if (math.abs(act - exp) >= tolerance) return false;
                }
            }

            return true;
        }
    }
}