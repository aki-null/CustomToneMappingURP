using System.Linq;
using System.Reflection;
using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using CustomToneMapping.URP;
using AgXMapper = CustomToneMapping.Baker.AgX.AgXToneMapping;
using GT7Mapper = CustomToneMapping.Baker.GT7.GT7ToneMapping;
using AgXVolume = CustomToneMapping.URP.AgXToneMapping;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CustomToneMappingVolume = CustomToneMapping.URP.CustomToneMapping;
using GTVolume = CustomToneMapping.URP.GT.GTToneMapping;
using GT7Volume = CustomToneMapping.URP.GT7ToneMapping;

namespace CustomToneMapping.Tests
{
    // HDR output shows LUT value * URP's paper white (TonemapHdr.hlsl), so HDR LUTs are baked for that paper white.
    // What must hold, in nits: the brightest output is the target peak, never above it. Below the shoulder the image
    // is the SDR image with SDR white at paper white. With paper white equal to SdrPaperWhite, GT and GT7 are the
    // reference HDR mapping. And the paper white is the one URP's LutBuilderHdr scaled by.
    public class PaperWhiteTests
    {
        const float Reference = 100f;
        static readonly float[] Peaks = { 250f, 400f, 1000f, 4000f, 10000f };
        static readonly float[] PaperWhites = { 80f, 203f, 300f, 400f };
        static readonly float[] SdrWhites = { 100f, 250f };

        // Scene-linear inputs across the LUT domain (LogC EI 1000 tops out near 55).
        static float[] Inputs(float max, int count) => Enumerable.Range(0, count).Select(i => max * i / (count - 1f)).ToArray();

        static GTConfig Gt(float peak, float paperWhite, float sdrWhite) => new()
        {
            TargetPeakNits = peak, IsHdrOutput = true, ReferenceLuminance = Reference, SdrPaperWhite = sdrWhite,
            PaperWhiteNits = paperWhite, Contrast = 1f, LinearSectionStart = 0.22f, LinearSectionLength = 0.4f,
            BlackTightness = 1.33f, BlackOffset = 0f, LutSize = 32
        };

        static GT7Config Gt7(float peak, float paperWhite, float sdrWhite, UcsMode ucs = UcsMode.ICtCp) => new()
        {
            TargetPeakNits = peak, IsHdrOutput = true, ReferenceLuminance = Reference, SdrPaperWhite = sdrWhite,
            PaperWhiteNits = paperWhite, Ucs = ucs, JzazbzExponentScaleFactor = 1.7f, CurveAlpha = 0.25f,
            CurveMidPoint = 0.538f, CurveLinearSection = 0.444f, CurveToeStrength = 1.28f, BlendRatio = 0.6f,
            FadeStart = 0.98f, FadeEnd = 1.16f, LutSize = 32
        };

        static AgXConfig AgX(float peak, float paperWhite, bool hdr = true) => new()
        {
            HdrMaxNits = peak, SdrMaxNits = paperWhite, HdrPurity = 0.5f, HdrExtraPowerFactor = 2f, IsHdrOutput = hdr,
            LookConfig = AgXLookConfig.GetPreset(AgXLookPreset.None), LutSize = 32
        };

        static float Gt(GTConfig c, bool hdr, float x)
        {
            var m = new GTToneMapping();
            if (hdr) m.InitializeAsHdr(c); else m.InitializeAsSdr(c);
            return m.ApplyToneMap(new float3(x)).x;
        }

        static float3 Gt7(GT7Config c, bool hdr, float3 x)
        {
            var m = new GT7Mapper();
            if (hdr) m.InitializeAsHdr(c); else m.InitializeAsSdr(c);
            return m.ApplyToneMap(x);
        }

        static float3 AgX(AgXConfig c, float3 x)
        {
            var m = new AgXMapper();
            m.Initialize(c);
            return m.ApplyToneMap(x);
        }

        // ---- GT ----

        [Test]
        public void GtPeakIsTheTargetPeak([ValueSource(nameof(Peaks))] float peak,
            [ValueSource(nameof(PaperWhites))] float paperWhite, [ValueSource(nameof(SdrWhites))] float sdrWhite)
        {
            var c = Gt(peak, paperWhite, sdrWhite);
            Assert.IsTrue(c.TryValidate(out var error), error);
            // The shoulder converges to its peak: exp(-C2 (x - S0) / P) underflows long before 1e6.
            Assert.That(Gt(c, true, 1e6f) * paperWhite, Is.EqualTo(peak).Within(peak * 1e-5f));
            var previous = 0f;
            foreach (var x in Inputs(60f, 4096))
            {
                var nits = Gt(c, true, x) * paperWhite;
                Assert.That(nits, Is.LessThanOrEqualTo(peak * (1f + 1e-5f)), $"x={x}");
                Assert.That(nits, Is.GreaterThanOrEqualTo(previous), $"monotonic at x={x}");
                previous = nits;
            }
        }

