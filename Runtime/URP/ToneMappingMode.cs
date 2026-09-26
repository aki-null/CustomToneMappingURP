using System;
using UnityEngine;

namespace CustomToneMapping.URP
{
    [Serializable]
    public enum ToneMappingMode
    {
        // Serialized as the value: never renumber. The inspector lists modes in declaration order.
        [InspectorName("None")] None = 0,
        [InspectorName("GT")] GT = 1,
        [InspectorName("GT7")] GT7 = 2,
        [InspectorName("AgX")] AgX = 3,
        [InspectorName("ACES 2.0")] ACES2 = 5,
        [InspectorName("Custom LUT")] CustomLUT = 4,
    }
}
