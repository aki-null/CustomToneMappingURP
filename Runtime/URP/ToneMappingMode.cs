using System;
using UnityEngine;

namespace CustomToneMapping.URP
{
    [Serializable]
    public enum ToneMappingMode
    {
        [InspectorName("None")] None = 0,
        [InspectorName("GT")] GT,
        [InspectorName("GT7")] GT7,
        [InspectorName("AgX")] AgX,
        [InspectorName("Custom LUT")] CustomLUT,
    }
}