        // The toe and linear section do not depend on the curve's peak, so below both shoulders the HDR LUT holds
        // exactly the SDR values: SDR white is shown at paper white.
        [Test]
        public void GtMatchesSdrBelowTheShoulder([ValueSource(nameof(Peaks))] float peak,
            [ValueSource(nameof(PaperWhites))] float paperWhite, [ValueSource(nameof(SdrWhites))] float sdrWhite)
        {
            var hdr = Gt(peak, paperWhite, sdrWhite);
            var sdr = hdr;
            sdr.IsHdrOutput = false;
            var p = math.min(hdr.FramebufferTarget, sdr.FramebufferTarget);
            var shoulder = hdr.LinearSectionStart + (p - hdr.LinearSectionStart) * hdr.LinearSectionLength / hdr.Contrast;
            foreach (var x in Inputs(shoulder * 0.999f, 512))
                Assert.AreEqual(Gt(sdr, false, x), Gt(hdr, true, x), $"x={x}");
        }

        // GTReferenceData's HDR cases are the reference mapping (1.0 = ReferenceLuminance nits). SdrPaperWhite does not
        // enter the reference's HDR path, so every SDR white must reproduce it once paper white equals it.
        [Test]
        public void GtIsTheReferenceWhenPaperWhiteIsSdrWhite([Values(100f, 250f, 80f)] float white)
        {
            var cases = GTReferenceData.TestCases.Where(t => t.IsHdr).ToArray();
            Assert.That(cases.Length, Is.GreaterThan(0));
            foreach (var t in cases)
            {
                var actual = Gt(Gt(t.PeakNits, white, white), true, t.Input.x) * white / Reference;
                Assert.That(t.Input.x == t.Input.y && t.Input.y == t.Input.z, "GT is per channel; neutral cases only");
                Assert.That(actual, Is.EqualTo(t.Expected.x).Within(1e-4f * math.max(1f, t.Expected.x)), t.Description);
            }
        }

        // SDR is the HDR mapping on a display whose peak and paper white are both SDR white.
        [Test]
        public void GtSdrIsHdrOnASdrWhiteDisplay([ValueSource(nameof(SdrWhites))] float sdrWhite)
        {
            var hdr = Gt(sdrWhite, sdrWhite, sdrWhite);
            var sdr = hdr;
            sdr.IsHdrOutput = false;
            foreach (var x in Inputs(60f, 2048))
                Assert.That(Gt(hdr, true, x), Is.EqualTo(Gt(sdr, false, x)).Within(1e-6f), $"x={x}");
        }

        // ---- GT7 ----

        [Test]
        public void Gt7PeakIsTheTargetPeak([ValueSource(nameof(Peaks))] float peak,
            [ValueSource(nameof(PaperWhites))] float paperWhite, [ValueSource(nameof(SdrWhites))] float sdrWhite,
            [Values(UcsMode.ICtCp, UcsMode.JzAzBz)] UcsMode ucs)
        {
            var c = Gt7(peak, paperWhite, sdrWhite, ucs);
            Assert.IsTrue(c.TryValidate(out var error), error);
            var white = Gt7(c, true, new float3(1e6f)) * paperWhite;
            for (int i = 0; i < 3; i++) Assert.That(white[i], Is.EqualTo(peak).Within(peak * 1e-5f));
            // Saturated colors and every channel stay at or below the peak.
            foreach (var x in Inputs(60f, 512))
            foreach (var rgb in new[] { new float3(x), new float3(x, 0.1f * x, 0.02f * x), new float3(0.05f * x, 0.3f * x, x) })
            {
                var nits = Gt7(c, true, rgb) * paperWhite;
                Assert.That(math.cmax(nits), Is.LessThanOrEqualTo(peak * (1f + 1e-5f)), $"rgb={rgb}");
            }
        }

