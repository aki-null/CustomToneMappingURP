// SPDX-License-Identifier: Apache-2.0
// Copyright Contributors to the ACES Project.
// Forward ACES 2.0, generated from the pinned CTL by Tools/ACES2~/port-shader.cjs.
// Why the transform runs per LUT texel instead of being sampled from a strip: see Aces2LutBaker.cs.
//
// GPU cost per LUT texel, from compiled mobile shader code: the Renderer Feature's pass costs about 0.65x URP's
// ACES 1.x LutBuilderHdr, and URP's LutBuilderHdr with ACES 2.0 about 1.3x the same with ACES 1.x. The shader is
// arithmetic-bound. The gamut mapper is the largest part, then the appearance model's powers. The per-hue and
// per-J values are tables; see shader-postlude.txt for the register budget any further change must keep.
#ifndef CUSTOM_ACES2_INCLUDED
#define CUSTOM_ACES2_INCLUDED

#define ACES2_ATLAS_WIDTH 1441
#define ACES2_ATLAS_HEIGHT 5
#define ACES2_HUE_SAMPLES 1440

static const float ref_luminance = 100.;
static const float cam_nl_Y_reference = 100.0;
static const float cam_nl_offset = 0.2713 * cam_nl_Y_reference;
static const float J_scale = 100.0;
static const float smooth_cusps = 0.12;
static const float cusp_mid_blend = 1.3;
static const float focus_gain_blend = 0.3;
static const float compression_threshold = 0.75;

struct TSParams
{
    float n;
    float n_r;
    float g;
    float t_1;
    float c_t;
    float s_2;
    float u_2;
    float m_2;
    float forward_limit;
    float log_peak;
};

struct JMhParams
{
    float3x3 MATRIX_RGB_to_CAM16_c;
    float3x3 MATRIX_CAM16_c_to_RGB;
    float3x3 MATRIX_cone_response_to_Aab;
    float3x3 MATRIX_Aab_to_cone_response;
    float F_L_n;
    float cz;
    float inv_cz;
    float A_w_J;
    float inv_A_w_J;
};

struct HueDependentGamutParams
{
    float2 JMcusp;
    float gamma_bottom_inv;
    float gamma_top_inv;
    float focus_J;
    float analytical_threshold;
    float reach_max_M;
};

struct ODTParams
{
    float peakLuminance;
    JMhParams input_params;
    JMhParams limit_params;
    TSParams ts;
    float limit_J_max;
    float model_gamma_inv;
    float sat;
    float sat_thr;
    float compr;
    float mid_J;
    float focus_dist;
    float lower_hull_gamma_inv;
    int2 hue_linearity_search_range;
};

float4 _Aces2Constants[13];

float Aces2Constant(int i) { return _Aces2Constants[i >> 2][i & 3]; }

float Aces2InvAInMax() { return Aces2Constant(38); }

float Aces2InvLimitJMax() { return Aces2Constant(39); }

float3x3 Aces2AP1ToCAM16()
{
    return float3x3(
        Aces2Constant(40), Aces2Constant(41), Aces2Constant(42),
        Aces2Constant(43), Aces2Constant(44), Aces2Constant(45),
        Aces2Constant(46), Aces2Constant(47), Aces2Constant(48));
}

// Full precision: the atlas is RGBA32F, and TEXTURE2D would sample it at mediump on GLES.
TEXTURE2D_FLOAT(_Aces2Atlas);

// Point-sampled at texel centres instead of read with Texture2D.Load, because URP's post-processing shaders
// declare no #pragma target and this file also compiles inside LutBuilderHdr under the URP customization.
float4 Aces2AtlasTexel(int x, int y)
{
    float2 uv = float2((x + .5) / ACES2_ATLAS_WIDTH, (y + .5) / ACES2_ATLAS_HEIGHT);
    return SAMPLE_TEXTURE2D_LOD(_Aces2Atlas, sampler_PointClamp, uv, 0);
}

// Every pow base in the CTL is non-negative (spow handles signed values), so abs() changes no result. It only
// stops the HLSL compiler from warning that pow is undefined for negative bases.
float aces2_pow(float x, float y) { return pow(abs(x), y); }

// Reach M and the chroma-compression norm of the hue: atlas row 1, a uniform grid of ACES2_HUE_SAMPLES steps. It
// includes the entries of the CTL's one-degree reach table, so reach M interpolates exactly as the CTL does; the norm
// is a smooth function of hue and interpolates to about 1e-6 relative.
float2 Aces2ReachNorm(float hue)
{
    float x = hue * (ACES2_HUE_SAMPLES / 360.0);
    int i = min((int)x, ACES2_HUE_SAMPLES - 1);
    return lerp(Aces2AtlasTexel(i, 1).xy, Aces2AtlasTexel(i + 1, 1).xy, x - i);
}

