using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker.GT
{
    /// <summary>
    /// GT Tone Mapping
    /// Based on the curve specifications from Hajime UCHIMURA @ Polyphony Digital Inc.
    /// https://www.desmos.com/calculator/gslcdxvipg
    /// </summary>
    [BurstCompile]
    public struct GTToneMapping : IToneMap
    {
        private GTConfig _config;

        // Pre-computed curve parameters
        private float _l0; // Linear length after scale
        private float _S0, _S1; // Shoulder region bounds  
        private float _C2; // Shoulder curve parameter
        private float _sdrCorrectionFactor;
        private float _framebufferTarget;

        public void InitializeAsHdr(GTConfig config)
        {
            _config = config;
            _sdrCorrectionFactor = 1.0f;
            _framebufferTarget = config.TargetPeakNits / config.ReferenceLuminance;
            InitializeCurveParameters();
        }

        public void InitializeAsSdr(GTConfig config)
        {
            _config = config;
            var fbPaperWhite = config.SdrPaperWhite / config.ReferenceLuminance;
            _sdrCorrectionFactor = 1.0f / fbPaperWhite;
            _framebufferTarget = config.SdrPaperWhite / config.ReferenceLuminance;
            InitializeCurveParameters();
        }

        private void InitializeCurveParameters()
        {
            var P = _framebufferTarget;
            var a = _config.Contrast;
            var m = _config.LinearSectionStart;
            var l = _config.LinearSectionLength;

            // Pre-compute curve parameters
            _l0 = (P - m) * l / a;
            _S0 = m + _l0;
            _S1 = m + a * _l0;
            _C2 = (a * P) / (P - _S1);
        }

        public float3 ApplyToneMap(float3 rgb)
        {
            var result = ApplyGtCurve(rgb);

            // Apply SDR correction if needed
            result *= _sdrCorrectionFactor;

            // Clamp to target for SDR
            if (!IsHDROutput)
            {
                result = math.min(result, 1.0f);
            }

            return result;
        }

        private float3 ApplyGtCurve(float3 x)
        {
            var m = _config.LinearSectionStart;
            var c = _config.BlackTightness;
            var b = _config.BlackOffset;
            var P = _framebufferTarget;
            var a = _config.Contrast;

            var m3 = new float3(m);
            var b3 = new float3(b);

            // Clamp negative values
            x = math.max(0f, x);

            // Weights
            var w0 = 1.0f - math.smoothstep(0f, m, x); // toe region
            var w2Mask = x > (m + _l0); // shoulder region
            var w2 = math.select(0f, 1f, w2Mask);
            var w1 = 1.0f - w0 - w2; // linear region

            // Toe: T(x) = m * (x/m)^c + b
            var powTerm = math.pow(x / m3, c);
            var toeVal = m3 * powTerm + b3;
            var toe = math.select(toeVal, b3, x < 1e-10f);

            // Linear function: L(x) = m + a(x - m)
            var linear = m + a * (x - m);

            // Shoulder function: S(x) = P - (P - S1) * e^(-C2 * (x - S0) / P)
            var shoulder = P - (P - _S1) * math.exp(-_C2 * (x - _S0) / P);

            // Weighted combination
            return toe * w0 + linear * w1 + shoulder * w2;
        }

        public bool IsHDROutput => _config.IsHdrOutput;
        public Colorspace InputColorspace => Colorspace.Rec2020;
        public Colorspace OutputColorspace => Colorspace.Rec2020;
    }
}