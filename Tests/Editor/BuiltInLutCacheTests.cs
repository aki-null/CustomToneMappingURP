using System;
using System.Collections;
using CustomToneMapping.Baker;
using CustomToneMapping.Baker.ACES2;
using CustomToneMapping.Baker.AgX;
using CustomToneMapping.Baker.GT;
using CustomToneMapping.Baker.GT7;
using CustomToneMapping.URP;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
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

        // Tick only counts a frame whose frameCount differs from the last one, so tests drive their own frame numbers.
        private static int _frameCount = 1 << 20;

        private static void AdvanceFrames(int frames)
        {
            for (var i = 0; i < frames; i++)
                BuiltInLutCache.Tick(++_frameCount);
        }

        [Test]
        public void InvalidConfigBakesNothing()
        {
            var config = CreateConfig(0.0f);
            config.Contrast = float.NaN;

            var status = BuiltInLutCache.GetOrBake(config, out var texture, out var error);

            Assert.AreEqual(MaterialPreparationStatus.Invalid, status);
            Assert.IsNull(texture);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ReusesReadyLutForIdenticalConfig()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);

            Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(config, out var first, out var firstError));
            Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(config, out var second, out var secondError));

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.IsNull(firstError);
            Assert.IsNull(secondError);
        }

        [Test]
        public void InvalidRequestDoesNotEvictReadyLuts()
        {
            AssumeLutBakingSupported();
            var configs = new GTConfig[BuiltInLutCache.Capacity];
            var textures = new Texture2D[configs.Length];
            for (var i = 0; i < configs.Length; i++)
            {
                configs[i] = CreateConfig(i * 0.01f);
                Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(configs[i], out textures[i], out _));
            }

            var invalid = CreateConfig(0.25f);
            invalid.Contrast = float.NaN;
            Assert.AreEqual(MaterialPreparationStatus.Invalid, BuiltInLutCache.GetOrBake(invalid, out _, out _));

            for (var i = 0; i < configs.Length; i++)
            {
                Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(configs[i], out var texture, out _));
                Assert.AreSame(textures[i], texture);
            }
        }

        [Test]
        public void ReplacesTheLeastRecentlyUsedConfig()
        {
            AssumeLutBakingSupported();
            var configs = new GTConfig[BuiltInLutCache.Capacity + 1];
            var textures = new Texture2D[configs.Length];
            for (var i = 0; i < configs.Length; i++)
                configs[i] = CreateConfig(i * 0.01f);
            for (var i = 0; i < BuiltInLutCache.Capacity; i++)
            {
                BuiltInLutCache.GetOrBake(configs[i], out textures[i], out _);
                AdvanceFrames(1);
            }

            // Touch the oldest, so the second oldest is the one replaced.
            BuiltInLutCache.GetOrBake(configs[0], out _, out _);
            AdvanceFrames(1);
            BuiltInLutCache.GetOrBake(configs[BuiltInLutCache.Capacity], out _, out _);

            BuiltInLutCache.GetOrBake(configs[0], out var kept, out _);
            Assert.AreSame(textures[0], kept);
            BuiltInLutCache.GetOrBake(configs[1], out var rebaked, out _);
            Assert.AreNotSame(textures[1], rebaked);
        }

        [Test]
        public void ModesDoNotEvictEachOther()
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBake(CreateGT7Config(32), out var gt7Texture, out _);
            for (var i = 0; i <= BuiltInLutCache.Capacity; i++)
                BuiltInLutCache.GetOrBake(CreateConfig(i * 0.01f), out _, out _);

            BuiltInLutCache.GetOrBake(CreateGT7Config(32), out var gt7Again, out _);
            Assert.AreSame(gt7Texture, gt7Again);
        }

        [UnityTest]
        public IEnumerator WarmReadyHitDoesNotBakeOrAllocateManagedMemory()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(config, out _, out _));

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
                    BuiltInLutCache.Tick(++_frameCount);
                    if (BuiltInLutCache.GetOrBake(config, out _, out _) != MaterialPreparationStatus.Ready)
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
        public void ClearDestroysCacheOwnedLuts()
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var texture, out _);
            Assert.IsNotNull(texture);

            UrpBridge.ClearCache();

            Assert.IsTrue(texture == null);
            Assert.IsNull(BuiltInLutCache.GetCachedLut(ToneMappingMode.GT));
        }

        [Test]
        public void ExternallyDestroyedLutIsRebaked()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            BuiltInLutCache.GetOrBake(config, out var texture, out _);
            UnityEngine.Object.DestroyImmediate(texture);

            Assert.IsNull(BuiltInLutCache.GetCachedLut(ToneMappingMode.GT));
            Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(config, out var rebaked, out _));
            Assert.IsTrue(rebaked != null);
        }

        [Test]
        public void LutExpiresAfterItsStaleLifetime()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            BuiltInLutCache.GetOrBake(config, out var texture, out _);

            AdvanceFrames(BuiltInLutCache.StaleLifetimeFrames);
            Assert.IsTrue(texture != null);
            Assert.AreSame(texture, BuiltInLutCache.GetCachedLut(ToneMappingMode.GT));

            AdvanceFrames(1);
            Assert.IsTrue(texture == null);
            Assert.IsNull(BuiltInLutCache.GetCachedLut(ToneMappingMode.GT));

            Assert.AreEqual(MaterialPreparationStatus.Ready, BuiltInLutCache.GetOrBake(config, out var replacement, out _));
            Assert.IsTrue(replacement != null);
        }

        // Only frames that tone map call Tick. Repeated calls in one frame (several cameras) count once, so no number
        // of calls ages an entry without frames passing.
        [Test]
        public void TicksWithinOneFrameCountOnce()
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var texture, out _);
            AdvanceFrames(BuiltInLutCache.StaleLifetimeFrames);
            for (var i = 0; i < 10; i++)
                BuiltInLutCache.Tick(_frameCount);

            Assert.IsTrue(texture != null);
        }

        [Test]
        public void UseRefreshesAge()
        {
            AssumeLutBakingSupported();
            var config = CreateConfig(0.0f);
            BuiltInLutCache.GetOrBake(config, out var texture, out _);
            AdvanceFrames(BuiltInLutCache.StaleLifetimeFrames);

            BuiltInLutCache.GetOrBake(config, out var touched, out _);
            AdvanceFrames(1);

            Assert.IsTrue(texture != null);
            Assert.AreSame(texture, touched);
        }

        [Test]
        public void PurgeKeepsRecentlyUsedLutsOfEveryMode()
        {
            AssumeLutBakingSupported();
            var gt7 = CreateGT7Config(32);
            BuiltInLutCache.GetOrBake(CreateConfig(0.0f), out var gtTexture, out _);
            BuiltInLutCache.GetOrBake(gt7, out var gt7Texture, out _);
            BuiltInLutCache.GetOrBake(CreateAgXConfig(32), out var agxTexture, out _);
            AdvanceFrames(BuiltInLutCache.StaleLifetimeFrames);

            BuiltInLutCache.GetOrBake(gt7, out _, out _);
            AdvanceFrames(1);

            Assert.IsTrue(gtTexture == null);
            Assert.IsTrue(agxTexture == null);
            Assert.IsTrue(gt7Texture != null);
            Assert.AreSame(gt7Texture, BuiltInLutCache.GetCachedLut(ToneMappingMode.GT7));
            Assert.IsNull(BuiltInLutCache.GetCachedLut(ToneMappingMode.GT));
            Assert.IsNull(BuiltInLutCache.GetCachedLut(ToneMappingMode.AgX));
        }

        // The ACES 2.0 LDR strip is too slow to rebake after a mode switch, so aging keeps the most recent one. Strips
        // of other LUT sizes still expire, so trying sizes does not pile them up.
        [Test]
        public void AgingKeepsOnlyTheMostRecentLdrStrip()
        {
            AssumeLutBakingSupported();
            BuiltInLutCache.GetOrBakeAces2Strip(32, out var older, out _);
            AdvanceFrames(1);
            BuiltInLutCache.GetOrBakeAces2Strip(33, out var current, out _);
            AdvanceFrames(BuiltInLutCache.StaleLifetimeFrames * 2);

            Assert.IsTrue(older == null);
            Assert.IsTrue(current != null);
            Assert.AreSame(current, BuiltInLutCache.GetCachedLut(ToneMappingMode.ACES2));
        }

        [Test]
        public void PersistentFailureIsLoggedOnce()
        {
            LogAssert.Expect(LogType.Warning, "Custom tone mapping disabled for GT: bad contrast.");
            UrpBridge.LogFailure(ToneMappingMode.GT, MaterialPreparationStatus.Invalid, "bad contrast");
            UrpBridge.LogFailure(ToneMappingMode.GT, MaterialPreparationStatus.Invalid, "bad contrast");
            LogAssert.NoUnexpectedReceived();
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
