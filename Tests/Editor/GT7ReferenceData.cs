using Unity.Mathematics;

namespace CustomToneMapping.Tests
{
    public static class GT7ReferenceData
    {
        public struct TestCase
        {
            public readonly string ConfigName;
            public readonly string Description;
            public readonly float3 Input;
            public readonly float3 Expected;
            public readonly bool IsHdr;
            public readonly float PeakNits;
            public readonly int UcsMode;

            public TestCase(string configName, string description, float3 input, float3 expected,
                bool isHdr, float peakNits, int ucsMode)
            {
                ConfigName = configName;
                Description = description;
                Input = input;
                Expected = expected;
                IsHdr = isHdr;
                PeakNits = peakNits;
                UcsMode = ucsMode;
            }
        }

        public static readonly TestCase[] TestCases =
        {
            new(
                "SDR_ICtCp", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(4.000115e-01f, 4.000100e-01f, 4.000094e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(5.795093e-02f, 5.795071e-02f, 5.795062e-02f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(1.999455e-01f, 1.999448e-01f, 1.999445e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(3.999899e-01f, 3.624298e-06f, 0.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(3.385948e-06f, 4.000017e-01f, 6.108853e-07f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(1.250654e-06f, 1.753041e-06f, 3.999867e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(3.999995e-01f, 4.000026e-01f, 7.673165e-07f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(3.999939e-01f, 2.178772e-06f, 3.999929e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(0.000000e+00f, 4.000103e-01f, 3.999990e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(5.713872e-01f, 7.101475e-02f, 3.256869e-02f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(3.395285e-02f, 3.167411e-01f, 4.748995e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(4.785385e-01f, 3.591169e-01f, 3.440318e-02f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(7.267386e-01f, 7.267359e-01f, 7.267348e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(1.996307e-01f, 4.907024e-01f, 2.995641e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(1.000000e+00f, 1.000000e+00f, 7.387621e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(6.878040e-05f, 6.878014e-05f, 6.878003e-05f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(3.609444e-06f, 3.609430e-06f, 3.609424e-06f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(2.738086e-11f, 2.738076e-11f, 2.738071e-11f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(2.999945e-01f, 2.199552e-01f, 1.797247e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(3.400285e-01f, 2.599884e-01f, 2.199966e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(7.310817e-02f, 2.358323e-01f, 3.367188e-02f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(1.588756e-01f, 2.396202e-01f, 3.594408e-01f),
                false, 1.000000e+02f, 0
            ),
            new(
                "SDR_ICtCp", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(3.588859e-01f, 1.993189e-01f, 7.438301e-02f),
                false, 1.000000e+02f, 0
            ),
            new(
                "HDR1000_ICtCp", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000029e+00f, 1.000025e+00f, 1.000023e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448773e-01f, 1.448768e-01f, 1.448766e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.998638e-01f, 4.998619e-01f, 4.998612e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999748e-01f, 9.060745e-06f, 0.000000e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(8.464870e-06f, 1.000004e+00f, 1.527213e-06f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(3.126636e-06f, 4.382603e-06f, 9.999666e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(9.999987e-01f, 1.000006e+00f, 1.918291e-06f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999846e-01f, 5.446929e-06f, 9.999822e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(0.000000e+00f, 1.000026e+00f, 9.999975e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.459687e+00f, 1.801211e-01f, 8.280696e-02f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.493643e-02f, 7.919970e-01f, 1.188333e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.197706e+00f, 8.981881e-01f, 8.606534e-02f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(2.000051e+00f, 2.000043e+00f, 2.000040e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(4.979366e+00f, 4.979346e+00f, 4.979339e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(8.351075e+00f, 8.351043e+00f, 8.351029e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998313e-01f, 1.230008e+00f, 7.499883e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(1.000000e+01f, 1.000000e+01f, 6.706637e+00f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.719510e-04f, 1.719503e-04f, 1.719501e-04f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(9.023609e-06f, 9.023574e-06f, 9.023560e-06f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.845216e-11f, 6.845189e-11f, 6.845179e-11f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.499862e-01f, 5.498881e-01f, 4.493116e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500713e-01f, 6.499711e-01f, 5.499914e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.827704e-01f, 5.895807e-01f, 8.417969e-02f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.971889e-01f, 5.990506e-01f, 8.986019e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR1000_ICtCp", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.972148e-01f, 4.982973e-01f, 1.859575e-01f),
                true, 1.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000029e+00f, 1.000025e+00f, 1.000023e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448773e-01f, 1.448768e-01f, 1.448766e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.998638e-01f, 4.998619e-01f, 4.998612e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999748e-01f, 9.060745e-06f, 0.000000e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(8.464870e-06f, 1.000004e+00f, 1.527213e-06f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(3.126636e-06f, 4.382603e-06f, 9.999666e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(9.999987e-01f, 1.000006e+00f, 1.918291e-06f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999846e-01f, 5.446929e-06f, 9.999822e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(0.000000e+00f, 1.000026e+00f, 9.999975e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.459687e+00f, 1.801211e-01f, 8.280696e-02f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.493643e-02f, 7.919970e-01f, 1.188333e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.197706e+00f, 8.981881e-01f, 8.606534e-02f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(2.000051e+00f, 2.000043e+00f, 2.000040e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(5.000118e+00f, 5.000098e+00f, 5.000091e+00f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000016e+01f, 1.000012e+01f, 1.000010e+01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(4.000000e+01f, 4.000000e+01f, 4.000000e+01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998313e-01f, 1.230008e+00f, 7.499883e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.136302e+01f, 3.006841e+01f, 4.000000e+01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(4.000000e+01f, 4.000000e+01f, 2.384731e+01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.719510e-04f, 1.719503e-04f, 1.719501e-04f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(9.023609e-06f, 9.023574e-06f, 9.023560e-06f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.845216e-11f, 6.845189e-11f, 6.845179e-11f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.499862e-01f, 5.498881e-01f, 4.493116e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500713e-01f, 6.499711e-01f, 5.499914e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.827704e-01f, 5.895807e-01f, 8.417969e-02f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.971889e-01f, 5.990506e-01f, 8.986019e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR4000_ICtCp", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.972148e-01f, 4.982973e-01f, 1.859575e-01f),
                true, 4.000000e+03f, 0
            ),
            new(
                "HDR10000_ICtCp", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000029e+00f, 1.000025e+00f, 1.000023e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448773e-01f, 1.448768e-01f, 1.448766e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.998638e-01f, 4.998619e-01f, 4.998612e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999748e-01f, 9.060745e-06f, 0.000000e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(8.464870e-06f, 1.000004e+00f, 1.527213e-06f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(3.126636e-06f, 4.382603e-06f, 9.999666e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(9.999987e-01f, 1.000006e+00f, 1.918291e-06f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999846e-01f, 5.446929e-06f, 9.999822e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(0.000000e+00f, 1.000026e+00f, 9.999975e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.459687e+00f, 1.801211e-01f, 8.280696e-02f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.493643e-02f, 7.919970e-01f, 1.188333e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.197706e+00f, 8.981881e-01f, 8.606534e-02f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(2.000051e+00f, 2.000043e+00f, 2.000040e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(5.000118e+00f, 5.000098e+00f, 5.000091e+00f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000016e+01f, 1.000012e+01f, 1.000010e+01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(8.351598e+01f, 8.351566e+01f, 8.351553e+01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998313e-01f, 1.230008e+00f, 7.499883e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.228758e+01f, 3.423659e+01f, 5.639126e+01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(9.173119e+01f, 6.802202e+01f, 4.257394e+01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.719510e-04f, 1.719503e-04f, 1.719501e-04f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(9.023609e-06f, 9.023574e-06f, 9.023560e-06f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.845216e-11f, 6.845189e-11f, 6.845179e-11f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.499862e-01f, 5.498881e-01f, 4.493116e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500713e-01f, 6.499711e-01f, 5.499914e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.827704e-01f, 5.895807e-01f, 8.417969e-02f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.971889e-01f, 5.990506e-01f, 8.986019e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "HDR10000_ICtCp", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.972148e-01f, 4.982973e-01f, 1.859575e-01f),
                true, 1.000000e+04f, 0
            ),
            new(
                "SDR_JzAzBz", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(4.000132e-01f, 3.999845e-01f, 3.999930e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(5.794787e-02f, 5.795110e-02f, 5.795372e-02f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(1.999630e-01f, 1.999249e-01f, 1.999483e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(3.999865e-01f, 9.971205e-06f, 3.253849e-07f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(1.806899e-05f, 3.999905e-01f, 1.359878e-06f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(-5.423455e-07f, 5.124999e-07f, 3.999985e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(4.000080e-01f, 3.999864e-01f, 2.815606e-06f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(3.999996e-01f, 5.036443e-06f, 4.000073e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(2.301954e-05f, 3.999723e-01f, 4.000170e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(5.780030e-01f, 7.072025e-02f, 3.218164e-02f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(3.315745e-02f, 3.149309e-01f, 4.727589e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(4.780787e-01f, 3.587640e-01f, 3.422868e-02f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(7.266955e-01f, 7.267141e-01f, 7.267070e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(1.996691e-01f, 4.908524e-01f, 2.996162e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(1.000000e+00f, 1.000000e+00f, 7.160200e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(6.867271e-05f, 6.884617e-05f, 6.890382e-05f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(3.593005e-06f, 3.619715e-06f, 3.627638e-06f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(2.618330e-11f, 2.813241e-11f, 2.869246e-11f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(2.999329e-01f, 2.199737e-01f, 1.796940e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(3.400257e-01f, 2.599763e-01f, 2.200009e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(7.174217e-02f, 2.333997e-01f, 3.264698e-02f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(1.587590e-01f, 2.394158e-01f, 3.592115e-01f),
                false, 1.000000e+02f, 1
            ),
            new(
                "SDR_JzAzBz", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(3.586325e-01f, 1.990833e-01f, 7.420496e-02f),
                false, 1.000000e+02f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000033e+00f, 9.999613e-01f, 9.999824e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448697e-01f, 1.448777e-01f, 1.448843e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.999076e-01f, 4.998123e-01f, 4.998707e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999661e-01f, 2.492801e-05f, 8.134623e-07f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(4.517247e-05f, 9.999763e-01f, 3.399694e-06f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(-1.355864e-06f, 1.281250e-06f, 9.999963e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(1.000020e+00f, 9.999658e-01f, 7.039016e-06f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999989e-01f, 1.259111e-05f, 1.000018e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(5.754884e-05f, 9.999309e-01f, 1.000042e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.477526e+00f, 1.809753e-01f, 8.297597e-02f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.295057e-02f, 7.875327e-01f, 1.183036e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.196756e+00f, 8.974212e-01f, 8.570149e-02f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(1.999987e+00f, 2.000082e+00f, 2.000011e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(4.979417e+00f, 4.979339e+00f, 4.979247e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(8.352077e+00f, 8.351066e+00f, 8.351873e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998279e-01f, 1.230034e+00f, 7.499502e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(1.000000e+01f, 1.000000e+01f, 6.445668e+00f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.716818e-04f, 1.721154e-04f, 1.722595e-04f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(8.982513e-06f, 9.049289e-06f, 9.069096e-06f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.545825e-11f, 7.033102e-11f, 7.173114e-11f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.498323e-01f, 5.499342e-01f, 4.492351e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500643e-01f, 6.499407e-01f, 5.500022e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.793554e-01f, 5.834993e-01f, 8.161745e-02f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.968976e-01f, 5.985396e-01f, 8.980287e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR1000_JzAzBz", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.965814e-01f, 4.977081e-01f, 1.855124e-01f),
                true, 1.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000033e+00f, 9.999613e-01f, 9.999824e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448697e-01f, 1.448777e-01f, 1.448843e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.999076e-01f, 4.998123e-01f, 4.998707e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999661e-01f, 2.492801e-05f, 8.134623e-07f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(4.517247e-05f, 9.999763e-01f, 3.399694e-06f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(-1.355864e-06f, 1.281250e-06f, 9.999963e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(1.000020e+00f, 9.999658e-01f, 7.039016e-06f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999989e-01f, 1.259111e-05f, 1.000018e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(5.754884e-05f, 9.999309e-01f, 1.000042e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.477526e+00f, 1.809753e-01f, 8.297597e-02f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.295057e-02f, 7.875327e-01f, 1.183036e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.196756e+00f, 8.974212e-01f, 8.570149e-02f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(1.999987e+00f, 2.000082e+00f, 2.000011e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(4.999870e+00f, 5.000178e+00f, 4.999953e+00f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000049e+01f, 9.999618e+00f, 1.000032e+01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(4.000000e+01f, 4.000000e+01f, 4.000000e+01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998279e-01f, 1.230034e+00f, 7.499502e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.118073e+01f, 2.992685e+01f, 4.000000e+01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(4.000000e+01f, 3.971378e+01f, 2.338791e+01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.716818e-04f, 1.721154e-04f, 1.722595e-04f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(8.982513e-06f, 9.049289e-06f, 9.069096e-06f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.545825e-11f, 7.033102e-11f, 7.173114e-11f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.498323e-01f, 5.499342e-01f, 4.492351e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500643e-01f, 6.499407e-01f, 5.500022e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.793554e-01f, 5.834993e-01f, 8.161745e-02f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.968976e-01f, 5.985396e-01f, 8.980287e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR4000_JzAzBz", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.965814e-01f, 4.977081e-01f, 1.855124e-01f),
                true, 4.000000e+03f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Black",
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(0.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "White",
                new float3(1.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(1.000033e+00f, 9.999613e-01f, 9.999824e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "18% Gray",
                new float3(1.800000e-01f, 1.800000e-01f, 1.800000e-01f),
                new float3(1.448697e-01f, 1.448777e-01f, 1.448843e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "50% Gray",
                new float3(5.000000e-01f, 5.000000e-01f, 5.000000e-01f),
                new float3(4.999076e-01f, 4.998123e-01f, 4.998707e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Red",
                new float3(1.000000e+00f, 0.000000e+00f, 0.000000e+00f),
                new float3(9.999661e-01f, 2.492801e-05f, 8.134623e-07f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Green",
                new float3(0.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(4.517247e-05f, 9.999763e-01f, 3.399694e-06f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Blue",
                new float3(0.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(-1.355864e-06f, 1.281250e-06f, 9.999963e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Yellow",
                new float3(1.000000e+00f, 1.000000e+00f, 0.000000e+00f),
                new float3(1.000020e+00f, 9.999658e-01f, 7.039016e-06f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Magenta",
                new float3(1.000000e+00f, 0.000000e+00f, 1.000000e+00f),
                new float3(9.999989e-01f, 1.259111e-05f, 1.000018e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Cyan",
                new float3(0.000000e+00f, 1.000000e+00f, 1.000000e+00f),
                new float3(5.754884e-05f, 9.999309e-01f, 1.000042e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Vivid Red",
                new float3(1.500000e+00f, 2.000000e-01f, 1.000000e-01f),
                new float3(1.477526e+00f, 1.809753e-01f, 8.297597e-02f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Sky Blue",
                new float3(1.000000e-01f, 8.000000e-01f, 1.200000e+00f),
                new float3(8.295057e-02f, 7.875327e-01f, 1.183036e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Racing Yellow",
                new float3(1.200000e+00f, 9.000000e-01f, 1.000000e-01f),
                new float3(1.196756e+00f, 8.974212e-01f, 8.570149e-02f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "2x Overexposure",
                new float3(2.000000e+00f, 2.000000e+00f, 2.000000e+00f),
                new float3(1.999987e+00f, 2.000082e+00f, 2.000011e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "5x Overexposure",
                new float3(5.000000e+00f, 5.000000e+00f, 5.000000e+00f),
                new float3(4.999870e+00f, 5.000178e+00f, 4.999953e+00f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "10x Overexposure",
                new float3(1.000000e+01f, 1.000000e+01f, 1.000000e+01f),
                new float3(1.000049e+01f, 9.999618e+00f, 1.000032e+01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "100x Overexposure",
                new float3(1.000000e+02f, 1.000000e+02f, 1.000000e+02f),
                new float3(8.348679e+01f, 8.354471e+01f, 8.352686e+01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Complex Color 1",
                new float3(5.000000e-01f, 1.230000e+00f, 7.500000e-01f),
                new float3(4.998279e-01f, 1.230034e+00f, 7.499502e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Complex Color 2",
                new float3(1.230000e+01f, 3.430000e+01f, 5.690000e+01f),
                new float3(1.226341e+01f, 3.421538e+01f, 5.634517e+01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Complex Color 3",
                new float3(1.504700e+03f, 6.451000e+01f, 5.000000e-01f),
                new float3(9.402905e+01f, 7.133776e+01f, 4.670826e+01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Near Black",
                new float3(1.000000e-03f, 1.000000e-03f, 1.000000e-03f),
                new float3(1.716818e-04f, 1.721154e-04f, 1.722595e-04f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Very Near Black",
                new float3(1.000000e-04f, 1.000000e-04f, 1.000000e-04f),
                new float3(8.982513e-06f, 9.049289e-06f, 9.069096e-06f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Extremely Small",
                new float3(1.000000e-08f, 1.000000e-08f, 1.000000e-08f),
                new float3(6.545825e-11f, 7.033102e-11f, 7.173114e-11f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Skin Tone 1",
                new float3(7.500000e-01f, 5.500000e-01f, 4.500000e-01f),
                new float3(7.498323e-01f, 5.499342e-01f, 4.492351e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Skin Tone 2",
                new float3(8.500000e-01f, 6.500000e-01f, 5.500000e-01f),
                new float3(8.500643e-01f, 6.499407e-01f, 5.500022e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Grass Green",
                new float3(2.000000e-01f, 6.000000e-01f, 1.000000e-01f),
                new float3(1.793554e-01f, 5.834993e-01f, 8.161745e-02f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Sky Blue Day",
                new float3(4.000000e-01f, 6.000000e-01f, 9.000000e-01f),
                new float3(3.968976e-01f, 5.985396e-01f, 8.980287e-01f),
                true, 1.000000e+04f, 1
            ),
            new(
                "HDR10000_JzAzBz", "Sunset Orange",
                new float3(9.000000e-01f, 5.000000e-01f, 2.000000e-01f),
                new float3(8.965814e-01f, 4.977081e-01f, 1.855124e-01f),
                true, 1.000000e+04f, 1
            )
        };
    }
}