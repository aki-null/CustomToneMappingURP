using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom Tone Mapping")]
    public sealed class CustomToneMapping : VolumeComponent
    {
        public ToneMappingModeParameter mode = new(ToneMappingMode.None);

        [HideInInspector] public TextureParameter lutTexture = new(null);
    }
}