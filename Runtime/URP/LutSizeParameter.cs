using System;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class LutSizeParameter : VolumeParameter<int>
    {
        public const int MinLutSize = 32;
        public const int MaxLutSize = 65;

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