using System;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class LutSizeParameter : VolumeParameter<LutSize>
    {
        public LutSizeParameter(LutSize value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}