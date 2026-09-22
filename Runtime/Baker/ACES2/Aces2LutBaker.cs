using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static CustomToneMapping.Baker.ACES2.AcademyTransform;

namespace CustomToneMapping.Baker.ACES2
{
    // Bakes the ACES 2.0 Output Transform parameters for one display: the RGBA32F atlas that Aces2.hlsl reads (rows 0,
    // 1, 3 and 4 per-hue values, row 2 the tonescale and chroma compression over the input achromatic response; layout
    // in Aces2Atlas.cs) and the display constants for its uniform array.
    //
    // Why an atlas instead of a LogC strip like the other modes: the chain pass tone-maps URP's graded LUT, and
    // grading easily pushes values past the strip's input range of about 58.9 scene-linear. Contrast +10 alone
    // takes URP's brightest node to about 100. ACES 2.0 HDR output keeps rising up there, so a strip clips it,
    // while the atlas evaluates the transform for any input.
    //
    // Measured through URP's 32^3 LUT on the research scene-like samples (dE ITP p99, atlas vs 32^3 strip):
    // default grading 1.7 vs 3.3 at 100 nits and 2.3 vs 4.0 at 10000 nits; strong grading 4.6 vs 6.5 at 100 nits,
    // 5.3 vs 12.6 at 4000 and 5.7 vs 21 at 10000. A strip with a wider input range stops clipping, but its nodes
    // get sparser. Even a 64^3 wide strip stays about 20% worse than the atlas.
    //
    // URP's LDR grading path tone-maps each pixel, so a strip is the only option there (Aces2SdrToneMap).
    //
    // SDR is Rec.709/D65 at 100 nits, HDR is Rec.2020/D65 at the peak. URP keeps the display encoding.
    // Generated files, provenance and validation: Tools/ACES2~/README.md.
    [BurstCompile]
    internal static class Aces2LutBaker
    {
        // Burst only compiles a generic job it can see instantiated with concrete types. Without this the LDR strip
        // silently falls back to managed code and the bake is roughly seven times slower.
        private static void BurstCompileHint()
        {
#pragma warning disable CS0219
            var dummy = new LutBaker.LutJob<Aces2SdrToneMap>();
#pragma warning restore CS0219
        }

        // CTL init_ODTParams, with its three per-hue table loops (about 270k appearance-model evaluations) run as
        // Burst jobs over the generated *_entry functions. The serial version is still the reference, and Aces2Tests
        // checks this one against it.
        [BurstCompile]
        struct CornersJob : IJob
        {
            public ODTParams P;
            public NativeArray<double> HueTable;
            public NativeArray<double3> RgbCorners, JMhCorners;

            public void Execute()
            {
                var reachCorners = find_reach_corners_table(P.reach_params, P);
                build_limiting_cusp_corners_tables(RgbCorners, JMhCorners, P.limit_params, P.peakLuminance);
                HueTable.CopyFrom(build_hue_table(extract_sorted_cube_hues(reachCorners, JMhCorners)));
            }
        }

        [BurstCompile]
        struct ReachJob : IJobParallelFor
        {
            public JMhParams Reach;
            public double LimitJMax;
            public NativeArray<double> Table;

            // CTL loop: for (i = 0; i < tableSize) writing reachTable[i + baseIndex]; so entry i = index - baseIndex.
            public void Execute(int index)
            {
                if (index >= baseIndex && index < baseIndex + tableSize)
                    make_reach_m_table_entry(index - baseIndex, Reach, LimitJMax, Table);
            }
        }

        [BurstCompile]
        struct CuspJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<double> HueTable;
            [ReadOnly] public NativeArray<double3> RgbCorners, JMhCorners;
            public JMhParams Limit;
            public NativeArray<double3> Cusps;

            // CTL loop: for (i = baseIndex; i != totalTableSize) writing output_table[i]; so entry i = index.
            // The CTL's `previous` warm start is commented out in this revision, which is what makes the hues independent.
            public void Execute(int index)
            {
                if (index >= baseIndex)
                    build_cusp_table_entry(index, HueTable, RgbCorners, JMhCorners, Limit, default, Cusps);
            }
        }

        [BurstCompile]
        struct GammaJob : IJobParallelFor
        {
            public ODTParams P;

            // CTL loop: for (i = baseIndex; i != baseIndex + tableSize) writing upper_hull_gamma[i]; so entry i = index.
            public void Execute(int index)
            {
                if (index >= baseIndex && index < baseIndex + tableSize)
                    make_upper_hull_gamma_table_entry(index, P.TABLE_gamut_cusps, P, P.TABLE_upper_hull_gamma);
            }
        }

