using NUnit.Framework;
using CustomToneMapping.URP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomToneMapping.Tests
{
    public class CustomLutValidationTests
    {
        // Validation reads the texture every time, so a resized or destroyed LUT is judged by what it is now.
        [Test]
        public void ValidatesTheCurrentTexture()
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(UrpBridge.TryValidateCustomLut(texture, out var error));
                Assert.IsNull(error);

                Assert.IsTrue(texture.Reinitialize(3, 2));
                Assert.IsFalse(UrpBridge.TryValidateCustomLut(texture, out error));
                Assert.IsNotNull(error);

                Assert.IsTrue(texture.Reinitialize(16, 4));
                Assert.IsTrue(UrpBridge.TryValidateCustomLut(texture, out _));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            Assert.IsFalse(UrpBridge.TryValidateCustomLut(texture, out var destroyedError));
            Assert.IsNotNull(destroyedError);
        }
    }
}
