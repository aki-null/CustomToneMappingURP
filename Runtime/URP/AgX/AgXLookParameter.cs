using System;
using CustomToneMapping.Baker.AgX;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class AgXLookParameter : VolumeParameter<AgXLookPreset>
    {
        public AgXLookParameter(AgXLookPreset value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}