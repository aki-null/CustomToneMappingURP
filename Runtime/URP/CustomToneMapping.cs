using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom Tone Mapping")]
    public sealed class CustomToneMapping : VolumeComponent
    {
        public ToneMappingModeParameter mode = new(ToneMappingMode.None);
        [Tooltip("Size of the baked tone mapping LUT: quality against bake time and memory.")]
        public LutSizeParameter lutSize = new(32);

        [HideInInspector] public TextureParameter lutTexture = new(null);
    }
}