// CTL init_HueDependentGamutParams, plus the reach M that compress_gamut reads. The CTL searches its hue table twice
// (cusp_from_table and the narrowed lookup_hue_interval, which can land one entry off). Their results are linear in
// hue between breakpoints, so Aces2Atlas bakes the linear pieces: the hue's grid cell names its first piece and the at
// most two breakpoints inside it. The upper-hull gamma steps at breakpoints (its interpolation weight is not
// normalised), and the pieces keep those steps exactly where the CTL has them.
HueDependentGamutParams init_HueDependentGamutParams(float hue, ODTParams p)
{
    float4 cell = Aces2AtlasTexel(min((int)(hue * (ACES2_HUE_SAMPLES / 360.0)), ACES2_HUE_SAMPLES - 1), 0);
    int piece = (int)cell.x + (hue > cell.y ? 1 : 0) + (hue > cell.z ? 1 : 0);
    float4 a = Aces2AtlasTexel(piece, 3); // start hue, cusp J, its slope, cusp M
    float4 b = Aces2AtlasTexel(piece, 4); // cusp M slope, upper-hull gamma, its slope
    float dh = hue - a.x;
    HueDependentGamutParams hdp;
    hdp.JMcusp = float2(a.y + a.z * dh, a.w + b.x * dh);
    hdp.gamma_bottom_inv = p.lower_hull_gamma_inv;
    hdp.gamma_top_inv = b.y + b.z * dh;
    // compute_focus_J
    hdp.focus_J = lerp(hdp.JMcusp[0], p.mid_J, min(1.0, cusp_mid_blend - hdp.JMcusp[0] * Aces2InvLimitJMax()));
    hdp.analytical_threshold = lerp(hdp.JMcusp[0], p.limit_J_max, focus_gain_blend);
    hdp.reach_max_M = Aces2ReachNorm(hue).x;
    return hdp;
}

static const float3x3 AP0_TO_AP1 = float3x3(
    1.4514393161, -.0765537734, .0083161484,
    -.2365107469, 1.1762296998, -.0060324498,
    -.2149285693, -.0996759264, .9977163014);

static const float3x3 AP1_TO_AP0 = float3x3(
    .6954522414, .0447945634, -.0055258826,
    .1406786965, .8596711185, .0040252103,
    .1638690622, .0955343182, 1.0015006723);

float radians_to_degrees(float radians)
{
    return radians * 180.0 / 3.14159265358979323846;
}

float _post_adaptation_cone_response_compression_fwd(float Rc)
{
    const float F_L_Y = aces2_pow(Rc, 0.42);
    const float Ra = (F_L_Y) / (cam_nl_offset + F_L_Y);
    return Ra;
}

float copysign(float x, float y)
{
    return sign(y) * abs(x);
}

float post_adaptation_cone_response_compression_fwd(float v)
{
    const float abs_v = abs(v);
    const float Ra = _post_adaptation_cone_response_compression_fwd(abs_v);
    return copysign(Ra, v);
}

float J_to_Achromatic_n(float J, float inv_cz)
{
    return aces2_pow(J * (1. / J_scale), inv_cz);
}

float _post_adaptation_cone_response_compression_inv(float Ra)
{
    const float Ra_lim = min(Ra, 0.99);
    const float F_L_Y = (cam_nl_offset * Ra_lim) / (1. - Ra_lim);
    const float Rc = aces2_pow(F_L_Y, 1. / 0.42);
    return Rc;
}

float post_adaptation_cone_response_compression_inv(float v)
{
    const float abs_v = abs(v);
    const float Rc = _post_adaptation_cone_response_compression_inv(abs_v);
    return copysign(Rc, v);
}

float3 Aab_to_RGB(float3 Aab, JMhParams p)
{
    float3 rgb_a = mul(Aab, p.MATRIX_Aab_to_cone_response);

    float3 rgb_m = float3(
        post_adaptation_cone_response_compression_inv(rgb_a[0]),
        post_adaptation_cone_response_compression_inv(rgb_a[1]),
        post_adaptation_cone_response_compression_inv(rgb_a[2]));

    float3 rgb = mul(rgb_m, p.MATRIX_CAM16_c_to_RGB);

    return rgb;
}

