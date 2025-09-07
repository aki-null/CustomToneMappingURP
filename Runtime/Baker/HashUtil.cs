using System.Runtime.CompilerServices;

namespace CustomToneMapping.Baker
{
    internal static class HashUtil
    {
        public const uint Fnv1A32Offset = 2166136261u;
        private const uint Fnv1A32Prime = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash32(uint seed, uint value)
        {
            unchecked
            {
                seed ^= value;
                seed *= Fnv1A32Prime;
                return seed;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash32(uint seed, int value) => Hash32(seed, unchecked((uint)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash32(uint seed, float value) => Hash32(seed, AsUInt(value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint AsUInt(float f)
        {
            return *(uint*)&f;
        }
    }
}