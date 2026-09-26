using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;
using CustomToneMapping.URP;
using CustomToneMapping.URP.RendererFeatures;
using CustomToneMappingVolume = CustomToneMapping.URP.CustomToneMapping;

namespace CustomToneMapping.Tests
{
    // Copies internalColorLut into a caller-owned RenderTexture after the chain pass has run.
    internal sealed class LutCaptureFeature : ScriptableRendererFeature
    {
        public RenderTexture destination;
        RTHandle _handle;
        CapturePass _pass;

        public override void Create() => _pass = new CapturePass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data) { if (destination != null) renderer.EnqueuePass(_pass); }
        protected override void Dispose(bool disposing) { _handle?.Release(); _handle = null; }

        sealed class CapturePass : ScriptableRenderPass
        {
            readonly LutCaptureFeature _owner;
            public CapturePass(LutCaptureFeature owner) { _owner = owner; }
            sealed class Data { public TextureHandle source; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (!resources.internalColorLut.IsValid()) return;
                if (_owner._handle == null || _owner._handle.rt != _owner.destination) { _owner._handle?.Release(); _owner._handle = RTHandles.Alloc(_owner.destination); }
                var target = graph.ImportTexture(_owner._handle);
                using var builder = graph.AddRasterRenderPass<Data>("Capture grading LUT", out var data);
                data.source = resources.internalColorLut;
                builder.UseTexture(data.source);
                builder.SetRenderAttachment(target, 0);
                builder.SetRenderFunc((Data d, RasterGraphContext c) => Blitter.BlitTexture(c.cmd, d.source, new Vector4(1, 1, 0, 0), 0, false));
            }
        }
    }

    // Renders through a temporary URP asset carrying the real CustomToneMappingRendererFeature and compares every texel
    // of the grading LUT it produces with the managed Academy transform of URP's own tonemap-None grading LUT.
    public class Aces2RendererFeatureTests
    {
        [Test]
        public void FeatureAppliesAces2ToEveryTexelOfTheGradedLut()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            if (PlayerSettings.colorSpace != ColorSpace.Linear) Assert.Ignore("Requires a linear color space project.");
            const int size = 32;
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            UniversalRenderPipelineAsset asset = null; UniversalRendererData renderer = null;
            CustomToneMappingRendererFeature feature = null; LutCaptureFeature capture = null;
            GameObject cameraObject = null, volumeObject = null; VolumeProfile profile = null; RenderTexture target = null, lut = null;
            try
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                Assert.IsNotNull(renderer.postProcessData);
                feature = ScriptableObject.CreateInstance<CustomToneMappingRendererFeature>(); renderer.rendererFeatures.Add(feature);
                capture = ScriptableObject.CreateInstance<LutCaptureFeature>(); renderer.rendererFeatures.Add(capture);
                lut = new RenderTexture(size * size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); lut.Create(); capture.destination = lut;
                asset = UniversalRenderPipelineAsset.Create(renderer);
                asset.colorGradingMode = ColorGradingMode.HighDynamicRange; asset.colorGradingLutSize = size;
                GraphicsSettings.defaultRenderPipeline = asset; QualitySettings.renderPipeline = asset;

                cameraObject = new GameObject("ACES2 feature test camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 0; camera.allowHDR = true; camera.backgroundColor = new Color(1, .1f, .01f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = ~0;
                target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); target.Create(); camera.targetTexture = target;

                // A non-trivial grade, so the captured LUT differs from the identity in every channel.
                volumeObject = new GameObject("ACES2 feature test volume");
                var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                // URP tone mapping None, as the feature requires, whatever the project's default profile sets.
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
                var mode = profile.Add<CustomToneMappingVolume>(true); mode.mode.Override(ToneMappingMode.None);
                profile.Add<Aces2ToneMapping>(true);
                var adjustments = profile.Add<ColorAdjustments>(true); adjustments.contrast.Override(35); adjustments.saturation.Override(20);
                profile.Add<WhiteBalance>(true).temperature.Override(25);
                var curves = profile.Add<ColorCurves>(true); curves.red.value.AddKey(.5f, .65f); curves.red.overrideState = true;
                volume.sharedProfile = profile;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request); // warm-up: pipeline and volume stack come up
                volume.enabled = false; volume.enabled = true;
                VolumeManager.instance.Update(camera.transform, ~0);
                RenderPipeline.SubmitRenderRequest(camera, request);   // mode None: URP's graded, tonemap-None LUT
                var graded = Aces2TestSupport.ReadPixels(lut);

                mode.mode.Override(ToneMappingMode.ACES2);
                VolumeManager.instance.Update(camera.transform, ~0);
                RenderPipeline.SubmitRenderRequest(camera, request);   // the chain pass output
                var aces = Aces2TestSupport.ReadPixels(lut);
                Assert.AreEqual(size * size * size, graded.Length);
                Assert.AreEqual(graded.Length, aces.Length);

                var p = Aces2TestSupport.Parameters(100, false);
                float worst = 0; int over = 0;
                for (int i = 0; i < aces.Length; i++)
                {
                    Assert.IsTrue(float.IsFinite(aces[i].r + aces[i].g + aces[i].b), "finite ACES 2 output at texel " + i);
                    var expected = Aces2TestSupport.Aces2FromRec709(new double3(graded[i].r, graded[i].g, graded[i].b), p);
                    float d = 0;
                    for (int c = 0; c < 3; c++) d = Mathf.Max(d, Mathf.Abs(aces[i][c] - (float)expected[c]));
                    if (d > 2e-3f) over++;
                    worst = Mathf.Max(worst, d);
                }
                TestContext.WriteLine($"texels={aces.Length} maxAbsDiff={worst:G6} texelsOver2e-3={over}");
                Assert.That(over, Is.Zero, "the Renderer Feature's LUT diverged from the managed Academy reference");
                Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Hidden/CustomToneMapChain")));
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = previousPipeline; QualitySettings.renderPipeline = previousQuality;
                foreach (var o in new UnityEngine.Object[] { cameraObject, volumeObject, target, lut, profile, feature, capture, asset, renderer })
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }
}
