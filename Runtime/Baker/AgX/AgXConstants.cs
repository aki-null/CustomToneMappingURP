using Unity.Mathematics;

namespace CustomToneMapping.Baker.AgX
{
    public static class AgXConstants
    {
        // Log encoding parameters
        public const float MidGrey = 0.18f;
        public const float Log2Minimum = -10.0f;
        public const float Log2Maximum = 6.5f;

        // Sigmoid curve parameters
        public const float ExponentToe = 1.5f;
        public const float ExponentShoulder = 1.5f;
        private const float FulcrumSlope = 2.4f;

        public const float ChromaMixPercent = 40.0f;

        public static float FulcrumInput => math.abs(Log2Minimum) / (Log2Maximum - Log2Minimum);
        public static float FulcrumOutput => math.pow(MidGrey, 1.0f / 2.4f);

        public static float CalculatedSlope =>
            FulcrumSlope * ((math.abs(Log2Minimum) + Log2Maximum) / 16.5f);

        // Transformation matrices
        public static readonly float3x3 InsetMatrix = new(
            0.856627153315983f, 0.095121240538159f, 0.048251606145858f,
            0.137318972929847f, 0.761241990602591f, 0.101439036467562f,
            0.11189821299995f, 0.07679941860319f, 0.811302368396859f
        );

        public static readonly float3x3 OutsetMatrix = new(
            1.127100581814437f, -0.110606643096603f, -0.016493938717835f,
            -0.141329763498438f, 1.157823702216272f, -0.016493938717834f,
            -0.141329763498438f, -0.110606643096603f, 1.25193640659504f
        );

        public static readonly float3x3 Rec2020ToXyz = new(
            0.6369535067850740f, 0.1446191846692331f, 0.1688558539228734f,
            0.2626983389565560f, 0.6780087657728165f, 0.0592928952706273f,
            0.0000000000000000f, 0.0280731358475570f, 1.0608272349505707f
        );

        public static readonly float3x3 XyzToRec2020 = new(
            1.7166634277958805f, -0.3556733197301399f, -0.2533680878902478f,
            -0.6666738361988869f, 1.6164557398246981f, 0.0157682970961337f,
            0.0176424817849772f, -0.0427769763827532f, 0.9422432810184308f
        );

        public static readonly float3x3 XyzToP3 = new(
            2.493496911941425f, -0.9313836179191239f, -0.4027107844507168f,
            -0.8294889695615747f, 1.7626640603183463f, 0.023624685841943577f,
            0.03584583024378447f, -0.07617238926804182f, 0.9568845240076872f
        );

        public static readonly float3x3 P3ToXyz = new(
            0.4865709486482162f, 0.26566769316909306f, 0.1982172852343625f,
            0.22897456406974884f, 0.6917385218365064f, 0.079286914093745f,
            0.0f, 0.04511338185890264f, 1.043944368900976f
        );

        public static readonly float3 Rec2020LuminanceCoeffs =
            new(0.2589235355689848f, 0.6104985346066525f, 0.13057792982436284f);
    }
}