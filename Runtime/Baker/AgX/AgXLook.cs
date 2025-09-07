using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker.AgX
{
    [BurstCompile]
    public static class AgXLook
    {
        // Punchy Shadows: { rgb:[0.2,0.2,0.2], master:0.35, start:0.4, pivot:0.1 }

        // Knots from start=0.4, pivot=0.1
        private const float X0 = 0.1f; // pivot
        private const float X1 = 0.25f; // midpoint
        private const float X2 = 0.4f; // start
        private const float Dx10 = 0.15f;
        private const float Dx21 = 0.15f;
        private const float InvDx10 = 1f / Dx10; // 6.6666665
        private const float InvDx21 = 1f / Dx21; // 6.6666665

        // RGB shadows (val=0.2)
        private const float Y0RGB = X0;
        private const float Y1RGB = 0.22f;
        private const float Y2RGB = X2;
        private const float M0RGB = 0.2f;
        private const float M2RGB = 1.0f;
        private const float CLRGB = M0RGB * Dx10; // 0.03
        private const float CRRGB = M2RGB * Dx21; // 0.15

        // Master shadows (val=0.35)
        private const float Y0M = X0;
        private const float Y1M = 0.225625f;
        private const float Y2M = X2;
        private const float M0M = 0.35f;
        private const float M2M = 1.0f;
        private const float CLM = M0M * Dx10; // 0.0525
        private const float CRM = M2M * Dx21; // 0.15

        /*
        OpenColorIO
        ===

        Copyright Contributors to the OpenColorIO Project.

        Redistribution and use in source and binary forms, with or without
        modification, are permitted provided that the following conditions are
        met:

        * Redistributions of source code must retain the above copyright
          notice, this list of conditions and the following disclaimer.
        * Redistributions in binary form must reproduce the above copyright
          notice, this list of conditions and the following disclaimer in the
          documentation and/or other materials provided with the distribution.
        * Neither the name of the copyright holder nor the names of its
          contributors may be used to endorse or promote products derived from
          this software without specific prior written permission.

        THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
        "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
        LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
        A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
        HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
        SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
        LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
        DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
        THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
        (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
        OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
        */

        private static float3 OcioShadows(float3 t, float y0, float y1, float y2, float m0, float m2, float cL,
            float cR)
        {
            // Piecewise cubic on [X0..X2] with linear extensions (OCIO ComputeHSFwd)
            var tL = (t - X0) * InvDx10;
            var tR = (t - X1) * InvDx21;

            var tL2 = tL * tL;
            var one = new float3(1f);
            var fL = y0 * (one - tL2) + y1 * tL2 + cL * (one - tL) * tL;

            var tR1 = one - tR;
            var fR = y1 * (tR1 * tR1) + y2 * (2f - tR) * tR + cR * (tR - 1f) * tR;

            // res = (t < X1) ? fL : fR
            var c1 = t < X1;
            var res = math.select(fR, fL, c1);

            // Left linear (t < X0): y0 + (t - X0)*m0
            var r0 = y0 + (t - X0) * m0;
            var c0 = t < X0;
            res = math.select(res, r0, c0);

            // Right linear (t >= X2): y2 + (t - X2)*m2
            var r2 = y2 + (t - X2) * m2;
            var c2 = t < X2; // keep res if t < X2, else r2
            res = math.select(r2, res, c2);

            return res;
        }

        private static float3 OcioPunchyShadows(float3 rgb)
        {
            var outRGB = OcioShadows(rgb, Y0RGB, Y1RGB, Y2RGB, M0RGB, M2RGB, CLRGB, CRRGB);
            outRGB = OcioShadows(outRGB, Y0M, Y1M, Y2M, M0M, M2M, CLM, CRM);
            return outRGB;
        }

        private static float3 OcioCdlPower(float3 rgb, float pow)
        {
            var powv = math.pow(rgb, pow);

            var isNeg = rgb < 0f;
            var res = math.select(powv, rgb, isNeg);

            var isNaN = math.isnan(res);
            res = math.select(res, 0f, isNaN);

            return res;
        }

        public static float3 Apply(float3 rgb, AgXLookConfig config)
        {
            if (config is { LookPreset: AgXLookPreset.Punchy, Intensity: > 0f })
            {
                // The log space we use here is based on the AgX look profile in OCIO
                var agxLog = ColorUtility.Log2Encode(rgb, -12.47393f, 12.5260688117f);
                agxLog = math.saturate(agxLog);

                var previousAgxLog = agxLog;

                agxLog = OcioPunchyShadows(agxLog);
                agxLog = OcioCdlPower(agxLog, 1.0912f);

                agxLog = math.lerp(previousAgxLog, agxLog, config.Intensity);

                rgb = ColorUtility.Log2Decode(agxLog, -12.47393f, 12.5260688117f);
            }

            return rgb;
        }
    }
}