float toe(float x, float limit, float k1_in, float k2_in, bool invert = false)
{
    if (x > limit)
        return x;

    float k2 = max(k2_in, 0.001);
    float k1 = sqrt(k1_in * k1_in + k2 * k2);
    float k3 = (limit + k1) / (limit + k2);

    if (invert)
    {
        return (x * x + k1 * x) / (k3 * (x + k2));
    }
    else
    {
        const float minus_b = k3 * x - k1;
        const float minus_c = k2 * k3 * x;
        return 0.5 * (minus_b + sqrt(minus_b * minus_b + 4. * minus_c));
    }
}

float get_focus_gain(float J, float analytical_threshold, float limit_J_max, float focus_dist)
{
    float gain = limit_J_max * focus_dist;

    if (J > analytical_threshold)
    {
        float gain_adjustment = log10((limit_J_max - analytical_threshold) / max(0.0001, limit_J_max - J));
        gain_adjustment = gain_adjustment * gain_adjustment + 1.;
        gain = gain * gain_adjustment;
    }

    return gain;
}

float solve_J_intersect(float J, float M, float focusJ, float maxJ, float slope_gain)
{
    const float M_scaled = M / slope_gain;
    const float a = M_scaled / focusJ;

    if (J < focusJ)
    {
        const float b = 1. - M_scaled;
        const float c = -J;
        const float det = b * b - 4. * a * c;
        const float root = sqrt(det);
        return -2. * c / (b + root);
    }
    else
    {
        const float b = -(1. + M_scaled + maxJ * a);
        const float c = maxJ * M_scaled + J;
        const float det = b * b - 4. * a * c;
        const float root = sqrt(det);
        return -2. * c / (b - root);
    }
}

float compute_compression_vector_slope(float intersect_J, float focus_J, float limit_J_max, float slope_gain)
{
    float direction_scalar;
    if (intersect_J < focus_J)
    {
        direction_scalar = intersect_J;
    }
    else
    {
        direction_scalar = limit_J_max - intersect_J;
    }
    return direction_scalar * (intersect_J - focus_J) / (focus_J * slope_gain);
}

float estimate_line_and_boundary_intersection_M(
    float J_axis_intersect, float slope, float inv_gamma, float J_max, float M_max, float J_intersection_reference)
{
    const float normalised_J = J_axis_intersect / J_intersection_reference;
    const float shifted_intersection = J_intersection_reference * aces2_pow(normalised_J, inv_gamma);

    return shifted_intersection * M_max / (J_max - slope * M_max);
}

float smin_scaled(float a, float b, float scale_reference)
{
    const float s_scaled = smooth_cusps * scale_reference;
    const float h = max(s_scaled - abs(a - b), 0.0) / s_scaled;
    return min(a, b) - h * h * h * s_scaled * (1. / 6.);
}

float find_gamut_boundary_intersection(
    float2 JM_cusp, float J_max, float gamma_top_inv, float gamma_bottom_inv, float J_intersect_source, float slope,
    float J_intersect_cusp)
{
    const float M_boundary_lower = estimate_line_and_boundary_intersection_M(
        J_intersect_source, slope, gamma_bottom_inv, JM_cusp[0], JM_cusp[1], J_intersect_cusp);

    const float f_J_intersect_cusp = J_max - J_intersect_cusp;
    const float f_J_intersect_source = J_max - J_intersect_source;
    const float f_JM_cusp_J = J_max - JM_cusp[0];
    const float M_boundary_upper = estimate_line_and_boundary_intersection_M(
        f_J_intersect_source, -slope, gamma_top_inv, f_JM_cusp_J, JM_cusp[1], f_J_intersect_cusp);

    float M_boundary = smin_scaled(M_boundary_lower, M_boundary_upper, JM_cusp[1]);
    return M_boundary;
}

float reinhard_remap(float scale, float nd, bool invert = false)
{
    if (invert)
    {
        if (nd >= 1.0)
        {
            return scale;
        }
        else
        {
            return scale * -(nd / (nd - 1.));
        }
    }
    return scale * nd / (1. + nd);
}

float remap_M(float M, float gamut_boundary_M, float reach_boundary_M, bool invert = false)
{
    const float boundary_ratio = gamut_boundary_M / reach_boundary_M;
    const float proportion = max(boundary_ratio, compression_threshold);
    const float threshold = proportion * gamut_boundary_M;

    if (M <= threshold || proportion >= 1.)
        return M;

    const float m_offset = M - threshold;
    const float gamut_offset = gamut_boundary_M - threshold;
    const float reach_offset = reach_boundary_M - threshold;

    const float scale = reach_offset / ((reach_offset / gamut_offset) - 1.);
    const float nd = m_offset / scale;

    return threshold + reinhard_remap(scale, nd, invert);
}

