// SPDX-License-Identifier: Apache-2.0
// Copyright Contributors to the ACES Project.
// The atlas texels. The sizes and BuildConstants are generated into Aces2Atlas.Generated.cs by
// Tools/ACES2~/port-shader.cjs.
using Unity.Collections;
using Unity.Mathematics;
using static CustomToneMapping.Baker.ACES2.AcademyTransform;

namespace CustomToneMapping.Baker.ACES2
{
    // The RGBA32F atlas that Aces2.hlsl reads. Every value comes from the reference port.
    // Row 0:        for each uniform hue cell x (hue x * 360 / HueSamples onwards), its first piece and the at most two
    //               breakpoints inside the cell (NoBreakpoint when absent).
    // Row 1:        reach M and the chroma-compression norm at hue x * 360 / HueSamples, interpolated by the shader.
    // Row 2:        the tonescale and chroma compression's powers of the input J, over the input achromatic response
    //               A (J is a power of A, and the shader needs J for nothing else): tonemapped J,
    //               pow(nJ, model_gamma_inv), pow(tonemapped J / J, model_gamma_inv) and nJ. The shader interpolates
    //               these instead of computing seven powers per texel.
    // Rows 3 and 4: init_HueDependentGamutParams' cusp J, cusp M and upper-hull gamma, which are linear in hue
    //               between breakpoints: the CTL's hue-table entries and the window steps of its narrowed search. One
    //               column per piece: row 3 (start hue, cusp J, its slope, cusp M), row 4 (cusp M slope, gamma, its
    //               slope). The shader evaluates the piece instead of searching the hue table, which reproduces the
    //               CTL's steps and kinks exactly.
    // The display constants are not texels: BuildConstants writes the shader's uniform array.
    internal static partial class Aces2Atlas
    {
        private const float NoBreakpoint = 1e30f; // past any hue

        // The A table ends at the input of the brightest color the input clamp lets through (the AP1 clamp box corner).
        internal static double InputAMax(in ODTParams p) =>
            RGB_to_Aab(math.mul(AP1_TO_AP0, new double3(p.ts.forward_limit)), p.input_params)[0];

        // Rows 0, 3 and 4. cusp_from_table's and lookup_hue_interval's intervals change only at a hue-table entry or
        // at a step of the narrowed search window (entry i0 = 1 + hue * totalTableSize / 360). Between two such
        // breakpoints the values are linear, so each piece is taken from the port at two interior hues and checked at
        // a third. A broken invariant writes NaN instead of throwing: Aces2LutBaker.BuildPixels runs this as a job and
        // rejects NaN.
        internal static void BuildPieces(in ODTParams p, NativeArray<float4> pixels)
        {
            using var cuts = new NativeList<double>(2 * totalTableSize, Allocator.Temp);
            cuts.Add(0);
            cuts.Add(360);
            for (int i = 0; i < p.TABLE_hues.Length; i++)
                if (p.TABLE_hues[i] > 0 && p.TABLE_hues[i] < 360)
                    cuts.Add(p.TABLE_hues[i]);
            for (int m = 1; m * 360.0 / totalTableSize < 360; m++)
                cuts.Add(m * 360.0 / totalTableSize);
            cuts.Sort();
            for (int i = cuts.Length - 1; i > 0; i--)
                if (cuts[i] - cuts[i - 1] < 1e-6)
                    cuts.RemoveAt(i);

            int pieces = cuts.Length - 1;
            if (pieces > Width)
            {
                Fail(pixels);
                return;
            }

            for (int j = 0; j < pieces; j++)
            {
                double lo = cuts[j], hi = cuts[j + 1];
                double hA = lo + 0.25 * (hi - lo), hB = lo + 0.75 * (hi - lo), hMid = 0.5 * (lo + hi);
                var a = init_HueDependentGamutParams(hA, p);
                var b = init_HueDependentGamutParams(hB, p);
                double slopeJ = (b.JMcusp[0] - a.JMcusp[0]) / (hB - hA);
                double slopeM = (b.JMcusp[1] - a.JMcusp[1]) / (hB - hA);
                double slopeGamma = (b.gamma_top_inv - a.gamma_top_inv) / (hB - hA);

                // A missed breakpoint shows as a piece that is not linear.
                var mid = init_HueDependentGamutParams(hMid, p);
                if (math.abs(mid.gamma_top_inv - (a.gamma_top_inv + slopeGamma * (hMid - hA))) > 1e-9 ||
                    math.abs(mid.JMcusp[0] - (a.JMcusp[0] + slopeJ * (hMid - hA))) > 1e-7)
                {
                    Fail(pixels);
                    return;
                }

                // The values are taken at the start hue as stored in float, which is where the shader measures from.
                double start = (float)lo;
                pixels[3 * Width + j] = new float4((float)start, (float)(a.JMcusp[0] + slopeJ * (start - hA)),
                    (float)slopeJ, (float)(a.JMcusp[1] + slopeM * (start - hA)));
                pixels[4 * Width + j] = new float4((float)slopeM, (float)(a.gamma_top_inv + slopeGamma * (start - hA)),
                    (float)slopeGamma, 0);
            }

            for (int j = pieces; j < Width; j++)
            {
                pixels[3 * Width + j] = default;
                pixels[4 * Width + j] = default;
            }

            int first = 0;
            for (int x = 0; x < HueSamples; x++)
            {
                double lo = x * 360.0 / HueSamples, hi = (x + 1) * 360.0 / HueSamples;
                while (first + 1 < pieces && cuts[first + 1] <= lo)
                    first++;

                float b1 = NoBreakpoint, b2 = NoBreakpoint;
                for (int k = first + 1; k < pieces && cuts[k] < hi; k++)
                {
                    if (b1 == NoBreakpoint)
                        b1 = (float)cuts[k];
                    else if (b2 == NoBreakpoint)
                        b2 = (float)cuts[k];
                    else
                    {
                        Fail(pixels); // more than two breakpoints in one cell
                        return;
                    }
                }
                pixels[x] = new float4(first, b1, b2, 0);
            }
            pixels[HueSamples] = default;
        }

        // One column of rows 1 and 2. Aces2LutBaker runs the columns as a parallel job.
        internal static void BuildColumn(in ODTParams p, NativeArray<float4> pixels, int x, double aInMax)
        {
            double hue = (x % HueSamples) * 360.0 / HueSamples; // the last column repeats hue 0
            pixels[Width + x] = new float4((float)reach_M_from_table(hue, p.TABLE_reach_M),
                (float)chroma_compress_norm(hue, p.chroma_compress_scale), 0, 0);

            double A = math.max(aInMax * x / (Width - 1), aInMax * 1e-6); // entry 0 holds the limits at A -> 0
            double J = Achromatic_n_to_J(A, p.input_params.cz);
            double Y = J_to_Y(J, p.input_params);
            double tonemapped = Y_to_J(tonescale_fwd(Y / ref_luminance, p.ts), p.input_params);
            double nJ = tonemapped / p.limit_J_max;
            pixels[2 * Width + x] = new float4((float)tonemapped, (float)math.pow(nJ, p.model_gamma_inv),
                (float)math.pow(tonemapped / J, p.model_gamma_inv), (float)nJ);
        }

        private static void Fail(NativeArray<float4> pixels) => pixels[0] = new float4(float.NaN);
    }
}
