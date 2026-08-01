namespace CustomToneMapping.Baker
{
    internal static class ValidationPrimitives
    {
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
