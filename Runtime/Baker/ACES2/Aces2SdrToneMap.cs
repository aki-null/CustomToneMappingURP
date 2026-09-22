using Unity.Collections;
using Unity.Mathematics;
using static CustomToneMapping.Baker.ACES2.AcademyTransform;

namespace CustomToneMapping.Baker.ACES2
{
    // ACES 2.0 as an IToneMap, so LutBaker's Burst job can bake it into an ordinary LogC strip for URP's LDR grading
    // path, the same way it bakes GT, GT7 and AgX. LDR grading means SDR output, so the display is always
    // Rec.709/D65 at 100 nits.
    //
    // It reads the same parameter tables the atlas is built from, straight from memory, so the strip needs no atlas
    // texture and no GPU pass.
    internal struct Aces2SdrToneMap : IToneMap
    {
        // Unity scene-linear Rec.709/D65 -> ACES2065-1. Same literals as Core ACES.hlsl sRGB_2_AP0 and as
        // the Rec.709 input of kCustomTonemapRec709ToAP1 in TonemapParams.hlsl, so the strip and the shader agree.
        internal static readonly double3x3 Rec709ToAP0 = new double3x3(
            0.4397010, 0.3829780, 0.1773350,
            0.0897923, 0.8134230, 0.0967616,
            0.0175440, 0.1115440, 0.8707040);

        // LutJob is an IJobParallelFor, and the per-hue tables are read at indices unrelated to the job index.
        // Nothing writes them here, so lifting the range restriction is safe.
        [NativeDisableParallelForRestriction] internal ODTParams Params;

        public bool IsHDROutput => false;
        public Colorspace InputColorspace => Colorspace.Rec709;
        public Colorspace OutputColorspace => Colorspace.Rec709;

        public float3 ApplyToneMap(float3 rgb)
        {
            var ap0 = math.mul(Rec709ToAP0, new double3(rgb.x, rgb.y, rgb.z));
            var display = math.clamp(outputTransform_fwd(ap0, Params), 0, Params.peakLuminance / 100.0);
            return new float3((float)display.x, (float)display.y, (float)display.z);
        }
    }
}
