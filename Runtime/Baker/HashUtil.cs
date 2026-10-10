using System.Runtime.CompilerServices;

namespace CustomToneMapping.Baker
{
    internal static class HashUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FloatBitsEqual(float left, float right) => AsUInt(left) == AsUInt(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint AsUInt(float f)
        {
            return *(uint*)&f;
        }
    }
}