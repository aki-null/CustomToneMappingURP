// Port of Troy Sobotka's tunable sigmoid function
// https://github.com/sobotka/SB2383-Configuration-Generation

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker.AgX
{
    [BurstCompile]
    public static class Sigmoid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LinearBreakpoint(float numerator, float slope, float coordinate)
        {
            var denominator = math.sqrt(slope * slope + 1.0f);
            return numerator / denominator + coordinate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 Line(float3 x, float slope, float intercept)
        {
            return slope * x + intercept;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Scale(float limitX, float limitY, float transitionX, float transitionY, float power,
            float slope)
        {
            var termA = math.pow(slope * (limitX - transitionX), -power);
            var termB = math.pow(slope * (limitX - transitionX) / (limitY - transitionY), power) - 1.0f;
            return math.pow(termA * termB, -1.0f / power);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 Exponential(float3 x, float power)
        {
            return x / math.pow(1.0f + math.pow(x, power), 1.0f / power);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ExponentialCurve(float3 x, float scale, float slope, float power, float transitionX,
            float transitionY)
        {
            return scale * Exponential(slope * (x - transitionX) / scale, power) + transitionY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CalculateSigmoid(float3 x, float2 pivots, float slope, float2 powers)
        {
            var lengths = new float2(0.0f, 0.0f);
            var limits = new float4(0.0f, 0.0f, 1.0f, 1.0f);

            var transitionToeX = LinearBreakpoint(-lengths.x, slope, pivots.x);
            var transitionToeY = LinearBreakpoint(slope * -lengths.x, slope, pivots.y);

            var transitionShoulderX = LinearBreakpoint(lengths.y, slope, pivots.x);
            var transitionShoulderY = LinearBreakpoint(slope * lengths.y, slope, pivots.y);

            var inverseTransitionToeX = 1.0f - transitionToeX;
            var inverseTransitionToeY = 1.0f - transitionToeY;
            var inverseLimitToeX = 1.0f - limits.x;
            var inverseLimitToeY = 1.0f - limits.y;

            var scaleToe = -Scale(inverseLimitToeX, inverseLimitToeY, inverseTransitionToeX, inverseTransitionToeY,
                powers.x, slope);
            var scaleShoulder = Scale(limits.z, limits.w, transitionShoulderX, transitionShoulderY, powers.y, slope);

            var intercept = transitionToeY - slope * transitionToeX;

            var lessThanToe = x < transitionToeX;
            var lessThanShoulder = x <= transitionShoulderX;

            var toeResult = ExponentialCurve(x, scaleToe, slope, powers.x, transitionToeX, transitionToeY);
            var linearResult = Line(x, slope, intercept);
            var shoulderResult = ExponentialCurve(x, scaleShoulder, slope, powers.y, transitionShoulderX,
                transitionShoulderY);

            return math.select(
                math.select(shoulderResult, linearResult, lessThanShoulder),
                toeResult,
                lessThanToe
            );
        }
    }
}