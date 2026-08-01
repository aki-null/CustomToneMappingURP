using System;

namespace CustomToneMapping.Baker.AgX
{
    public struct AgXLookConfig : IEquatable<AgXLookConfig>
    {
        public AgXLookPreset LookPreset;
        public float Intensity;

        public bool TryValidate(out string error)
        {
            if (!ValidationPrimitives.IsFinite(Intensity) || Intensity < 0.0f || Intensity > 1.0f)
            {
                error = "AgX look intensity must be finite and between 0 and 1.";
                return false;
            }

            switch (LookPreset)
            {
                case AgXLookPreset.None:
                case AgXLookPreset.Punchy:
                case AgXLookPreset.Greyscale:
                case AgXLookPreset.VeryHighContrast:
                case AgXLookPreset.HighContrast:
                case AgXLookPreset.MediumHighContrast:
                case AgXLookPreset.BaseContrast:
                case AgXLookPreset.MediumLowContrast:
                case AgXLookPreset.LowContrast:
                case AgXLookPreset.VeryLowContrast:
                    error = null;
                    return true;
                default:
                    error = "AgX contains an unsupported look preset.";
                    return false;
            }
        }

        public bool Equals(AgXLookConfig other)
        {
            return LookPreset == other.LookPreset && HashUtil.FloatBitsEqual(Intensity, other.Intensity);
        }

        public static AgXLookConfig GetPreset(AgXLookPreset preset)
        {
            if (preset == AgXLookPreset.None)
            {
                return new AgXLookConfig
                {
                    LookPreset = AgXLookPreset.None,
                    Intensity = 0.0f
                };
            }

            return new AgXLookConfig
            {
                LookPreset = preset,
                Intensity = 1.0f
            };
        }
    }
}