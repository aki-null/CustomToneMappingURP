using System;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class ToneMappingModeParameter : VolumeParameter<ToneMappingMode>
    {
        public ToneMappingModeParameter(ToneMappingMode value, bool overrideState = false) : base(value, overrideState)
        {
        }
    }
}