using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using CustomToneMapping.Baker;
using CustomToneMapping.Baker.ACES2;
using CustomToneMapping.URP;
using CustomToneMappingVolume = CustomToneMapping.URP.CustomToneMapping;

namespace CustomToneMapping.Tests
{
    // How ACES 2.0 hooks into URP: the bridge's routing by keyword layout (driven through the volume stack, as URP's
    // patched call sites drive it), the TonemapParams.hlsl hooks in URP's ACES branch, the Renderer Feature's chain
    // shader under HDR output, and the LDR strip. Fixture shaders declare the keyword layouts that stock URP lacks.
    public class Aces2UrpTests
    {
        const string LutBuilder = "Hidden/CustomToneMapping/Tests/ACES2LutBuilder";             // 1.3 layout: URP's tonemap set plus _CUSTOM_TONEMAP_ACES2
        const string LutBuilderLegacy = "Hidden/CustomToneMapping/Tests/ACES2LutBuilderLegacy"; // pre-1.3 layout
        const string Uber = "Hidden/CustomToneMapping/Tests/ACES2Uber";                         // UberPost's set (_HDR_GRADING)
        const string Hook = "Hidden/CustomToneMapping/Tests/ACES2Hook";                         // LutBuilderHdr's include order and ACES calls

        VolumeStack _stack, _previousStack;
        RenderTexture _target;
        bool _initializedManager;

        CustomToneMappingVolume Mode => _stack.GetComponent<CustomToneMappingVolume>();
        Aces2ToneMapping Aces2 => _stack.GetComponent<Aces2ToneMapping>();

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            // EditMode tests run without a render pipeline instance. The package's volume components have no
            // pipeline restriction, so initializing VolumeManager without a pipeline still loads them.
            if (!VolumeManager.instance.isInitialized) { VolumeManager.instance.Initialize(); _initializedManager = true; }
            _previousStack = VolumeManager.instance.stack;
            _stack = VolumeManager.instance.CreateStack();
            VolumeManager.instance.stack = _stack;
            Assert.IsNotNull(Mode, "CustomToneMapping component in the stack");
            Mode.mode.value = ToneMappingMode.ACES2;
            Mode.lutSize.value = 32;
            Aces2.acesAwareGrading.value = true;
            UrpBridge.ResetFailureState();
            _target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Assert.IsTrue(_target.Create());
        }

        [TearDown]
        public void TearDown()
        {
            VolumeManager.instance.stack = _previousStack;
            if (_stack != null) { VolumeManager.instance.DestroyStack(_stack); _stack = null; }
            if (_initializedManager) { VolumeManager.instance.Deinitialize(); _initializedManager = false; }
            UrpBridge.ClearCache();
            if (_target != null) UnityEngine.Object.DestroyImmediate(_target);
        }

