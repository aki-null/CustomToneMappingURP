using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CustomToneMapping.URP.RendererFeatures
{
    [SupportedOnRenderer(typeof(UniversalRendererData))]
    [DisallowMultipleRendererFeature("Custom Tone Mapping")]
    public class CustomToneMappingRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, Reload("Runtime/RendererFeatures/CustomToneMapChain.shader")]
        private Shader shader;

        private CustomToneMappingPass _pass;

        public override void Create()
        {
            _pass?.Dispose();
            // The serialized [Reload] reference is filled in for imported renderer assets. Features created at runtime
            // (tests, scripts) fall back to Shader.Find.
            _pass = new CustomToneMappingPass(shader != null ? shader : Shader.Find("Hidden/CustomToneMapChain"))
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses + 1
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.postProcessEnabled)
                return;

            // A post-processed camera counts as a frame that ages the LUT cache, even with mode None, so unused LUTs
            // still expire.
            BuiltInLutCache.Tick();
            var customToneMapping = VolumeManager.instance?.stack?.GetComponent<CustomToneMapping>();
            if (customToneMapping == null || customToneMapping.mode.value == ToneMappingMode.None)
                return;

            // The pass checks the camera's grading mode: HDR output makes URP grade in HDR whatever the asset says.
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
        }
    }
}
