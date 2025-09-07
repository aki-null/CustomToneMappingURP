using Unity.Mathematics;

namespace CustomToneMapping.Baker
{
    public interface IToneMap
    {
        float3 ApplyToneMap(float3 rgb);

        bool IsHDROutput { get; }

        Colorspace InputColorspace { get; }
        Colorspace OutputColorspace { get; }
    }
}