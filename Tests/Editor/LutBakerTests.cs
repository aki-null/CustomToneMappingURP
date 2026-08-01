using CustomToneMapping.Baker;
using NUnit.Framework;
using UnityEngine.Experimental.Rendering;

namespace CustomToneMapping.Tests
{
    public class LutBakerTests
    {
        [Test]
        public void HdrFormatNeverFallsBackToUnorm()
        {
            if (LutBaker.TryChooseFormat(true, out var format))
                Assert.AreNotEqual(GraphicsFormat.R8G8B8A8_UNorm, format);
        }

    }
}
