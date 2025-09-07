using System;
using CustomToneMapping.Baker.GT7;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable]
    public sealed class UcsModeParameter : VolumeParameter<UcsMode>
    {
        public UcsModeParameter(UcsMode value, bool overrideState = false) : base(value, overrideState)
        {
        }
    }
}