        // Below both shoulders the per-channel curve is the SDR one, so without the chroma step (BlendRatio 0) HDR
        // holds exactly the SDR values. The chroma step fades chroma by luminance relative to the peak, and Rec.2020
        // grey keeps a trace of chroma in the UCS (Jzazbz's LMS rows do not sum to one value), so with it grey
        // differs by that trace: GT7's own SDR/HDR difference, bounded by the reference tests' JzAzBz tolerance.
        [Test]
        public void Gt7MatchesSdrBelowTheShoulder([ValueSource(nameof(Peaks))] float peak,
            [ValueSource(nameof(PaperWhites))] float paperWhite, [ValueSource(nameof(SdrWhites))] float sdrWhite,
            [Values(UcsMode.ICtCp, UcsMode.JzAzBz)] UcsMode ucs)
        {
            var hdr = Gt7(peak, paperWhite, sdrWhite, ucs);
            var sdr = hdr;
            sdr.IsHdrOutput = false;
            var curveOnlyHdr = hdr;
            curveOnlyHdr.BlendRatio = 0f;
            var curveOnlySdr = sdr;
            curveOnlySdr.BlendRatio = 0f;
            var shoulder = hdr.CurveLinearSection * math.min(hdr.FramebufferTarget, sdr.FramebufferTarget);
            var worst = 0f;
            foreach (var x in Inputs(shoulder * 0.999f, 512).Skip(1))
            {
                var input = new float3(x);
                Assert.AreEqual(Gt7(curveOnlySdr, false, input), Gt7(curveOnlyHdr, true, input), $"curve at x={x}");
                var expected = Gt7(sdr, false, input);
                worst = math.max(worst, math.cmax(math.abs(Gt7(hdr, true, input) - expected) / expected));
            }
            TestContext.WriteLine($"worst relative difference with the chroma step {worst:G3}");
            Assert.That(worst, Is.LessThanOrEqualTo(5e-4f));
        }

        [Test]
        public void Gt7SdrIsHdrOnASdrWhiteDisplay([ValueSource(nameof(SdrWhites))] float sdrWhite)
        {
            var hdr = Gt7(sdrWhite, sdrWhite, sdrWhite);
            var sdr = hdr;
            sdr.IsHdrOutput = false;
            foreach (var x in Inputs(60f, 512))
            foreach (var rgb in new[] { new float3(x), new float3(x, 0.1f * x, 0.02f * x) })
                Assert.IsTrue(TestHelpers.AreVectorsEqual(Gt7(sdr, false, rgb), Gt7(hdr, true, rgb), 1e-6f), $"rgb={rgb}");
        }

        // ---- AgX ----

        // AgX's HDR output is clamped to peak / SDR white, and SDR white is paper white.
        [Test]
        public void AgXPeakIsTheTargetPeak([ValueSource(nameof(Peaks))] float peak,
            [ValueSource(nameof(PaperWhites))] float paperWhite)
        {
            if (peak < paperWhite) Assert.Pass("AgXBelowPaperWhiteScalesTheSdrImageToThePeak");
            var c = AgX(peak, paperWhite);
            Assert.IsTrue(c.TryValidate(out var error), error);
            var white = math.cmax(AgX(c, new float3(1e3f))) * paperWhite;
            TestContext.WriteLine($"white {white} nits of {peak}");
            Assert.That(white, Is.EqualTo(peak).Within(peak * 1e-4f));
            foreach (var x in Inputs(60f, 512))
            foreach (var rgb in new[] { new float3(x), new float3(x, 0.1f * x, 0.02f * x), new float3(0.05f * x, 0.3f * x, x) })
                Assert.That(math.cmax(AgX(c, rgb)) * paperWhite, Is.LessThanOrEqualTo(peak * (1f + 1e-6f)), $"rgb={rgb}");
        }

        // A display dimmer than paper white has no headroom: the SDR image, scaled so SDR white lands on the peak.
        [Test]
        public void AgXBelowPaperWhiteScalesTheSdrImageToThePeak([Values(100f, 250f)] float peak)
        {
            const float paperWhite = 300f;
            var hdr = AgX(peak, paperWhite);
            Assert.IsTrue(hdr.TryValidate(out var error), error);
            var sdr = AgX(peak, paperWhite, false);
            foreach (var x in Inputs(60f, 512))
            foreach (var rgb in new[] { new float3(x), new float3(x, 0.1f * x, 0.02f * x) })
                Assert.IsTrue(TestHelpers.AreVectorsEqual(AgX(sdr, rgb) * (peak / paperWhite), AgX(hdr, rgb), 1e-6f), $"rgb={rgb}");
        }

        // ---- Volumes ----

