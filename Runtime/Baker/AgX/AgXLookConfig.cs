namespace CustomToneMapping.Baker.AgX
{
    public struct AgXLookConfig
    {
        public AgXLookPreset LookPreset;
        public float Intensity;

        public static AgXLookConfig GetPreset(AgXLookPreset preset)
        {
            return preset switch
            {
                AgXLookPreset.Punchy => new AgXLookConfig
                {
                    LookPreset = AgXLookPreset.Punchy,
                    Intensity = 1.0f
                },
                _ => new AgXLookConfig
                {
                    LookPreset = AgXLookPreset.None,
                    Intensity = 0.0f
                }
            };
        }
    }
}