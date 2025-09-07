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
            _pass = new CustomToneMappingPass(shader)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses + 1
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.postProcessEnabled)
                return;

            var customToneMapping = VolumeManager.instance.stack.GetComponent<CustomToneMapping>();
            if (customToneMapping == null || customToneMapping.mode.value == ToneMappingMode.None)
                return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }
    }
}