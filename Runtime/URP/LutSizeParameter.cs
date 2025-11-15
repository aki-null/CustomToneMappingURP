using System;
using CustomToneMapping.Baker;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class LutSizeParameter : VolumeParameter<int>
    {
        public const int MinLutSize = LutBaker.MinLutSize;
        public const int MaxLutSize = LutBaker.MaxLutSize;

        public LutSizeParameter(int value, bool overrideState = false)
            : base(UnityEngine.Mathf.Clamp(value, MinLutSize, MaxLutSize), overrideState)
        {
        }

        public override int value
        {
            get => m_Value;
            set => m_Value = UnityEngine.Mathf.Clamp(value, MinLutSize, MaxLutSize);
        }
    }
}