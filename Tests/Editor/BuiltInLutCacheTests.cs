using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CustomToneMapping.Baker;
using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using CustomToneMapping.URP;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CustomToneMapping.Tests
{
    public class BuiltInLutCacheTests
    {
        [SetUp]
        public void SetUp()
        {
            UrpBridge.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            UrpBridge.ClearCache();
        }

        [Test]
        public void ReusesFailureForIdenticalNonFiniteConfig()
        {
            var config = new GTConfig
            {
                TargetPeakNits = 1000.0f,
                IsHdrOutput = false,
                ReferenceLuminance = 100.0f,
                SdrPaperWhite = 100.0f,
                Contrast = float.NaN,
                LinearSectionStart = 0.22f,
                LinearSectionLength = 0.4f,
                BlackTightness = 1.33f,
                BlackOffset = 0.0f,
                LutSize = 32
            };

            var first = BuiltInLutCache.GetOrBake(config, out var firstTexture,
                out _, out var firstError, out var firstShouldReport);
            var second = BuiltInLutCache.GetOrBake(config, out var secondTexture,
                out _, out var secondError, out var secondShouldReport);

            Assert.AreEqual(MaterialPreparationStatus.Invalid, first);
            Assert.AreEqual(MaterialPreparationStatus.Invalid, second);
            Assert.IsNull(firstTexture);
            Assert.IsNull(secondTexture);
            Assert.IsNotNull(firstError);
            Assert.AreEqual(firstError, secondError);
            Assert.IsTrue(firstShouldReport);
            Assert.IsFalse(secondShouldReport);
        }

        [Test]
        public void ReusesReadyLutForIdenticalConfig()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);

            var firstStatus = BuiltInLutCache.GetOrBake(config, out var firstTexture,
                out var firstParams, out var firstError, out var firstShouldReport);
            var secondStatus = BuiltInLutCache.GetOrBake(config, out var secondTexture,
                out var secondParams, out var secondError, out var secondShouldReport);

            Assert.AreEqual(MaterialPreparationStatus.Ready, firstStatus);
            Assert.AreEqual(MaterialPreparationStatus.Ready, secondStatus);
            Assert.IsNotNull(firstTexture);
            Assert.AreSame(firstTexture, secondTexture);
            Assert.AreEqual(firstParams, secondParams);
            Assert.IsNull(firstError);
            Assert.IsNull(secondError);
            Assert.IsFalse(firstShouldReport);
            Assert.IsFalse(secondShouldReport);
        }

        [Test]
        public void InvalidRequestDoesNotEvictReadyLuts()
        {
            AssumeLutBakingSupported();
            var configs = new GTConfig[4];
            var textures = new Texture2D[4];

            for (var i = 0; i < configs.Length; i++)
            {
                configs[i] = CreateConfig(i * 0.01f);
                var status = BuiltInLutCache.GetOrBake(configs[i], out textures[i],
                    out _, out _, out _);
                Assert.AreEqual(MaterialPreparationStatus.Ready, status);
            }

            var invalid = CreateConfig(0.25f);
            invalid.Contrast = float.NaN;
            var invalidStatus = BuiltInLutCache.GetOrBake(invalid, out _, out _,
                out _, out var shouldReport);

            Assert.AreEqual(MaterialPreparationStatus.Invalid, invalidStatus);
            Assert.IsTrue(shouldReport);

            for (var i = 0; i < configs.Length; i++)
            {
                var status = BuiltInLutCache.GetOrBake(configs[i], out var texture,
                    out _, out _, out var hitShouldReport);
                Assert.AreEqual(MaterialPreparationStatus.Ready, status);
                Assert.AreSame(textures[i], texture);
                Assert.IsFalse(hitShouldReport);
            }
        }

        [Test]
        public void ReadyLruIsGlobalAcrossRequests()
        {
            AssumeLutBakingSupported();
            var configs = new GTConfig[5];
            var textures = new Texture2D[5];

            for (var i = 0; i < 4; i++)
            {
                configs[i] = CreateConfig(i * 0.01f, i == 1 ? 33 : 32);
                var status = BuiltInLutCache.GetOrBake(configs[i], out textures[i],
                    out _, out _, out _);
                Assert.AreEqual(MaterialPreparationStatus.Ready, status);
            }

            var touchedStatus = BuiltInLutCache.GetOrBake(configs[0], out var touchedTexture,
                out _, out _, out _);
            Assert.AreEqual(MaterialPreparationStatus.Ready, touchedStatus);
            Assert.AreSame(textures[0], touchedTexture);

            configs[4] = CreateConfig(0.04f, 32);
            var fifthStatus = BuiltInLutCache.GetOrBake(configs[4], out textures[4],
                out _, out _, out _);
            Assert.AreEqual(MaterialPreparationStatus.Ready, fifthStatus);

            var reloadedStatus = BuiltInLutCache.GetOrBake(configs[1], out var reloadedTexture,
                out _, out _, out _);
            Assert.AreEqual(MaterialPreparationStatus.Ready, reloadedStatus);
            Assert.AreNotSame(textures[1], reloadedTexture);
        }

        [Test]
        public void ReadyLruIsGlobalAcrossMapperKinds()
        {
            AssumeLutBakingSupported();
            var gt = CreateConfig(0.0f, 32);
            var gt7 = CreateGT7Config(33);
            var agx = CreateAgXConfig(32);
            var gtSecond = CreateConfig(0.01f, 32);

            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gt, out _, out _, out _, out _));
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gt7, out var gt7Texture, out _, out _, out _));
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(agx, out _, out _, out _, out _));
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gtSecond, out _, out _, out _, out _));

            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gt, out _, out _, out _, out _));

            var fifth = CreateConfig(0.02f, 32);
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(fifth, out _, out _, out _, out _));

            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gt7, out var reloadedGt7Texture,
                    out _, out _, out _));
            Assert.AreNotSame(gt7Texture, reloadedGt7Texture);
        }

        [UnityTest]
        public IEnumerator WarmReadyHitDoesNotBakeOrAllocateManagedMemory()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(config, out _, out _, out _, out _));

            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal,
                       "CustomToneMapping.BakeLUT", 16))
            {
                yield return null;
                var bakeSamplesBefore = recorder.Valid ? recorder.LastValue : 0;
                GC.Collect();
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

                var allReady = true;
                for (var i = 0; i < 128; i++)
                {
                    if (BuiltInLutCache.GetOrBake(config, out _, out _, out _, out _) !=
                        MaterialPreparationStatus.Ready)
                        allReady = false;
                }

                var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                yield return null;
                var bakeSamplesAfter = recorder.Valid ? recorder.LastValue : 0;

                Assert.IsTrue(recorder.Valid, "Bake profiler marker was not available.");
                Assert.IsTrue(allReady);
                Assert.AreEqual(bakeSamplesBefore, bakeSamplesAfter);
                Assert.AreEqual(allocatedBefore, allocatedAfter);
            }
        }

        [Test]
        public void ClearDestroysCacheOwnedReadyLut()
        {
            AssumeLutBakingSupported();
            var status = BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var texture,
                out _, out _, out _);
            Assert.AreEqual(MaterialPreparationStatus.Ready, status);
            Assert.IsNotNull(texture);

            UrpBridge.ClearCache();

            Assert.IsTrue(texture == null);
        }

        [Test]
        public void ReadyLutExpiresAfterTenUnusedFrames()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            var frame = Time.frameCount;
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(config, out var texture, out var sample, out _, out _));

            BuiltInLutCache.PurgeUnused(frame + 10);
            Assert.IsTrue(texture != null);
            Assert.AreSame(texture, BuiltInLutCache.CachedLutTexture);

            BuiltInLutCache.PurgeUnused(frame + 11);
            Assert.IsTrue(texture == null);
            Assert.IsNull(BuiltInLutCache.CachedLutTexture);

            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(config, out var replacement, out var newSample, out _, out _));
            Assert.IsTrue(replacement != null);
            Assert.AreNotSame(texture, replacement);
            Assert.AreEqual(sample, newSample);
        }

        [Test]
        public void ReadyHitRefreshesFrameAge()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            BuiltInLutCache.GetOrBake(config, out var texture, out _, out _, out _);
            AgeReadyLuts(10);

            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(config, out var touched, out _, out _, out _));
            BuiltInLutCache.PurgeUnused(Time.frameCount + 1);

            Assert.IsTrue(texture != null);
            Assert.AreSame(texture, touched);
            Assert.AreSame(texture, BuiltInLutCache.CachedLutTexture);
        }

        [Test]
        public void PurgePreservesRecentlyUsedLutsAcrossMapperKinds()
        {
            AssumeLutBakingSupported();
            var gt7 = CreateGT7Config(32);
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var gtTexture, out _, out _, out _);
            BuiltInLutCache.GetOrBake(gt7, out var gt7Texture, out _, out _, out _);
            BuiltInLutCache.GetOrBake(CreateAgXConfig(32), out var agxTexture, out _, out _, out _);
            AgeReadyLuts(11);

            // Refresh an entry other than the last-slot shortcut.
            Assert.AreEqual(MaterialPreparationStatus.Ready,
                BuiltInLutCache.GetOrBake(gt7, out var touched, out _, out _, out _));
            BuiltInLutCache.PurgeUnused(Time.frameCount);

            Assert.IsTrue(gtTexture == null);
            Assert.IsTrue(agxTexture == null);
            Assert.IsTrue(gt7Texture != null);
            Assert.AreSame(gt7Texture, touched);
            Assert.AreSame(gt7Texture, BuiltInLutCache.CachedLutTexture);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EndContextCleanupRemainsRegisteredAfterClear(bool exitingPlayMode)
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out _, out _, out _, out _);
            if (exitingPlayMode)
                ((Action)GetCacheSubscriber(typeof(Application), "quitting"))();
            else
                UrpBridge.ClearCache();
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var texture, out _, out _, out _);
            AgeReadyLuts(11);

            // Invoke only this cache's registered subscriber, without rendering or
            // calling unrelated subscribers with a synthetic render context.
            var cleanup = (Action<ScriptableRenderContext, List<Camera>>)
                GetCacheSubscriber(typeof(RenderPipelineManager), "endContextRendering");
            cleanup(default, null);

            Assert.IsTrue(texture == null, "Cleanup must not require another LUT request.");
            Assert.IsNull(BuiltInLutCache.CachedLutTexture);
        }

        [Test]
        public void PurgeDoesNotResetFailureSuppression()
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var texture, out _, out _, out _);
            var invalid = CreateConfig(0.0f);
            invalid.Contrast = float.NaN;
            BuiltInLutCache.GetOrBake(invalid, out _, out _, out var error, out var shouldReport);
            Assert.IsTrue(shouldReport);

            BuiltInLutCache.PurgeUnused(Time.frameCount + 11);
            Assert.IsTrue(texture == null);
            Assert.AreEqual(MaterialPreparationStatus.Invalid,
                BuiltInLutCache.GetOrBake(invalid, out _, out _, out var repeatedError, out shouldReport));
            Assert.AreEqual(error, repeatedError);
            Assert.IsFalse(shouldReport);
        }

        private static Delegate GetCacheSubscriber(Type eventOwner, string eventName)
        {
            var callbacks = (Delegate)eventOwner
                .GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            Assert.IsNotNull(callbacks);
            Delegate subscriber = null;
            foreach (var callback in callbacks.GetInvocationList())
            {
                if (callback.Method.DeclaringType != typeof(BuiltInLutCache))
                    continue;

                Assert.IsNull(subscriber, "Cache cleanup must be registered only once.");
                subscriber = callback;
            }

            Assert.IsNotNull(subscriber);
            return subscriber;
        }

        private static void AgeReadyLuts(int frames)
        {
            // Edit-mode frame advancement depends on editor rendering. Age the
            // stored timestamps instead of adding a test clock to the runtime.
            var entries = (Array)typeof(BuiltInLutCache)
                .GetField("ReadyEntries", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            var frameField = entries.GetType().GetElementType().GetField("LastUsedFrame");
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries.GetValue(i);
                frameField.SetValue(entry, unchecked(Time.frameCount - frames));
                entries.SetValue(entry, i);
            }
        }

        private static GTConfig CreateConfig(float blackOffset, int lutSize = 32)
        {
            return new GTConfig
            {
                TargetPeakNits = 1000.0f,
                IsHdrOutput = false,
                ReferenceLuminance = 100.0f,
                SdrPaperWhite = 100.0f,
                Contrast = 1.0f,
                LinearSectionStart = 0.22f,
                LinearSectionLength = 0.4f,
                BlackTightness = 1.33f,
                BlackOffset = blackOffset,
                LutSize = lutSize
            };
        }

        private static GT7Config CreateGT7Config(int lutSize)
        {
            return new GT7Config
            {
                TargetPeakNits = 1000.0f,
                IsHdrOutput = false,
                ReferenceLuminance = 100.0f,
                SdrPaperWhite = 250.0f,
                Ucs = UcsMode.ICtCp,
                JzazbzExponentScaleFactor = 1.7f,
                CurveAlpha = 0.25f,
                CurveMidPoint = 0.538f,
                CurveLinearSection = 0.444f,
                CurveToeStrength = 1.280f,
                BlendRatio = 0.6f,
                FadeStart = 0.98f,
                FadeEnd = 1.16f,
                LutSize = lutSize
            };
        }

        private static AgXConfig CreateAgXConfig(int lutSize)
        {
            return new AgXConfig
            {
                HdrMaxNits = 1000.0f,
                SdrMaxNits = 100.0f,
                HdrPurity = 0.5f,
                HdrExtraPowerFactor = 1.0f,
                IsHdrOutput = false,
                LookConfig = AgXLookConfig.GetPreset(AgXLookPreset.None),
                LutSize = lutSize
            };
        }

        private static void AssumeLutBakingSupported()
        {
            Assume.That(LutBaker.TryChooseFormat(false, out _), Is.True,
                "The current graphics device does not support LUT baking.");
        }
    }
}
