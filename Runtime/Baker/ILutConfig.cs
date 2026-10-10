using UnityEngine;

namespace CustomToneMapping.Baker
{
    public interface ILutConfig
    {
        int LutSize { get; }
    }

    // A config that bakes into an RGB LogC strip (GT, GT7, AgX), so the URP cache can check and bake it generically.
    internal interface IStripLutConfig
    {
        bool TryValidate(out string error);
        bool HdrOutput { get; }
        void BakeStripLut(ref Texture2D texture);
    }
}
