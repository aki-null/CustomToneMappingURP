using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CustomToneMapping.URP.RendererFeatures
{
    // Paper white as URP's internal GetHDROutputLuminanceParameters resolves it. Registered with UrpBridge at load:
    // the URP customization calls UrpBridge from inside URP, where this assembly is out of reach.
    internal static class UrpHdrOutput
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => UrpBridge.PaperWhiteResolver = PaperWhite;

        private static float PaperWhite(HDROutputUtils.HDRDisplayInformation info)
        {
            var tonemapping = VolumeManager.instance?.stack?.GetComponent<Tonemapping>();
            return tonemapping == null || tonemapping.detectPaperWhite.value
                ? info.paperWhiteNits
                : tonemapping.paperWhite.value;
        }
    }
}
