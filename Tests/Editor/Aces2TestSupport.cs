using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using CustomToneMapping.Baker.ACES2;

namespace CustomToneMapping.Tests
{
    // Shared by the ACES 2.0 tests: the managed Academy reference for the package's inputs, and GPU readback.
    internal static class Aces2TestSupport
    {
        // Column-vector matrix in row-major constructor order, the same literals as HDROutput.hlsl
        // RotateRec2020ToRec709; math.mul(m, v) applies it.
        internal static readonly double3x3 Rec2020ToRec709 = new double3x3(
            1.660496, -0.587656, -0.072840, -0.124547, 1.132895, -0.008348, -0.018154, -0.100597, 1.118751);

        internal static double3 ToDouble3(Vector3 v) => new double3(v.x, v.y, v.z);

        // ACEScg to ACES2065-1 with the port's own matrix (CTL rows stored as columns, so math.mul(m, v) is CTL's mult_f3_f33(v, m)).
        internal static double3 AP1ToAP0(Vector3 rgb) => math.mul(AcademyTransform.AP1_TO_AP0, ToDouble3(rgb));

        // The package's two limiting displays: Rec.709/D65 at 100 nits for SDR, Rec.2020/D65 at the peak for HDR.
        internal static AcademyTransform.ODTParams Parameters(int peakNits, bool hdr) =>
            AcademyTransform.init_ODTParams(peakNits, Aces2LutBaker.Primaries(hdr));

        // ACES 2.0 of an ACES2065-1 value in 100-nit units, clamped to the display like CustomAces2Tonemap.
        internal static double3 Aces2FromAP0(double3 ap0, in AcademyTransform.ODTParams p) =>
            math.clamp(AcademyTransform.outputTransform_fwd(ap0, p), 0, p.peakLuminance / 100.0);

        // ... of a Unity scene-linear Rec.709 value, as the standard grading branch hands it over.
        internal static double3 Aces2FromRec709(double3 rgb, in AcademyTransform.ODTParams p) => Aces2FromAP0(math.mul(Aces2SdrToneMap.Rec709ToAP0, rgb), p);

        // ... of an ACEScg value, as URP's ACES grading branch hands it to the hooked calls.
        internal static double3 Aces2FromAP1(Vector3 rgb, in AcademyTransform.ODTParams p) => Aces2FromAP0(AP1ToAP0(rgb), p);

        internal static Color[] ReadPixels(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                texture.Apply();
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }

        // Draws a full-screen triangle with the material into the target and returns texel (0,0).
        internal static Color Draw(Material material, RenderTexture target)
        {
            using (var cmd = new CommandBuffer())
            {
                cmd.SetRenderTarget(target);
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            return ReadPixels(target)[0];
        }
    }
}