        // SDR bakes must not depend on URP's paper white, or every paper white change would rebake them.
        [Test]
        public void SdrConfigsIgnorePaperWhite()
        {
            var gt = ScriptableObject.CreateInstance<GTVolume>();
            var gt7 = ScriptableObject.CreateInstance<GT7Volume>();
            var agx = ScriptableObject.CreateInstance<AgXVolume>();
            try
            {
                Assert.IsTrue(gt.ToConfig(1000f, 203f, false, 32).Equals(gt.ToConfig(1000f, 300f, false, 32)));
                Assert.IsTrue(gt7.ToConfig(1000f, 203f, false, 32).Equals(gt7.ToConfig(1000f, 300f, false, 32)));
                Assert.IsTrue(agx.ToConfig(1000f, 203f, false, 32).Equals(agx.ToConfig(1000f, 300f, false, 32)));
                Assert.IsFalse(gt.ToConfig(1000f, 203f, true, 32).Equals(gt.ToConfig(1000f, 300f, true, 32)));
                Assert.IsFalse(gt7.ToConfig(1000f, 203f, true, 32).Equals(gt7.ToConfig(1000f, 300f, true, 32)));
                Assert.IsFalse(agx.ToConfig(1000f, 203f, true, 32).Equals(agx.ToConfig(1000f, 300f, true, 32)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gt);
                UnityEngine.Object.DestroyImmediate(gt7);
                UnityEngine.Object.DestroyImmediate(agx);
            }
        }
    }

    // The paper white the LUTs are baked for is the one URP's LutBuilderHdr scaled by, and the Renderer Feature's chain
    // shader turns URP's LUT into LUT value * paper white nits with it.
    public class PaperWhiteUrpTests
    {
        VolumeStack _stack, _previousStack;
        RenderTexture _target;
        bool _initializedManager;

        Tonemapping Tonemapping => _stack.GetComponent<Tonemapping>();

        [SetUp]
        public void SetUp()
        {
            if (!VolumeManager.instance.isInitialized) { VolumeManager.instance.Initialize(); _initializedManager = true; }
            _previousStack = VolumeManager.instance.stack;
            _stack = VolumeManager.instance.CreateStack();
            VolumeManager.instance.stack = _stack;
            Assume.That(Tonemapping, Is.Not.Null, "URP's Tonemapping component is in the stack only with URP active");
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

        static readonly HDROutputUtils.HDRDisplayInformation Display = new(800, 1000, 0, 180);

        // The resolver copies an internal URP method. This keeps it equal to the original.
        [Test]
        public void PaperWhiteMatchesUrp([Values] bool detectPaperWhite)
        {
            Assert.IsNotNull(UrpBridge.PaperWhiteResolver, "the Renderer Feature assembly registers it at load");
            var urp = typeof(UniversalRenderPipeline).GetMethod("GetHDROutputLuminanceParameters",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(urp, "UniversalRenderPipeline.GetHDROutputLuminanceParameters");

            Tonemapping.detectPaperWhite.value = detectPaperWhite;
            Tonemapping.paperWhite.value = 250f;

            var args = new object[] { Display, ColorGamut.Rec2020, Tonemapping, null };
            urp.Invoke(null, args);
            var expected = ((Vector4)args[3]).z;
            Assert.AreEqual(expected, UrpBridge.PaperWhiteNits(Display));
            Assert.AreEqual(detectPaperWhite ? 180f : 250f, expected);
        }

        // The whole HDR path of the Renderer Feature: URP's LUT (graded value * paper white) -> chain shader (undo, the
        // built-in LUT, * paper white) -> nits. White must land on the peak, and mid grey on SDR mid grey * paper white.
        [TestCase(ToneMappingMode.GT, 250f)]
        [TestCase(ToneMappingMode.GT, 400f)]
        [TestCase(ToneMappingMode.GT7, 250f)]
        [TestCase(ToneMappingMode.GT7, 400f)]
        [TestCase(ToneMappingMode.AgX, 250f)]
        [TestCase(ToneMappingMode.AgX, 400f)]
        public void ChainShaderShowsWhiteAtThePeakAndSdrWhiteAtPaperWhite(ToneMappingMode mode, float paperWhite)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var shader = Shader.Find("Hidden/CustomToneMapChain");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
            var material = new Material(shader);
            var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                var custom = _stack.GetComponent<CustomToneMappingVolume>();
                custom.mode.value = mode;
                custom.lutSize.value = 32;
                Tonemapping.detectPaperWhite.value = false;
                Tonemapping.paperWhite.value = paperWhite;

                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, Display));
                material.EnableKeyword("HDR_COLORSPACE_CONVERSION");
                material.EnableKeyword("LEGACY_RENDER_PATH");
                material.SetInteger("_HDRColorspace", 1); // Rec.2020: URP's LUT holds graded Rec.2020 * paper white
                var paperWhiteNits = UrpBridge.PaperWhiteNits(Display);
                material.SetVector("_HDROutputLuminanceParams", new Vector4(0, 1000, paperWhiteNits, 1f / paperWhiteNits));
                material.SetTexture("_MainTex", source);