        static Material Fixture(string name)
        {
            var shader = Shader.Find(name);
            Assert.IsNotNull(shader, name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), name);
            return new Material(shader);
        }

        // Fixture output: 1 = _TONEMAP_ACES, 2 = _TONEMAP_CUSTOM, 4 = _CUSTOM_TONEMAP_ACES2 in the selected variant.
        int Variant(Material material) => Mathf.RoundToInt(Aces2TestSupport.Draw(material, _target).r);

        [Test]
        public void LutBuilderWithTheKeywordFollowsTheAcesAwareToggle()
        {
            var material = Fixture(LutBuilder);
            try
            {
                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(5, Variant(material), "URP's ACES branch plus the hook keyword");
                Assert.AreEqual(1, material.GetInteger("_CustomTonemapMode"));

                Aces2.acesAwareGrading.value = false;
                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(2, Variant(material), "toggle off: standard grading through _TONEMAP_CUSTOM");

                Mode.mode.value = ToneMappingMode.None;
                Assert.AreEqual(MaterialPreparationStatus.Disabled, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(0, Variant(material), "mode None clears every package keyword");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void LutBuilderWithoutTheKeywordGradesInStandardSpacesAndNoticesOnce()
        {
            var material = Fixture(LutBuilderLegacy);
            int notices = 0;
            Application.LogCallback handler = (message, _, type) => { if (type == LogType.Warning && message.Contains("standard spaces")) notices++; };
            Application.logMessageReceived += handler;
            try
            {
                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(2, Variant(material), "no ACES-aware grading without the keyword");
                Assert.AreEqual(1, material.GetInteger("_CustomTonemapMode"));
                UrpBridge.PrepareMaterialWithStatus(material, null);
                UrpBridge.PrepareMaterialWithStatus(material, null);
                Assert.AreEqual(1, notices, "the notice fires once per session");

                UrpBridge.ResetFailureState();
                Aces2.acesAwareGrading.value = false;
                UrpBridge.PrepareMaterialWithStatus(material, null);
                Assert.AreEqual(1, notices, "no notice when ACES-aware grading is not requested");
            }
            finally { Application.logMessageReceived -= handler; UnityEngine.Object.DestroyImmediate(material); }
        }

        // LDR grading cannot grade in ACES spaces, so ACES-aware grading gets the notice once.
        [Test]
        public void UberMaterialGetsTheLdrStripOnceBaked()
        {
            var material = Fixture(Uber);
            int notices = 0;
            Application.LogCallback handler = (message, _, type) => { if (type == LogType.Warning && message.Contains("HDR Color Grading")) notices++; };
            Application.logMessageReceived += handler;
            try
            {
                // Baked on the CPU inside the request, so the very first frame is already tone mapped.
                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(2, Variant(material), "LDR: the RGB LUT path");
                Assert.AreEqual(0, material.GetInteger("_CustomTonemapMode"));
                Assert.AreEqual(new Vector3(1f / (32 * 32), 1f / 32, 31), (Vector3)material.GetVector("_CustomTonemap_Params"));
                UrpBridge.PrepareMaterialWithStatus(material, null);
                Assert.AreEqual(1, notices, "the notice fires once per session");
            }
            finally { Application.logMessageReceived -= handler; UnityEngine.Object.DestroyImmediate(material); }
        }

        // The chain shader declares neither ACES keyword, so it grades in standard spaces without the notice meant
        // for an old URP customization.
        [Test]
        public void ChainMaterialNeverGetsAcesGrading()
        {
            var material = Fixture("Hidden/CustomToneMapChain");
            int notices = 0;
            Application.LogCallback handler = (message, _, type) => { if (type == LogType.Warning && message.Contains("standard spaces")) notices++; };
            Application.logMessageReceived += handler;
            try
            {
                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, null));
                Assert.AreEqual(1, material.GetInteger("_CustomTonemapMode"));
                Assert.IsFalse(material.IsKeywordEnabled("_TONEMAP_ACES"));
                Assert.AreEqual(0, notices);
            }
            finally { Application.logMessageReceived -= handler; UnityEngine.Object.DestroyImmediate(material); }
        }

        // The hooks in TonemapParams.hlsl: URP's two ACES output calls evaluate ACES 2.0 only in variants of the added keyword.
        [TestCase(false, TestName = "HookedAcesCallsEvaluateAces2InSdr")]
        [TestCase(true, TestName = "HookedAcesCallsEvaluateAces2InHdr")]
        public void HookedAcesCallsEvaluateAces2(bool hdr)
        {
            var material = Fixture(Hook);
            int peak = hdr ? 1000 : 100;
            var p = Aces2TestSupport.Parameters(peak, hdr);
            Texture2D atlas = null;
            try
            {
                atlas = Aces2LutBaker.Bake(new Aces2Config(peak, hdr), out var constants);
                Aces2LutBaker.Bind(material, atlas, constants);
                material.SetInteger("_CustomTonemapMode", 1);
                material.SetInteger("_HDRColorspace", 1);
                material.SetVector("_HDROutputLuminanceParams", new Vector4(0, peak, 203, 1f / peak));
                material.SetVector("_HDROutputGradingParams", new Vector4(hdr ? 3 : 0, 0, 0, 0)); // HDRRANGEREDUCTION_ACES1000NITS for the unhooked control
                if (hdr) material.EnableKeyword("HDR_COLORSPACE_CONVERSION");
                material.EnableKeyword("_TONEMAP_ACES");
                foreach (var input in new[] { new Vector3(.18f, .18f, .18f), new Vector3(1, .1f, .01f), new Vector3(.05f, .4f, 3) })
                {
                    material.SetVector("_TestInput", input);
                    var expected = Aces2TestSupport.Aces2FromAP1(input, p);
                    if (hdr) expected *= 100; // nits

                    material.EnableKeyword("_CUSTOM_TONEMAP_ACES2");
                    var hooked = Aces2TestSupport.Draw(material, _target);
                    for (int c = 0; c < 3; c++) Assert.That(hooked[c], Is.EqualTo(expected[c]).Within(hdr ? 0.5 : 2e-3), $"hooked, hdr={hdr}, input {input}, channel {c}");

                    material.DisableKeyword("_CUSTOM_TONEMAP_ACES2");
                    var stock = Aces2TestSupport.Draw(material, _target);
                    Assert.IsTrue(float.IsFinite(stock.r + stock.g + stock.b), "unhooked variant renders URP's ACES");
                    float difference = 0;
                    for (int c = 0; c < 3; c++) difference = Mathf.Max(difference, Mathf.Abs(stock[c] - (float)expected[c]));
                    Assert.That(difference, Is.GreaterThan(hdr ? 1f : 5e-3f), $"unhooked variant must be URP's ACES, not ACES 2 (hdr={hdr}, input {input})");
                }
            }
            finally { CoreUtils.Destroy(atlas); UnityEngine.Object.DestroyImmediate(material); }
        }

        // The Renderer Feature's chain shader under HDR output. URP's LUT holds graded values rotated to the output
        // space and scaled by paper white. The pass must undo that, apply ACES 2.0, and return nits in the output space.
        [TestCase(1, TestName = "ChainShaderOutputsNitsInRec2020")]
        [TestCase(0, TestName = "ChainShaderOutputsNitsInRec709")]
        public void ChainShaderOutputsNitsInTheOutputSpace(int hdrColorspace)
        {
            var material = Fixture("Hidden/CustomToneMapChain");
            const int peak = 1000;
            const float paperWhite = 203;
            var p = Aces2TestSupport.Parameters(peak, true);
            Texture2D atlas = null;
            var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                atlas = Aces2LutBaker.Bake(new Aces2Config(peak, true), out var constants);
                Aces2LutBaker.Bind(material, atlas, constants);
                material.SetInteger("_CustomTonemapMode", 1);
                material.EnableKeyword("HDR_COLORSPACE_CONVERSION");
                material.EnableKeyword("LEGACY_RENDER_PATH"); // sample _MainTex instead of the framebuffer attachment
                material.SetInteger("_HDRColorspace", hdrColorspace);
                material.SetVector("_HDROutputLuminanceParams", new Vector4(0, peak, paperWhite, 1f / peak));
                material.SetTexture("_MainTex", source);
                foreach (var input in new[] { new Vector3(.18f, .18f, .18f), new Vector3(2, .5f, .1f), new Vector3(.05f, .4f, 3), new Vector3(30, 30, 30) })
                {
                    var rec2020 = Aces2TestSupport.ToDouble3(input);
                    var rec709 = math.mul(Aces2TestSupport.Rec2020ToRec709, rec2020);
                    var stored = hdrColorspace == 1 ? rec2020 : rec709;
                    source.SetPixel(0, 0, new Color((float)stored[0] * paperWhite, (float)stored[1] * paperWhite, (float)stored[2] * paperWhite, 1));
                    source.Apply(false, false);

                    var aces = Aces2TestSupport.Aces2FromRec709(rec709, p);
                    var expected = hdrColorspace == 1 ? aces : math.mul(Aces2TestSupport.Rec2020ToRec709, aces);
                    var actual = Aces2TestSupport.Draw(material, _target);
                    for (int c = 0; c < 3; c++) Assert.That(actual[c], Is.EqualTo(expected[c] * 100).Within(0.5), $"nits, input {input}, colorspace {hdrColorspace}, channel {c}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); CoreUtils.Destroy(atlas); UnityEngine.Object.DestroyImmediate(material); }
        }

        // The LDR strip URP's per-pixel grading samples: ACES 2.0 SDR at every node.
        [TestCase(32)]
        public void LdrStripMatchesTheManagedTransformAtNodes(int n)
        {
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBakeAces2Strip(n, out var strip, out _));
            Assert.AreEqual(n * n, strip.width);
            Assert.AreEqual(n, strip.height);
            var texels = strip.GetPixels();
            var p = Aces2TestSupport.Parameters(100, false);
            float worst = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n * n; x += 7)
            {
                // Strip layout read by ApplyLut2D / written by GetLutStripValue: r = x % n, g = y, b = x / n.
                var node = LutBaker.AlexaLogC.LogCToLinear(new float3((x % n) / (n - 1f), y / (n - 1f), (x / n) / (n - 1f)));
                var rgb = math.max(new double3(node.x, node.y, node.z), 0);
                var expected = Aces2TestSupport.Aces2FromRec709(rgb, p);
                var texel = texels[y * n * n + x];
                for (int c = 0; c < 3; c++) worst = Mathf.Max(worst, Mathf.Abs(texel[c] - (float)expected[c]));
            }
            TestContext.WriteLine($"n={n} maxAbsDiff={worst:G6}");
            Assert.That(worst, Is.LessThanOrEqualTo(2e-3f));
        }
    }
}