        [BurstCompile]
        struct AtlasJob : IJobParallelFor
        {
            // Read-only, so a column may read any table entry: each column looks up its hue in the finished tables.
            [ReadOnly] public ODTParams P;
            public double AInMax;
            [NativeDisableParallelForRestriction] public NativeArray<float4> Pixels; // a column writes several rows
            public void Execute(int column) => Aces2Atlas.BuildColumn(P, Pixels, column, AInMax);
        }

        [BurstCompile]
        struct PiecesJob : IJob
        {
            [ReadOnly] public ODTParams P;
            public NativeArray<float4> Pixels;
            public void Execute() => Aces2Atlas.BuildPieces(P, Pixels);
        }

        internal static ODTParams InitParametersParallel(double peakNits, in Chromaticities limitingPrimaries)
        {
            int n = totalTableSize, first = baseIndex, last = baseIndex + tableSize;
            var p = default(ODTParams);
            p.TABLE_reach_M = new NativeArray<double>(n, Allocator.TempJob);
            p.TABLE_hues = new NativeArray<double>(n, Allocator.TempJob);
            p.TABLE_gamut_cusps = new NativeArray<double3>(n, Allocator.TempJob);
            p.TABLE_upper_hull_gamma = new NativeArray<double>(n, Allocator.TempJob);
            try
            {
                p.peakLuminance = peakNits;
                p.input_params = init_JMhParams(AP0);
                p.reach_params = init_JMhParams(REACH_PRI);
                p.limit_params = init_JMhParams(limitingPrimaries);
                p.ts = init_TSParams(peakNits);
                p.limit_J_max = Y_to_J(peakNits, p.input_params);
                p.model_gamma_inv = 1.0 / model_gamma;
                p.sat = Math.Max(0.2, chroma_expand - (chroma_expand * chroma_expand_fact) * p.ts.log_peak);
                p.sat_thr = chroma_expand_thr / peakNits;
                p.compr = chroma_compress + (chroma_compress * chroma_compress_fact) * p.ts.log_peak;
                p.chroma_compress_scale = Math.Pow(0.03379 * peakNits, 0.30596) - 0.45135;
                p.mid_J = Y_to_J(p.ts.c_t * ref_luminance, p.input_params);
                p.focus_dist = focus_distance + focus_distance * focus_distance_scaling * p.ts.log_peak;
                p.lower_hull_gamma_inv = 1.0 / (1.14 + 0.07 * p.ts.log_peak);

                using var hueTable = new NativeArray<double>(n, Allocator.TempJob);
                using var rgbCorners = new NativeArray<double3>(totalCornerCount, Allocator.TempJob);
                using var jmhCorners = new NativeArray<double3>(totalCornerCount, Allocator.TempJob);
                // CornersJob takes the whole ODTParams, so the job safety system makes the other jobs wait for it. That is
                // cheap: it only runs a few bisections.
                var corners = new CornersJob { P = p, HueTable = hueTable, RgbCorners = rgbCorners, JMhCorners = jmhCorners }.Schedule();
                var reach = new ReachJob { Reach = p.reach_params, LimitJMax = p.limit_J_max, Table = p.TABLE_reach_M }.Schedule(n, 16, corners);
                var cusps = new CuspJob { HueTable = hueTable, RgbCorners = rgbCorners, JMhCorners = jmhCorners, Limit = p.limit_params, Cusps = p.TABLE_gamut_cusps }.Schedule(n, 16, corners);
                JobHandle.CombineDependencies(reach, cusps).Complete();

                p.TABLE_reach_M[0] = p.TABLE_reach_M[tableSize];
                p.TABLE_reach_M[last] = p.TABLE_reach_M[first];
                var cusp = p.TABLE_gamut_cusps;
                cusp[0] = new double3(cusp[tableSize][0], cusp[tableSize][1], hueTable[0]);
                cusp[last] = new double3(cusp[first][0], cusp[first][1], hueTable[last]);
                for (int i = 0; i < n; i++)
                    p.TABLE_hues[i] = cusp[i][2];

                new GammaJob { P = p }.Schedule(n, 8).Complete();
                p.TABLE_upper_hull_gamma[0] = p.TABLE_upper_hull_gamma[tableSize];
                p.TABLE_upper_hull_gamma[last] = p.TABLE_upper_hull_gamma[first];
                p.hue_linearity_search_range = determine_hue_linearity_search_range(p.TABLE_hues);
                return p;
            }
            catch
            {
                Dispose(ref p);
                throw;
            }
        }

