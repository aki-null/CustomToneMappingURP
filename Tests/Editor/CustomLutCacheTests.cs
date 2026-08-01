using System;
using NUnit.Framework;
using CustomToneMapping.URP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomToneMapping.Tests
{
    public class CustomLutCacheTests
    {
        private Texture2D _first;
        private Texture2D _second;

        [SetUp]
        public void SetUp()
        {
            UrpBridge.ClearCache();
            _first = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _second = new Texture2D(3, 2, TextureFormat.RGBA32, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            UrpBridge.ClearCache();
        }

        [Test]
        public void AlternatingInvalidLutsReportOnlyOncePerTexture()
        {
            var first = CustomLutCache.TryGetOrValidate(_first, out _, out var firstError,
                out var firstShouldReport);
            var second = CustomLutCache.TryGetOrValidate(_second, out _, out var secondError,
                out var secondShouldReport);
            var firstAgain = CustomLutCache.TryGetOrValidate(_first, out _, out var firstAgainError,
                out var firstAgainShouldReport);
            var secondAgain = CustomLutCache.TryGetOrValidate(_second, out _, out var secondAgainError,
                out var secondAgainShouldReport);

            Assert.IsFalse(first);
            Assert.IsFalse(second);
            Assert.IsFalse(firstAgain);
            Assert.IsFalse(secondAgain);
            Assert.IsNotNull(firstError);
            Assert.IsNotNull(secondError);
            Assert.AreEqual(firstError, firstAgainError);
            Assert.AreEqual(secondError, secondAgainError);
            Assert.IsTrue(firstShouldReport);
            Assert.IsTrue(secondShouldReport);
            Assert.IsFalse(firstAgainShouldReport);
            Assert.IsFalse(secondAgainShouldReport);
        }

        [Test]
        public void SameFrameAccessUsesTrueLru()
        {
            var textures = new Texture2D[5];
            for (var i = 0; i < textures.Length; i++)
                textures[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                for (var i = 0; i < 4; i++)
                    Assert.IsFalse(CustomLutCache.TryGetOrValidate(textures[i], out _, out _, out _));

                Assert.IsFalse(CustomLutCache.TryGetOrValidate(textures[0], out _, out _,
                    out var firstTouchShouldReport));
                Assert.IsFalse(firstTouchShouldReport);

                Assert.IsFalse(CustomLutCache.TryGetOrValidate(textures[4], out _, out _,
                    out var fifthShouldReport));
                Assert.IsTrue(fifthShouldReport);

                Assert.IsFalse(CustomLutCache.TryGetOrValidate(textures[0], out _, out _,
                    out var firstAfterEvictionShouldReport));
                Assert.IsFalse(firstAfterEvictionShouldReport);

                Assert.IsFalse(CustomLutCache.TryGetOrValidate(textures[1], out _, out _,
                    out var secondAfterEvictionShouldReport));
                Assert.IsTrue(secondAfterEvictionShouldReport);
            }
            finally
            {
                for (var i = 0; i < textures.Length; i++)
                    Object.DestroyImmediate(textures[i]);
            }
        }

        [Test]
        public void ClearDoesNotDestroyCustomTextures()
        {
            CustomLutCache.TryGetOrValidate(_first, out _, out _, out _);
            CustomLutCache.TryGetOrValidate(_second, out _, out _, out _);
            CustomLutCache.ClearCache();

            Assert.IsNotNull(_first);
            Assert.IsNotNull(_second);
        }

        [Test]
        public void ResizedTextureIsRevalidated()
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(CustomLutCache.TryGetOrValidate(texture, out var firstSample,
                    out _, out var firstShouldReport));
                Assert.IsFalse(firstShouldReport);
                Assert.AreEqual(new Vector3(0.25f, 0.5f, 1.0f), firstSample);

                Assert.IsTrue(texture.Reinitialize(16, 4));
                Assert.IsTrue(CustomLutCache.TryGetOrValidate(texture, out var resizedSample,
                    out _, out var resizedShouldReport));
                Assert.IsFalse(resizedShouldReport);
                Assert.AreEqual(new Vector3(1.0f / 16.0f, 1.0f / 4.0f, 3.0f), resizedSample);

                Assert.IsTrue(texture.Reinitialize(3, 2));
                Assert.IsFalse(CustomLutCache.TryGetOrValidate(texture, out _, out _,
                    out var invalidShouldReport));
                Assert.IsTrue(invalidShouldReport);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void DestroyedTexturesAreRevalidatedIndependently()
        {
            var first = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            var second = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(CustomLutCache.TryGetOrValidate(first, out _, out _, out _));
            Assert.IsTrue(CustomLutCache.TryGetOrValidate(second, out _, out _, out _));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);

            Assert.IsFalse(CustomLutCache.TryGetOrValidate(first, out _, out _,
                out var firstShouldReport));
            Assert.IsTrue(firstShouldReport);
            Assert.IsFalse(CustomLutCache.TryGetOrValidate(first, out _, out _,
                out var firstAgainShouldReport));
            Assert.IsFalse(firstAgainShouldReport);

            Assert.IsFalse(CustomLutCache.TryGetOrValidate(second, out _, out _,
                out var secondShouldReport));
            Assert.IsTrue(secondShouldReport);
        }

        [Test]
        public void WarmCustomHitsAllocateNoManagedMemory()
        {
            Assert.IsFalse(CustomLutCache.TryGetOrValidate(_first, out _, out _, out _));
            Assert.IsFalse(CustomLutCache.TryGetOrValidate(_first, out _, out _, out _));

            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 128; i++)
                CustomLutCache.TryGetOrValidate(_first, out _, out _, out _);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }
    }
}
