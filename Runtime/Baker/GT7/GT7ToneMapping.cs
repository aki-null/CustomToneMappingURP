// GT7 Tone Mapping port for Unity
//
// Original implementation:
// Slides: https://blog.selfshadow.com/publications/s2025-shading-course/pdi/s2025_pbs_pdi_slides.pdf
// Code: https://blog.selfshadow.com/publications/s2025-shading-course/pdi/supplemental/gt7_tone_mapping.cpp
//
// MIT License
//
// Copyright (c) 2025 Polyphony Digital Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker.GT7
{
    // Convergent-shoulder tone mapping curve
    public struct GTToneMappingCurveV2
    {
        private float _peakIntensity;
        private float _alpha;
        private float _midPoint;
        private float _linearSection;
        private float _toeStrength;

        // Precomputed constants for the shoulder
        public float KA, KB, KC;

        public void Initialize(float monitorIntensity,
            float alpha,
            float grayPoint,
            float linearSection,
            float toeStrength)
        {
            _peakIntensity = monitorIntensity;
            _alpha = alpha;
            _midPoint = grayPoint;
            _linearSection = linearSection;
            _toeStrength = toeStrength;

            var k = (_linearSection - 1.0f) / (_alpha - 1.0f);
            KA = _peakIntensity * _linearSection + _peakIntensity * k;
            KB = -_peakIntensity * k * math.exp(_linearSection / k);
            KC = -1.0f / (k * _peakIntensity);
        }

        public float Evaluate(float x)
        {
            if (x < 0.0f) return 0.0f;

            var weightLinear = math.smoothstep(0.0f, _midPoint, x);
            var weightToe = 1.0f - weightLinear;

            // Shoulder mapping for highlights
            var shoulder = KA + KB * math.exp(x * KC);

            if (x < _linearSection * _peakIntensity)
            {
                var toeMapped = _midPoint * math.pow(x / _midPoint, _toeStrength);
                return weightToe * toeMapped + weightLinear * x;
            }

            return shoulder;
        }
    }

    [BurstCompile]
    public struct GT7ToneMapping : IToneMap
    {
        private GT7Config _cfg;

        // Derived values
        private float _sdrCorrectionFactor; // 1.0 for HDR, 1/FB(PaperWhite) for SDR
        private float _framebufferLuminanceTarget; // peak in FB scale
        private float _framebufferLuminanceTargetUcs; // luminance in chosen UCS
        private GTToneMappingCurveV2 _curve;

        public void InitializeAsHdr(GT7Config cfg)
        {
            _cfg = cfg;
            _sdrCorrectionFactor = 1.0f;
            InitializeParameters(cfg.TargetPeakNits);
        }

        public void InitializeAsSdr(GT7Config cfg)
        {
            _cfg = cfg;
            // 1.0 (linear) corresponds to ReferenceLuminance cd/m^2.
            // Paper white is expressed in cd/m^2. Convert to FB space then invert.
            var fbPaperWhite = PhysicalToFrameBuffer(cfg.SdrPaperWhite, cfg.ReferenceLuminance);
            _sdrCorrectionFactor = 1.0f / fbPaperWhite;
            InitializeParameters(cfg.SdrPaperWhite);
        }

        private void InitializeParameters(float physicalTargetLuminance)
        {
            _framebufferLuminanceTarget = PhysicalToFrameBuffer(physicalTargetLuminance, _cfg.ReferenceLuminance);

            _curve = default;
            _curve.Initialize(_framebufferLuminanceTarget,
                _cfg.CurveAlpha,
                _cfg.CurveMidPoint,
                _cfg.CurveLinearSection,
                _cfg.CurveToeStrength);

            // Compute UCS luminance of target
            var targetRgb = new float3(_framebufferLuminanceTarget);
            var ucs = RgbToUcs(targetRgb, _cfg);
            _framebufferLuminanceTargetUcs = ucs.x; // first component is luminance (I or Jz)
        }

        // Apply tone mapping to linear Rec.2020 RGB in FB space
        public float3 ApplyToneMap(float3 rgb)
        {
            // Convert to UCS
            var ucs = RgbToUcs(rgb, _cfg);

            // Per-channel mapping (skew)
            var skewedRgb = new float3(
                _curve.Evaluate(rgb.x),
                _curve.Evaluate(rgb.y),
                _curve.Evaluate(rgb.z)
            );

            var skewedUcs = RgbToUcs(skewedRgb, _cfg);

            var normalizedLuminance =
                _framebufferLuminanceTargetUcs > 1e-10f ? ucs.x / _framebufferLuminanceTargetUcs : 0.0f;
            var chromaScale = ChromaCurve(normalizedLuminance, _cfg.FadeStart, _cfg.FadeEnd);

            var scaledUcs = new float3(
                skewedUcs.x, // luminance from skewed color
                ucs.y * chromaScale,
                ucs.z * chromaScale
            );

            var scaledRgb = UcsToRgb(scaledUcs, _cfg);

            var blended = (1.0f - _cfg.BlendRatio) * skewedRgb + _cfg.BlendRatio * scaledRgb;
            var clamped = math.min(blended, new float3(_framebufferLuminanceTarget));

            return _sdrCorrectionFactor * clamped;
        }

        public bool IsHDROutput => _cfg.IsHdrOutput;

        public Colorspace InputColorspace => Colorspace.Rec2020;
        public Colorspace OutputColorspace => Colorspace.Rec2020;

        #region Math Helpers

        // Linear frame-buffer value to cd/m^2 (using ReferenceLuminance)
        private static float FrameBufferToPhysical(float fb, float referenceLuminance) => fb * referenceLuminance;

        // cd/m^2 to linear frame-buffer value
        private static float PhysicalToFrameBuffer(float physical, float referenceLuminance) =>
            physical / referenceLuminance;

        private static float ChromaCurve(float x, float a, float b) => 1.0f - math.smoothstep(a, b, x);

        // ST-2084 (PQ) EOTF: PQ -> linear cd/m^2 (then to FB)
        private static float EotfSt2084(float n, float exponentScaleFactor, float referenceLuminance)
        {
            n = math.clamp(n, 0.0f, 1.0f);

            const float m1 = 0.1593017578125f; // (2610/4096)/4
            var m2 = 78.84375f * exponentScaleFactor; // (2523/4096)*128
            const float c1 = 0.8359375f; // 3424/4096
            const float c2 = 18.8515625f; // (2413/4096)*32
            const float c3 = 18.6875f; // (2392/4096)*32
            const float pqC = 10000.0f;

            var np = math.pow(n, 1.0f / m2);
            var l = np - c1;
            l = math.max(l, 0.0f);
            l = l / (c2 - c3 * np);
            l = math.pow(l, 1.0f / m1);

            // Convert absolute luminance to FB scale
            return PhysicalToFrameBuffer(l * pqC, referenceLuminance);
        }

        // ST-2084 (PQ) inverse: linear cd/m^2 (from FB) -> PQ
        private static float InverseEotfSt2084(float v, float exponentScaleFactor, float referenceLuminance)
        {
            const float m1 = 0.1593017578125f;
            var m2 = 78.84375f * exponentScaleFactor;
            const float c1 = 0.8359375f;
            const float c2 = 18.8515625f;
            const float c3 = 18.6875f;
            const float pqC = 10000.0f;

            var physical = FrameBufferToPhysical(v, referenceLuminance);
            var y = physical / pqC;
            var ym = math.pow(y, m1);
            return math.exp2(m2 * (math.log2(c1 + c2 * ym) - math.log2(1.0f + c3 * ym)));
        }

        #endregion

        #region UCS Conversions

        private static float3 RgbToUcs(float3 rgb, GT7Config cfg)
        {
            switch (cfg.Ucs)
            {
                case UcsMode.ICtCp:
                    return RgbToICtCp(rgb, cfg.ReferenceLuminance);
                case UcsMode.JzAzBz:
                    return RgbToJzazbz(rgb, cfg.ReferenceLuminance, cfg.JzazbzExponentScaleFactor);
                default:
                    return RgbToICtCp(rgb, cfg.ReferenceLuminance);
            }
        }

        private static float3 UcsToRgb(float3 ucs, GT7Config cfg)
        {
            switch (cfg.Ucs)
            {
                case UcsMode.ICtCp:
                    return ICtCpToRgb(ucs, cfg.ReferenceLuminance);
                case UcsMode.JzAzBz:
                    return JzazbzToRgb(ucs, cfg.ReferenceLuminance, cfg.JzazbzExponentScaleFactor);
                default:
                    return ICtCpToRgb(ucs, cfg.ReferenceLuminance);
            }
        }

        // ICtCp (linear Rec.2020 -> ICtCp)
        private static float3 RgbToICtCp(float3 rgb, float referenceLuminance)
        {
            var l = (rgb.x * 1688.0f + rgb.y * 2146.0f + rgb.z * 262.0f) / 4096.0f;
            var m = (rgb.x * 683.0f + rgb.y * 2951.0f + rgb.z * 462.0f) / 4096.0f;
            var s = (rgb.x * 99.0f + rgb.y * 309.0f + rgb.z * 3688.0f) / 4096.0f;

            var lPQ = InverseEotfSt2084(l, 1.0f, referenceLuminance);
            var mPQ = InverseEotfSt2084(m, 1.0f, referenceLuminance);
            var sPQ = InverseEotfSt2084(s, 1.0f, referenceLuminance);

            var I = (2048.0f * lPQ + 2048.0f * mPQ) / 4096.0f;
            var Ct = (6610.0f * lPQ - 13613.0f * mPQ + 7003.0f * sPQ) / 4096.0f;
            var Cp = (17933.0f * lPQ - 17390.0f * mPQ - 543.0f * sPQ) / 4096.0f;
            return new float3(I, Ct, Cp);
        }

        private static float3 ICtCpToRgb(float3 ictcp, float referenceLuminance)
        {
            var l = ictcp.x + 0.00860904f * ictcp.y + 0.11103f * ictcp.z;
            var m = ictcp.x - 0.00860904f * ictcp.y - 0.11103f * ictcp.z;
            var s = ictcp.x + 0.560031f * ictcp.y - 0.320627f * ictcp.z;

            var lLin = EotfSt2084(l, 1.0f, referenceLuminance);
            var mLin = EotfSt2084(m, 1.0f, referenceLuminance);
            var sLin = EotfSt2084(s, 1.0f, referenceLuminance);

            var r = math.max(3.43661f * lLin - 2.50645f * mLin + 0.0698454f * sLin, 0.0f);
            var g = math.max(-0.79133f * lLin + 1.9836f * mLin - 0.192271f * sLin, 0.0f);
            var b = math.max(-0.0259499f * lLin - 0.0989137f * mLin + 1.12486f * sLin, 0.0f);
            return new float3(r, g, b);
        }

        // Jzazbz (linear Rec.2020 -> Jzazbz)
        private static float3 RgbToJzazbz(float3 rgb, float referenceLuminance, float exponentScale)
        {
            var l = rgb.x * 0.530004f + rgb.y * 0.355704f + rgb.z * 0.086090f;
            var m = rgb.x * 0.289388f + rgb.y * 0.525395f + rgb.z * 0.157481f;
            var s = rgb.x * 0.091098f + rgb.y * 0.147588f + rgb.z * 0.734234f;

            var lPQ = InverseEotfSt2084(l, exponentScale, referenceLuminance);
            var mPQ = InverseEotfSt2084(m, exponentScale, referenceLuminance);
            var sPQ = InverseEotfSt2084(s, exponentScale, referenceLuminance);

            var iz = 0.5f * lPQ + 0.5f * mPQ;
            var jz = (0.44f * iz) / (1.0f - 0.56f * iz) - 1.6295499532821566e-11f;
            var az = 3.524000f * lPQ - 4.066708f * mPQ + 0.542708f * sPQ;
            var bz = 0.199076f * lPQ + 1.096799f * mPQ - 1.295875f * sPQ;
            return new float3(jz, az, bz);
        }

        private static float3 JzazbzToRgb(float3 jab, float referenceLuminance, float exponentScale)
        {
            var jz = jab.x + 1.6295499532821566e-11f;
            var iz = jz / (0.44f + 0.56f * jz);
            var a = jab.y;
            var b = jab.z;

            var l = iz + a * 1.386050432715393e-1f + b * 5.804731615611869e-2f;
            var m = iz + a * -1.386050432715393e-1f + b * -5.804731615611869e-2f;
            var s = iz + a * -9.601924202631895e-2f + b * -8.118918960560390e-1f;

            var lLin = EotfSt2084(l, exponentScale, referenceLuminance);
            var mLin = EotfSt2084(m, exponentScale, referenceLuminance);
            var sLin = EotfSt2084(s, exponentScale, referenceLuminance);

            var r = lLin * 2.990669f + mLin * -2.049742f + sLin * 0.088977f;
            var g = lLin * -1.634525f + mLin * 3.145627f + sLin * -0.483037f;
            var b2 = lLin * -0.042505f + mLin * -0.377983f + sLin * 1.448019f;
            return new float3(r, g, b2);
        }

        #endregion
    }
}