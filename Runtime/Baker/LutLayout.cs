namespace CustomToneMapping.Baker
{
    internal static class LutLayout
    {
        internal const int MinSize = 32;
        internal const int MaxSize = 65;

        internal static bool IsValidSize(int size) => size >= MinSize && size <= MaxSize;
        internal static int GetWidth(int size) => size * size;
        internal static int GetHeight(int size) => size;
    }
}
