using Unity.Burst;
using UnityEngine;

namespace CustomToneMapping.Baker.GT7
{
    [BurstCompile]
    public static class GT7LutBaker
    {
        private static void BurstCompileHint()
        {
#pragma warning disable CS0219
            var dummy = new LutBaker.LutJob<GT7ToneMapping>();
#pragma warning restore CS0219
        }

        public static void BakeStripLut(GT7Config settings, ref Texture2D texture)
        {
            var mapper = new GT7ToneMapping();
            if (settings.IsHdrOutput)
            {
                mapper.InitializeAsHdr(settings);
            }
            else
            {
                mapper.InitializeAsSdr(settings);
            }

            LutBaker.BakeStripLut(mapper, settings.IsHdrOutput, settings.LutSize, ref texture);
        }
    }
}