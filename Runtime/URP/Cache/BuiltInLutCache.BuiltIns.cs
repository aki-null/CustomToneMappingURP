using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using UnityEngine;

namespace CustomToneMapping.URP
{
    internal static partial class BuiltInLutCache
    {
        private enum BuiltInToneMappingKind : byte
        {
            GT,
            GT7,
            AgX
        }

        // These arrays are parallel to ReadyEntries and FailureEntries. Each
        // mapper keeps its own strongly typed snapshot so equality remains exact
        // and constrained-generic (no boxing or shared validator switch).
        private static readonly GTConfig[] GtSnapshots = new GTConfig[Capacity];
        private static readonly GT7Config[] Gt7Snapshots = new GT7Config[Capacity];
        private static readonly AgXConfig[] AgxSnapshots = new AgXConfig[Capacity];
        private static readonly GTConfig[] GtFailureSnapshots = new GTConfig[Capacity];
        private static readonly GT7Config[] Gt7FailureSnapshots = new GT7Config[Capacity];
        private static readonly AgXConfig[] AgxFailureSnapshots = new AgXConfig[Capacity];

        private static void ClearReadySnapshot(int slot)
        {
            GtSnapshots[slot] = default;
            Gt7Snapshots[slot] = default;
            AgxSnapshots[slot] = default;
        }

        private static void ClearSnapshots()
        {
            System.Array.Clear(GtSnapshots, 0, GtSnapshots.Length);
            System.Array.Clear(Gt7Snapshots, 0, Gt7Snapshots.Length);
            System.Array.Clear(AgxSnapshots, 0, AgxSnapshots.Length);
            System.Array.Clear(GtFailureSnapshots, 0, GtFailureSnapshots.Length);
            System.Array.Clear(Gt7FailureSnapshots, 0, Gt7FailureSnapshots.Length);
            System.Array.Clear(AgxFailureSnapshots, 0, AgxFailureSnapshots.Length);
        }

        private readonly struct GTAdapter : IBuiltInLutAdapter<GTConfig>
        {
            public bool TryValidate(in GTConfig config, out string error) => config.TryValidate(out error);

            public bool IsHdrOutput(in GTConfig config) => config.IsHdrOutput;

            public void Bake(in GTConfig config, ref Texture2D texture)
            {
                GTLutBaker.BakeStripLut(config, ref texture);
            }
        }

        private readonly struct GT7Adapter : IBuiltInLutAdapter<GT7Config>
        {
            public bool TryValidate(in GT7Config config, out string error) => config.TryValidate(out error);

            public bool IsHdrOutput(in GT7Config config) => config.IsHdrOutput;

            public void Bake(in GT7Config config, ref Texture2D texture)
            {
                GT7LutBaker.BakeStripLut(config, ref texture);
            }
        }

        private readonly struct AgXAdapter : IBuiltInLutAdapter<AgXConfig>
        {
            public bool TryValidate(in AgXConfig config, out string error) => config.TryValidate(out error);

            public bool IsHdrOutput(in AgXConfig config) => config.IsHdrOutput;

            public void Bake(in AgXConfig config, ref Texture2D texture)
            {
                AgXLutBaker.BakeStripLut(config, ref texture);
            }
        }

        internal static MaterialPreparationStatus GetOrBake(in GTConfig config,
            out Texture2D texture, out Vector3 lutParams, out string error,
            out bool shouldReportFailure)
        {
            return GetOrBakeCore<GTConfig, GTAdapter>(in config, BuiltInToneMappingKind.GT,
                GtSnapshots, GtFailureSnapshots, out texture, out lutParams, out error,
                out shouldReportFailure);
        }

        internal static MaterialPreparationStatus GetOrBake(in GT7Config config,
            out Texture2D texture, out Vector3 lutParams, out string error,
            out bool shouldReportFailure)
        {
            return GetOrBakeCore<GT7Config, GT7Adapter>(in config, BuiltInToneMappingKind.GT7,
                Gt7Snapshots, Gt7FailureSnapshots, out texture, out lutParams, out error,
                out shouldReportFailure);
        }

        internal static MaterialPreparationStatus GetOrBake(in AgXConfig config,
            out Texture2D texture, out Vector3 lutParams, out string error,
            out bool shouldReportFailure)
        {
            return GetOrBakeCore<AgXConfig, AgXAdapter>(in config, BuiltInToneMappingKind.AgX,
                AgxSnapshots, AgxFailureSnapshots, out texture, out lutParams, out error,
                out shouldReportFailure);
        }
    }
}
