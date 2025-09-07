using Unity.Burst;
using UnityEngine;

namespace CustomToneMapping.Baker.GT
{
    [BurstCompile]
    public static class GTLutBaker
    {
        private static void BurstCompileHint()
        {
#pragma warning disable CS0219
            var dummy = new LutBaker.LutJob<GTToneMapping>();
#pragma warning restore CS0219
        }

        public static void BakeStripLut(GTConfig config, ref Texture2D texture)
        {
            var toneMap = new GTToneMapping();

            if (config.IsHdrOutput)
                toneMap.InitializeAsHdr(config);
            else
                toneMap.InitializeAsSdr(config);

            LutBaker.BakeStripLut(toneMap, config.IsHdrOutput, ref texture);
        }
    }
}