        internal static void Dispose(ref ODTParams p)
        {
            p.TABLE_reach_M.Dispose();
            p.TABLE_hues.Dispose();
            p.TABLE_gamut_cusps.Dispose();
            p.TABLE_upper_hull_gamma.Dispose();
        }

        static readonly int AtlasId = Shader.PropertyToID("_Aces2Atlas"), ConstantsId = Shader.PropertyToID("_Aces2Constants");

        internal static void Bind(Material material, Texture2D atlas, Vector4[] constants)
        {
            material.SetTexture(AtlasId, atlas);
            material.SetVectorArray(ConstantsId, constants);
        }

        internal static Chromaticities Primaries(bool hdr) => hdr ? Rec2020Primaries : Rec709Primaries;

        // The LDR strip goes through the same LutBaker path as the RGB LUT modes: a Burst job over the texels, with
        // the same layout, format choice and 2D strip that ApplyLut2D reads. It bakes synchronously so the first
        // frame that asks for it gets it. A deferred GPU bake would leave that frame with no tone map, which URP
        // shows as clipped linear. It runs once per size, like the other modes, but costs far more: every texel runs
        // the full reference transform, mostly the gamut mapper. On a fast desktop CPU it takes about 7 ms at 32^3
        // and 41 ms at 64^3, and a low-end phone takes several times that. A float port would not help: the managed port runs no
        // faster in float. The cache never ages out the most recent strip, so the cost is paid at first use and when
        // LUT Size changes.
        internal static void BakeStrip(int lutSize, ref Texture2D texture)
        {
            var parameters = InitParametersParallel(100, Rec709Primaries);
            try
            {
                LutBaker.BakeStripLut(new Aces2SdrToneMap { Params = parameters }, false, lutSize, ref texture);
            }
            finally
            {
                Dispose(ref parameters);
            }
        }

        internal static bool IsSupported(out string error)
        {
            error = SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, GraphicsFormatUsage.Sample)
                ? null : "ACES 2.0 requires sampleable RGBA32 floating-point parameter tables.";
            return error == null;
        }

        // The atlas texels. Every texel starts as NaN and every job overwrites its texels, so a job that stopped early
        // (an exception inside a job only reaches the log) or a broken invariant in BuildPieces leaves a NaN, and the
        // bake fails instead of uploading a half-built atlas.
        internal static NativeArray<float4> BuildPixels(in ODTParams p)
        {
            var pixels = new NativeArray<float4>(Aces2Atlas.Width * Aces2Atlas.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new float4(float.NaN);
            var columns = new AtlasJob { P = p, Pixels = pixels, AInMax = Aces2Atlas.InputAMax(p) }.Schedule(Aces2Atlas.Width, 64);
            new PiecesJob { P = p, Pixels = pixels }.Schedule(columns).Complete();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (math.all(math.isfinite(pixels[i])))
                    continue;
                pixels.Dispose();
                throw new InvalidOperationException("The ACES 2.0 parameter atlas could not be built: a bake job failed (see the log) or a hue piece broke its invariants.");
            }
            return pixels;
        }

        // One display's parameters: the atlas and the uniform array that Bind hands to a material. The caller has
        // validated the config and checked IsSupported.
        internal static Texture2D Bake(Aces2Config config, out Vector4[] constants)
        {
            var p = InitParametersParallel(config.PeakNits, Primaries(config.IsHdrOutput));
            try
            {
                using var pixels = BuildPixels(p);
                var atlas = new Texture2D(Aces2Atlas.Width, Aces2Atlas.Height, TextureFormat.RGBAFloat, false, true)
                {
                    name = "ACES2Parameters",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                try
                {
                    atlas.SetPixelData(pixels, 0);
                    // Only the shader reads the atlas, so the CPU copy is released.
                    atlas.Apply(false, true);
                    constants = new Vector4[Aces2Atlas.ConstantVectors];
                    Aces2Atlas.BuildConstants(p, constants, Aces2Atlas.InputAMax(p));
                }
                catch
                {
                    CoreUtils.Destroy(atlas);
                    throw;
                }
                return atlas;
            }
            finally
            {
                Dispose(ref p);
            }
        }
    }
}