float3 compress_gamut(float3 JMh, float Jx, ODTParams p, HueDependentGamutParams hdp, bool invert = false)
{
    const float J = JMh[0];
    const float M = JMh[1];
    const float h = JMh[2];

    const float slope_gain = get_focus_gain(Jx, hdp.analytical_threshold, p.limit_J_max, p.focus_dist);
    const float J_intersect_source = solve_J_intersect(J, M, hdp.focus_J, p.limit_J_max, slope_gain);
    const float gamut_slope = compute_compression_vector_slope(
        J_intersect_source, hdp.focus_J, p.limit_J_max, slope_gain);

    const float J_intersect_cusp = solve_J_intersect(
        hdp.JMcusp[0], hdp.JMcusp[1], hdp.focus_J, p.limit_J_max, slope_gain);

    const float gamut_boundary_M = find_gamut_boundary_intersection(
        hdp.JMcusp, p.limit_J_max, hdp.gamma_top_inv, hdp.gamma_bottom_inv, J_intersect_source, gamut_slope,
        J_intersect_cusp);

    if (gamut_boundary_M <= 0.)
    {
        float3 returnJMh = float3(J, 0., h);
        return returnJMh;
    }

    float reach_max_M = hdp.reach_max_M;

    const float reach_boundary_M = estimate_line_and_boundary_intersection_M(
        J_intersect_source, gamut_slope, p.model_gamma_inv, p.limit_J_max, reach_max_M, p.limit_J_max);

    const float remapped_M = remap_M(M, gamut_boundary_M, reach_boundary_M, invert);

    float3 JMhcompressed = float3(J_intersect_source + remapped_M * gamut_slope, remapped_M, h);

    return JMhcompressed;
}

// Hand-written, emitted after the generated functions. It replaces the CTL's outputTransform_fwd (as
// Aces2OutputTransformAP1), tonemap_and_compress_fwd, chroma_compress_fwd and gamut_compress_fwd with the same math,
// rearranged around the per-display tables that Aces2Atlas bakes from the reference port: the per-hue values (rows 0,
// 1, 3, 4) and the powers the tonescale and chroma compression take of the input J (row 2, indexed by the input
// achromatic response A). The reference-vector tests check the result.
//
// Mobile register budget: URP's LutBuilderHdr inlines this after its own grading, and the combination sits right at
// the register count that still allows full occupancy on mobile GPUs. Small structural changes here (branch
// attributes, where the table lookups happen, what stays live) can double the register count and halve occupancy.
// Baking the chroma compression's toe constants into another row, for one, saved 2% of the cycles and cost that.
// Re-check that LutBuilderHdr compiles to full occupancy after changing this file.
float4 Aces2ARow(float A)
{
    float x = saturate(A * Aces2InvAInMax()) * (ACES2_ATLAS_WIDTH - 1);
    int i = min((int)x, ACES2_ATLAS_WIDTH - 2);
    return lerp(Aces2AtlasTexel(i, 2), Aces2AtlasTexel(i + 1, 2), x - i);
}

// CTL chroma_compress_fwd. jt = Aces2ARow(input A): tonemapped J, pow(nJ, gamma), pow(tonemapped J / J, gamma), nJ.
// reachNorm = Aces2ReachNorm(hue): reach M and the chroma-compression norm.
float3 chroma_compress_fwd(float M, float h, float4 jt, float2 reachNorm, ODTParams p)
{
    float M_compr = M;
    [branch] if (M != 0.0)
    {
        float nJ = jt.w, snJ = max(0.0, 1.0 - nJ);
        float Mnorm = reachNorm.y, invMnorm = 1.0 / Mnorm;
        float limit = jt.y * reachNorm.x * invMnorm;
        float toe_limit = limit - 0.001;
        float toe_snJ_sat = snJ * p.sat;
        float toe_sqrt_nJ_sat_thr = sqrt(nJ * nJ + p.sat_thr);
        float toe_nJ_compr = nJ * p.compr;
        M_compr = M * jt.z * invMnorm;
        M_compr = limit - toe(limit - M_compr, toe_limit, toe_snJ_sat, toe_sqrt_nJ_sat_thr, false);
        M_compr = toe(M_compr, limit, toe_nJ_compr, snJ, false);
        M_compr = M_compr * Mnorm;
    }
    return float3(jt.x, M_compr, h);
}

// CTL gamut_compress_fwd, with the hue parameters from the atlas. Its early outs keep J (clamped to 0) and drop M.
float3 gamut_compress_fwd(float3 JMh, HueDependentGamutParams hdp, ODTParams p)
{
    const float J = JMh[0];
    const float M = JMh[1];
    float3 result = float3(max(J, 0.), 0., JMh[2]);
    [branch] if (J > 0. && M >= 0. && J <= p.limit_J_max)
        result = compress_gamut(JMh, J, p, hdp, false);
    return result;
}

