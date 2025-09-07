using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;

namespace CustomToneMapping.Baker
{
    [BurstCompile]
    public static class ColorUtility
    {
        private static readonly float3x3 Rec709ToRec2020Matrix = new(
            0.627402f, 0.329292f, 0.043306f,
            0.069095f, 0.919544f, 0.011361f,
            0.016394f, 0.088028f, 0.895578f
        );

        private static readonly float3x3 Rec2020ToRec709Matrix = new(
            1.660496f, -0.587656f, -0.072840f,
            -0.124547f, 1.132900f, -0.008353f,
            -0.018154f, -0.100597f, 1.118751f
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Rec709ToRec2020(float3 rgb709) => math.mul(Rec709ToRec2020Matrix, rgb709);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Rec2020ToRec709(float3 rgb2020) => math.mul(Rec2020ToRec709Matrix, rgb2020);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SafePower(float3 a, float b)
        {
            return math.sign(a) * math.pow(math.abs(a), b);
        }

        /*
        Colour
        ===
        Copyright 2013 Colour Developers

        Redistribution and use in source and binary forms, with or without
        modification, are permitted provided that the following conditions are
        met:

        1. Redistributions of source code must retain the above copyright
          notice, this list of conditions and the following disclaimer.
        2. Redistributions in binary form must reproduce the above copyright
          notice, this list of conditions and the following disclaimer in the
          documentation and/or other materials provided with the distribution.
        3. Neither the name of the copyright holder nor the names of its
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

        // https://colour.readthedocs.io/en/latest/_modules/colour/models/rgb/transfer_functions/log.html#log_encoding_Log2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Log2Encode(float3 linear, float minExp, float maxExp, float midGrey = 1.0f)
        {
            linear = math.max(linear, 1.17549435e-38f);
            var logValue = math.log2(linear / midGrey);
            return (logValue - minExp) / (maxExp - minExp);
        }

        // https://colour.readthedocs.io/en/latest/_modules/colour/models/rgb/transfer_functions/log.html#log_encoding_Log2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log2Encode(float linear, float minExp, float maxExp, float midGrey = 1.0f)
        {
            linear = math.max(linear, 1.17549435e-38f);
            var logValue = math.log2(linear / midGrey);
            return (logValue - minExp) / (maxExp - minExp);
        }

        // https://colour.readthedocs.io/en/latest/_modules/colour/models/rgb/transfer_functions/log.html#log_decoding_Log2
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Log2Decode(float3 encoded, float minExp, float maxExp, float midGrey = 1.0f)
        {
            var logValue = encoded * (maxExp - minExp) + minExp;
            return midGrey * math.exp2(logValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeDiv(float num, float den)
        {
            return den != 0f ? num / den : 0f;
        }

        // https://colour.readthedocs.io/en/latest/_modules/colour/models/rgb/cylindrical.html#RGB_to_HSV
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 RgbToHsv(float3 rgb)
        {
            var (r, g, b) = (rgb.x, rgb.y, rgb.z);

            var max = math.cmax(rgb);
            var min = math.cmin(rgb);
            var delta = max - min;

            var v = max;
            var s = SafeDiv(delta, max);

            var deltaR = SafeDiv((max - r) * (1f / 6f) + delta * 0.5f, delta);
            var deltaG = SafeDiv((max - g) * (1f / 6f) + delta * 0.5f, delta);
            var deltaB = SafeDiv((max - b) * (1f / 6f) + delta * 0.5f, delta);

            var h = deltaB - deltaG;
            if (max == g) h = (1f / 3f) + deltaR - deltaB;
            if (max == b) h = (2f / 3f) + deltaG - deltaR;

            if (h < 0f) h += 1f;
            if (h > 1f) h -= 1f;

            if (delta == 0f) h = 0f;

            return new float3(h, s, v);
        }

        // https://colour.readthedocs.io/en/latest/_modules/colour/models/rgb/cylindrical.html#HSV_to_RGB
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 HsvToRgb(float3 hsv)
        {
            hsv = math.saturate(hsv);

            var (h, s, v) = (hsv.x, hsv.y, hsv.z);

            var h6 = math.frac(h) * 6f;
            var i = math.floor(h6);
            var f = h6 - i;

            var j = v * (1f - s);
            var k = v * (1f - s * f);
            var l = v * (1f - s * (1f - f));

            var sector = (int)i;

            var rgb = new float3(v, l, j);
            rgb = math.select(rgb, new float3(k, v, j), sector == 1);
            rgb = math.select(rgb, new float3(j, v, l), sector == 2);
            rgb = math.select(rgb, new float3(j, k, v), sector == 3);
            rgb = math.select(rgb, new float3(l, j, v), sector == 4);
            rgb = math.select(rgb, new float3(v, j, k), sector == 5);

            return math.saturate(rgb);
        }
    }
}