                float Draw(float scene)
                {
                    source.SetPixel(0, 0, new Color(scene * paperWhite, scene * paperWhite, scene * paperWhite, 1));
                    source.Apply(false, false);
                    return Aces2TestSupport.Draw(material, _target).g;
                }

                // Detect peak is on by default: the display's 1000 nits.
                var white = Draw(1000f);
                var grey = Draw(0.18f);
                var sdrGrey = SdrMidGrey(mode);
                TestContext.WriteLine($"{mode} paper white {paperWhite}: white {white} nits, grey {grey} nits (SDR {sdrGrey * paperWhite})");
                Assert.That(white, Is.EqualTo(1000f).Within(1000f * 2e-3f), "white is the peak (half-float LUT)");
                if (mode != ToneMappingMode.AgX) // AgX's HDR sigmoid differs from its SDR one around mid grey
                    Assert.That(grey, Is.EqualTo(sdrGrey * paperWhite).Within(sdrGrey * paperWhite * 5e-3f), "grey is SDR grey at paper white");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // ACES 2.0 takes paper white as an exposure change before the Academy transform for the display peak, like
        // URP's own ACES. The peak stays the display peak whatever the paper white.
        [TestCase(1000, 250f)]
        [TestCase(1000, 400f)]
        [TestCase(250, 400f)]
        public void ChainShaderAppliesPaperWhiteAsAces2Exposure(int peak, float paperWhite)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            var material = new Material(Shader.Find("Hidden/CustomToneMapChain"));
            var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                _stack.GetComponent<CustomToneMappingVolume>().mode.value = ToneMappingMode.ACES2;
                var aces2 = _stack.GetComponent<Aces2ToneMapping>();
                aces2.detectPeakNits.value = false;
                aces2.targetPeakNits.value = peak;
                Tonemapping.detectPaperWhite.value = false;
                Tonemapping.paperWhite.value = paperWhite;

                Assert.AreEqual(MaterialPreparationStatus.Ready, UrpBridge.PrepareMaterialWithStatus(material, Display));
                material.EnableKeyword("HDR_COLORSPACE_CONVERSION");
                material.EnableKeyword("LEGACY_RENDER_PATH");
                material.SetInteger("_HDRColorspace", 1);
                material.SetVector("_HDROutputLuminanceParams", new Vector4(0, 1000, paperWhite, 1f / paperWhite));
                material.SetTexture("_MainTex", source);

                var p = Aces2TestSupport.Parameters(peak, true);
                foreach (var input in new[] { new Vector3(.18f, .18f, .18f), new Vector3(2, .5f, .1f), new Vector3(50, 50, 50) })
                {
                    source.SetPixel(0, 0, new Color(input.x * paperWhite, input.y * paperWhite, input.z * paperWhite, 1));
                    source.Apply(false, false);
                    var rec709 = math.mul(Aces2TestSupport.Rec2020ToRec709, Aces2TestSupport.ToDouble3(input));
                    var expected = Aces2TestSupport.Aces2FromRec709(rec709 * (paperWhite / 100.0), p) * 100;
                    var actual = Aces2TestSupport.Draw(material, _target);
                    TestContext.WriteLine($"input {input}: {actual.r:F2} {actual.g:F2} {actual.b:F2} nits, expected {expected}");
                    for (int c = 0; c < 3; c++)
                    {
                        Assert.That(actual[c], Is.EqualTo(expected[c]).Within(math.max(0.5, 2e-3 * expected[c])), $"input {input}, channel {c}");
                        Assert.That(actual[c], Is.LessThanOrEqualTo(peak * 1.001f), "never above the peak");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        float SdrMidGrey(ToneMappingMode mode)
        {
            switch (mode)
            {
                case ToneMappingMode.GT:
                {
                    var m = new GTToneMapping();
                    m.InitializeAsSdr(_stack.GetComponent<GTVolume>().ToConfig(1000f, 0f, false, 32));
                    return m.ApplyToneMap(new float3(0.18f)).y;
                }
                case ToneMappingMode.GT7:
                {
                    var m = new GT7Mapper();
                    m.InitializeAsSdr(_stack.GetComponent<GT7Volume>().ToConfig(1000f, 0f, false, 32));
                    return m.ApplyToneMap(new float3(0.18f)).y;
                }
                default:
                {
                    var m = new AgXMapper();
                    m.Initialize(_stack.GetComponent<AgXVolume>().ToConfig(1000f, 0f, false, 32));
                    return m.ApplyToneMap(new float3(0.18f)).y;
                }
            }
        }
    }
}