// CTL outputTransform_fwd on ACEScg. The CTL clamps in AP1 between two matrices (AP0 -> AP1 and back); starting from
// AP1 and fusing AP1 -> AP0 into the input model's RGB -> CAM16 matrix leaves one. CTL Aab_to_JMh would compute the
// input J, but row 2 is indexed by A directly, and the tonescale and gamut compression keep hue unchanged. The output
// side (CTL JMh_to_Aab) rebuilds the hue direction with cos and sin instead of carrying Aab.yz / M through the
// transform: two fewer live values, which the register budget above needs.
float3 Aces2OutputTransformAP1(float3 ap1, ODTParams p)
{
    float3 rgb_m = mul(clamp(ap1, 0., p.ts.forward_limit), Aces2AP1ToCAM16());
    float3 rgb_a = float3(post_adaptation_cone_response_compression_fwd(rgb_m[0]),
                          post_adaptation_cone_response_compression_fwd(rgb_m[1]),
                          post_adaptation_cone_response_compression_fwd(rgb_m[2]));
    float3 Aab = mul(rgb_a, p.input_params.MATRIX_cone_response_to_Aab);
    // CTL Aab_to_JMh: no colour when A <= 0.
    bool lit = Aab[0] > 0.;
    float A = lit ? Aab[0] : 0.;
    float M = lit ? sqrt(Aab[1] * Aab[1] + Aab[2] * Aab[2]) : 0.;
    // GLSL leaves atan(0, 0) undefined. The CTL's C atan2 returns 0 there. atan2 stays within [-180, 180] degrees, so
    // the CTL's wrap_to_360 reduces to one add (its fmod is the identity here).
    float h = M > 0. ? radians_to_degrees(atan2(Aab[2], Aab[1])) : 0.;
    h = h < 0. ? h + 360. : h;
    float3 tonemappedJMh = chroma_compress_fwd(M, h, Aces2ARow(A), Aces2ReachNorm(h), p);
    float3 compressedJMh = gamut_compress_fwd(tonemappedJMh, init_HueDependentGamutParams(h, p), p);
    float hr = compressedJMh[2] * (3.14159265358979323846 / 180.0);
    float3 AabOut = float3(J_to_Achromatic_n(compressedJMh[0], p.limit_params.inv_cz),
                           compressedJMh[1] * float2(cos(hr), sin(hr)));
    return Aab_to_RGB(AabOut, p.limit_params);
}

ODTParams Aces2Parameters()
{
    ODTParams p = (ODTParams)0;
    p.peakLuminance = Aces2Constant(0);
    p.input_params.MATRIX_cone_response_to_Aab = float3x3(
        Aces2Constant(1), Aces2Constant(2), Aces2Constant(3),
        Aces2Constant(4), Aces2Constant(5), Aces2Constant(6),
        Aces2Constant(7), Aces2Constant(8), Aces2Constant(9));
    p.limit_params.MATRIX_CAM16_c_to_RGB = float3x3(
        Aces2Constant(10), Aces2Constant(11), Aces2Constant(12),
        Aces2Constant(13), Aces2Constant(14), Aces2Constant(15),
        Aces2Constant(16), Aces2Constant(17), Aces2Constant(18));
    p.limit_params.MATRIX_Aab_to_cone_response = float3x3(
        Aces2Constant(19), Aces2Constant(20), Aces2Constant(21),
        Aces2Constant(22), Aces2Constant(23), Aces2Constant(24),
        Aces2Constant(25), Aces2Constant(26), Aces2Constant(27));
    p.limit_params.inv_cz = Aces2Constant(28);
    p.ts.forward_limit = Aces2Constant(29);
    p.limit_J_max = Aces2Constant(30);
    p.model_gamma_inv = Aces2Constant(31);
    p.sat = Aces2Constant(32);
    p.sat_thr = Aces2Constant(33);
    p.compr = Aces2Constant(34);
    p.mid_J = Aces2Constant(35);
    p.focus_dist = Aces2Constant(36);
    p.lower_hull_gamma_inv = Aces2Constant(37);
    return p;
}

float3 CustomAces2TonemapAP1(float3 ap1)
{
    ODTParams p = Aces2Parameters();
    return clamp(Aces2OutputTransformAP1(ap1, p), 0.0, p.peakLuminance / 100.0);
}

float3 CustomAces2Tonemap(float3 ap0)
{
    return CustomAces2TonemapAP1(mul(ap0, AP0_TO_AP1));
}

#endif
