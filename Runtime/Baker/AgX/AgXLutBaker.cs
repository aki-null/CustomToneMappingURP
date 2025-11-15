using Unity.Burst;
using UnityEngine;

namespace CustomToneMapping.Baker.AgX
{
    [BurstCompile]
    public static class AgXLutBaker
    {
        private static void BurstCompileHint()
        {
#pragma warning disable CS0219
            var dummy = new LutBaker.LutJob<AgXToneMapping>();
#pragma warning restore CS0219
        }

        public static void BakeStripLut(AgXConfig settings, ref Texture2D texture)
        {
            var mapper = new AgXToneMapping();
            mapper.Initialize(settings);

            LutBaker.BakeStripLut(mapper, settings.IsHdrOutput, settings.LutSize, ref texture);
        }
    }
}