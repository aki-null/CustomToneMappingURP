// Unity implementation of AgX
// Based on https://github.com/EaryChow/AgX_LUT_Gen/blob/main/AgXHLG.py
//
// AgX is developed by Troy James Sobotka.
// This implementation of AgX is based on the EaryChow's version.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker.AgX
{
    [BurstCompile]
    public struct AgXToneMapping : IToneMap
    {
        private AgXConfig _cfg;
        private float _hdrSdrRatio;

        public void Initialize(AgXConfig cfg)
        {
            _cfg = cfg;
            _hdrSdrRatio = cfg.HdrMaxNits / cfg.SdrMaxNits;

            if (_hdrSdrRatio < 1f)
            {
                _cfg.IsHdrOutput = false;
            }
        }

        public float3 ApplyToneMap(float3 rgb)
        {
            rgb = ApplyLuminanceCompensation(rgb);

            rgb = math.mul(AgXConstants.InsetMatrix, rgb);

            rgb = AgXLook.Apply(rgb, _cfg.LookConfig);

            var preFormHsv = ColorUtility.RgbToHsv(rgb);

            var logRgb = ColorUtility.Log2Encode(rgb, AgXConstants.Log2Minimum, AgXConstants.Log2Maximum,
                AgXConstants.MidGrey);
            logRgb = math.saturate(logRgb);

            var shoulderPower = 1.0f;
            if (_cfg.IsHdrOutput)
            {
                shoulderPower = math.pow(_hdrSdrRatio, math.log10(_cfg.HdrExtraPowerFactor));
            }

            logRgb = ApplySigmoid(logRgb, shoulderPower);

            // Linearize
            var linear = ColorUtility.SafePower(logRgb, 2.4f);

            if (_cfg.IsHdrOutput)
            {
                linear = DarkenMiddleGrey(linear);
            }

            var postFormHsv = ColorUtility.RgbToHsv(linear);
            postFormHsv.x = LerpChromaticityAngle(preFormHsv.x, postFormHsv.x, AgXConstants.ChromaMixPercent / 100f);
            linear = ColorUtility.HsvToRgb(postFormHsv);

            rgb = math.mul(AgXConstants.OutsetMatrix, linear);

            if (_cfg.UseP3Limit)
            {
                rgb = ApplyP3Limit(rgb);
            }

            if (_cfg.IsHdrOutput)
            {
                // Scale to compensate for HDR darkening and preserve SDR brightness
                rgb *= _hdrSdrRatio;
                // Clamp at HDR maximum (10.0 for 1000 nits)
                return math.clamp(rgb, 0f, _hdrSdrRatio);
            }

            return math.saturate(rgb);
        }

        public bool IsHDROutput => _cfg.IsHdrOutput;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyLuminanceCompensation(float3 rgb)
        {
            var y = math.dot(rgb, AgXConstants.Rec2020LuminanceCoeffs);
            var maxRgb = math.max(rgb.x, math.max(rgb.y, rgb.z));
            var inverseRgb = maxRgb - rgb;
            var maxInverse = math.max(inverseRgb.x, math.max(inverseRgb.y, inverseRgb.z));
            var yInverseRGB = math.dot(inverseRgb, AgXConstants.Rec2020LuminanceCoeffs);
            var yCompensateNegative = maxInverse - yInverseRGB + y;

            var minRgb = math.min(rgb.x, math.min(rgb.y, rgb.z));
            var offset = math.max(-minRgb, 0.0f);
            var rgbOffset = rgb + offset;

            var maxRgbOffset = math.max(rgbOffset.x, math.max(rgbOffset.y, rgbOffset.z));
            var inverseRgbOffset = maxRgbOffset - rgbOffset;
            var maxInverseOffset = math.max(inverseRgbOffset.x, math.max(inverseRgbOffset.y, inverseRgbOffset.z));
            var yInverseRGBOffset = math.dot(inverseRgbOffset, AgXConstants.Rec2020LuminanceCoeffs);
            var yNew = math.dot(rgbOffset, AgXConstants.Rec2020LuminanceCoeffs);
            yNew = maxInverseOffset - yInverseRGBOffset + yNew;

            var luminanceRatio = yNew > yCompensateNegative ? yCompensateNegative / yNew : 1.0f;

            return luminanceRatio * rgbOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplySigmoid(float3 x, float hdrShoulderPower)
        {
            var pivots = new float2(AgXConstants.FulcrumInput, AgXConstants.FulcrumOutput);
            var slope = AgXConstants.CalculatedSlope;
            var powers = new float2(AgXConstants.ExponentToe, AgXConstants.ExponentShoulder * hdrShoulderPower);
            return Sigmoid.CalculateSigmoid(x, pivots, slope, powers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 DarkenMiddleGrey(float3 col)
        {
            var preHsv = ColorUtility.RgbToHsv(col);

            const float minExp = -20f;
            var maxExp = math.log2(1f / 0.18f);
            var tempLog = ColorUtility.Log2Encode(col, minExp, maxExp, AgXConstants.MidGrey);

            var originalMiddleGrey = ColorUtility.Log2Encode(0.18f, minExp, maxExp, AgXConstants.MidGrey);
            var darkenedMiddleGrey =
                ColorUtility.Log2Encode(0.18f / _hdrSdrRatio, minExp, maxExp, AgXConstants.MidGrey);

            var darkened = Sigmoid.CalculateSigmoid(tempLog, new float2(originalMiddleGrey, darkenedMiddleGrey),
                1.000001f, new float2(3f, 1f));

            var darkenedLinear = ColorUtility.Log2Decode(darkened, minExp, maxExp, AgXConstants.MidGrey);

            var postHsv = ColorUtility.RgbToHsv(darkenedLinear);
            postHsv.x = LerpChromaticityAngle(preHsv.x, postHsv.x, _cfg.HdrPurity);
            postHsv.y = math.lerp(preHsv.y, postHsv.y, _cfg.HdrPurity);

            return ColorUtility.HsvToRgb(postHsv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApplyP3Limit(float3 rgb)
        {
            rgb = math.mul(AgXConstants.Rec2020ToXyz, rgb);
            rgb = math.mul(AgXConstants.XyzToP3, rgb);
            rgb = ApplyLuminanceCompensation(rgb);
            rgb = math.mul(AgXConstants.P3ToXyz, rgb);
            rgb = math.mul(AgXConstants.XyzToRec2020, rgb);
            return rgb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LerpChromaticityAngle(float h1, float h2, float t)
        {
            var delta = h2 - h1;
            if (delta > 0.5f) h2 -= 1f;
            else if (delta < -0.5f) h2 += 1f;

            var result = h1 + t * (h2 - h1);
            return result - math.floor(result); // Wrap to [0,1]
        }

        public Colorspace InputColorspace => Colorspace.Rec2020;
        public Colorspace OutputColorspace => Colorspace.Rec2020;
    }
}