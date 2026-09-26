using System;
using CustomToneMapping.Baker.ACES2;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomToneMapping.URP
{
    [Serializable, VolumeComponentMenu("Post-processing/ACES 2.0 Tone Mapping")]
    public sealed class Aces2ToneMapping : VolumeComponent
    {
        [Tooltip("Read HDR peak luminance from the active display.")]
        public BoolParameter detectPeakNits = new(true);
        // Not blended between volumes: every distinct peak is a new parameter bake.
        [Tooltip("HDR peak in nits, used when detection is off or the display reports none. SDR always uses 100 nits.")]
        public NoInterpClampedFloatParameter targetPeakNits =
            new(1000f, Aces2Config.MinPeakNits, Aces2Config.MaxPeakNits);
        [Tooltip("Grade in URP's ACES spaces (ACEScc contrast, ACEScg operations) before ACES 2.0. Needs the URP customization with HDR Color Grading (README, Method 2). Otherwise grading uses URP's standard spaces.")]
        public BoolParameter acesAwareGrading = new(true);
    }
}
