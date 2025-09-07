using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace CustomToneMapping.URP.RendererFeatures
{
    public class CustomToneMappingPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private static readonly int HDROutputLuminanceParams = Shader.PropertyToID("_HDROutputLuminanceParams");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private const string LegacyRenderPathKeyword = "LEGACY_RENDER_PATH";

        public CustomToneMappingPass(Shader shader)
        {
            if (shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
        }

        private void ConfigureHDROutputInternal(ColorGamut hdrDisplayColorGamut,
            HDROutputUtils.HDRDisplayInformation hdrDisplayInformation)
        {
            HDROutputUtils.ConfigureHDROutput(_material, hdrDisplayColorGamut,
                HDROutputUtils.Operation.ColorConversion);
            var hdrParams = new Vector4(
                hdrDisplayInformation.minToneMapLuminance,
                hdrDisplayInformation.maxToneMapLuminance,
                hdrDisplayInformation.paperWhiteNits,
                1.0f / hdrDisplayInformation.paperWhiteNits
            );
            _material.SetVector(HDROutputLuminanceParams, hdrParams);
        }

        #region Render Graph

        private class CopyPassData
        {
            public TextureHandle SourceLut;
        }

        private class ToneMapPassData
        {
            public Material Material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            if (!resourceData.internalColorLut.IsValid()) return;

            // Prevent double tonemapping
            if (VolumeManager.instance.stack.GetComponent<Tonemapping>().mode != TonemappingMode.None)
            {
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();

            // Bind tone map LUT
            if (!UrpBridge.PrepareMaterial(_material,
                    cameraData.isHDROutputActive ? cameraData.hdrDisplayInformation : null))
            {
                return;
            }

            // Disable non-RG compatibility
            _material.DisableKeyword(LegacyRenderPathKeyword);

            ConfigureHDROutput(cameraData);

            var lutDesc = renderGraph.GetTextureDesc(resourceData.internalColorLut);
            lutDesc.name = "CustomToneMapTemp";
            lutDesc.clearBuffer = false;
            var tempLut = renderGraph.CreateTexture(lutDesc);

            using (var builder =
                   renderGraph.AddRasterRenderPass<ToneMapPassData>("Apply Tone Map", out var passData))
            {
                passData.Material = _material;

                builder.SetInputAttachment(resourceData.internalColorLut, 0);
                builder.SetRenderAttachment(tempLut, 0);

                builder.SetRenderFunc((ToneMapPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1);
                });
            }

            renderGraph.AddCopyPass(tempLut, resourceData.internalColorLut, passName: "Copy Back LUT");
        }

        private void ConfigureHDROutput(UniversalCameraData cameraData)
        {
            if (cameraData.isHDROutputActive)
            {
                ConfigureHDROutputInternal(cameraData.hdrDisplayColorGamut, cameraData.hdrDisplayInformation);
            }
        }

        #endregion

        #region Non-Render Graph Path (Legacy)

        // Reflection cache
        private static PropertyInfo _colorGradingLutProperty;
        private static bool _reflectionInitialized;

        private RTHandle _tempLutHandle;

        private static void InitializeReflection()
        {
            if (_reflectionInitialized) return;

            try
            {
                var universalRendererType = typeof(UniversalRenderer);
                _colorGradingLutProperty = universalRendererType.GetProperty("colorGradingLut",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to initialize reflection for colorGradingLut: {e.Message}");
            }

            _reflectionInitialized = true;
        }

        private static RTHandle GetColorGradingLut(ScriptableRenderer renderer)
        {
            // Unfortunately, colorGradingLut is internal in UniversalRenderer, so we use reflection to access it
            InitializeReflection();

            if (_colorGradingLutProperty == null || renderer is not UniversalRenderer) return null;

            try
            {
                return (RTHandle)_colorGradingLutProperty.GetValue(renderer);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to get colorGradingLut via reflection: {e.Message}");
            }

            return null;
        }

        [Obsolete("This rendering path is for compatibility mode only. Use Render Graph API instead.", false)]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Get the color grading LUT via reflection
            var colorGradingLut = GetColorGradingLut(renderingData.cameraData.renderer);
            if (colorGradingLut?.rt == null)
            {
                Debug.LogWarning("Color grading LUT not found or not created.");
                return;
            }

            // Allocate temp texture with same descriptor as LUT
            var lutDesc = colorGradingLut.rt.descriptor;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _tempLutHandle, lutDesc,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "CustomToneMapTemp");

            ConfigureTarget(_tempLutHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        [Obsolete("This rendering path is for compatibility mode only. Use Render Graph API instead.", false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            // Prevent double tonemapping
            var tonemapping = VolumeManager.instance.stack.GetComponent<Tonemapping>();
            if (tonemapping?.mode.value != TonemappingMode.None) return;

            // Get the color grading LUT via cached reflection
            var colorGradingLut = GetColorGradingLut(renderingData.cameraData.renderer);
            if (colorGradingLut?.rt == null || !colorGradingLut.rt.IsCreated()) return;

            var cameraData = renderingData.cameraData;
            var cmd = CommandBufferPool.Get("Custom Tone Mapping");

            using (new ProfilingScope(cmd, profilingSampler))
            {
                // Setup material for tone mapping
                UrpBridge.PrepareMaterial(_material,
                    cameraData.isHDROutputActive ? cameraData.hdrDisplayInformation : null);

                // Enable non-RG compatibility
                _material.EnableKeyword(LegacyRenderPathKeyword);

                ConfigureHDROutputLegacy(cameraData);

                // Apply tone mapping
                CoreUtils.SetRenderTarget(cmd, _tempLutHandle,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.None, Color.clear);
                _material.SetTexture(MainTexProperty, colorGradingLut);
                CoreUtils.DrawFullScreen(cmd, _material);

                // Copy back to original LUT
                CoreUtils.SetRenderTarget(cmd, colorGradingLut,
                    RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    ClearFlag.None, Color.clear);
                Blitter.BlitTexture(cmd, _tempLutHandle, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void ConfigureHDROutputLegacy(CameraData cameraData)
        {
            if (cameraData.isHDROutputActive)
            {
                ConfigureHDROutputInternal(cameraData.hdrDisplayColorGamut, cameraData.hdrDisplayInformation);
            }
        }

        #endregion

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            _tempLutHandle?.Release();
            _tempLutHandle = null;
        }
    }
}