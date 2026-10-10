namespace CustomToneMapping.Baker
{
    internal static class LutLayout
    {
        internal const int MinSize = 32;
        internal const int MaxSize = 65;

        internal static bool TryValidateSize(int size, out string error)
        {
            error = size >= MinSize && size <= MaxSize ? null : $"LUT size must be between {MinSize} and {MaxSize}.";
            return error == null;
        }
        internal static int GetWidth(int size) => size * size;
        internal static int GetHeight(int size) => size;
    }
}
