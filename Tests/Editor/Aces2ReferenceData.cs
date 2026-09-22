// Frozen with the unmodified Academy CTL interpreter (e280b6c4cf49726914260f1cf0cb2949d8db1027).
// ACES source: 069b0bc3e1f6c62820f19fdae2fecec3f4fc0f80. AP1 input; linear output before peak clamp.
// Regenerate with Tools/ACES2~/generate-fixtures.cjs and ACES_CTL_CHECK set.
using UnityEngine;
namespace CustomToneMapping.Tests
{
    internal static class Aces2ReferenceData
    {
        internal static readonly Vector3[] Inputs = {
            new Vector3(0.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.180000000f, 0.180000000f, 0.180000000f),
            new Vector3(1.00000000f, 1.00000000f, 1.00000000f),
            new Vector3(1.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.00000000f, 1.00000000f, 0.00000000f),
            new Vector3(0.00000000f, 0.00000000f, 1.00000000f),
            new Vector3(5.84350000f, 5.98480000f, 0.00979000000f),
            new Vector3(29.5500000f, 29.5500000f, 0.00000000f),
            new Vector3(100.000000f, 0.00000000f, 0.00000000f),
            new Vector3(1000.00000f, 1000.00000f, 1000.00000f),
            new Vector3(-0.100000000f, 0.500000000f, 1.00000000f),
            new Vector3(16.0000000f, 16.0000000f, 0.00000000f),
            new Vector3(56.9193800f, 0.00903940000f, 62.2590100f),
            new Vector3(187.191000f, 0.149501400f, 252.875300f)
        };
        internal static readonly Vector3[] Peak100 = {
            new Vector3(0.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.0999994054f, 0.0999993011f, 0.0999994650f),
            new Vector3(0.457565635f, 0.457565606f, 0.457565844f),
            new Vector3(0.742964506f, -0.00388545962f, 0.00311517389f),
            new Vector3(-0.00617693551f, 0.468831986f, 0.0614035167f),
            new Vector3(-0.00151558768f, 0.0364856310f, 0.375843495f),
            new Vector3(0.974019229f, 0.991704345f, -0.00871143863f),
            new Vector3(0.998735666f, 1.01268291f, 0.714851260f),
            new Vector3(1.02183819f, 0.889481008f, 0.868689239f),
            new Vector3(1.00747120f, 1.00747025f, 1.00747085f),
            new Vector3(-0.00533457287f, 0.288481951f, 0.420990050f),
            new Vector3(1.00102127f, 1.02023423f, 0.452295601f),
            new Vector3(1.00716889f, 0.924449563f, 0.978335500f),
            new Vector3(0.999701500f, 0.995480835f, 0.998633921f)
        };
        internal static readonly Vector3[] Peak1000 = {
            new Vector3(0.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.145115465f, 0.145115286f, 0.145115539f),
            new Vector3(1.06563878f, 1.06563699f, 1.06563830f),
            new Vector3(0.944079101f, 0.00644811196f, -0.00338368653f),
            new Vector3(-0.0174575262f, 1.02289307f, -0.0178026948f),
            new Vector3(-0.0000438738134f, 0.00475804182f, 0.699621141f),
            new Vector3(4.87666845f, 5.01653194f, -0.0601025708f),
            new Vector3(9.53330612f, 9.64879799f, -0.0327940919f),
            new Vector3(10.9651756f, 5.00320530f, 3.50321960f),
            new Vector3(10.0645351f, 10.0645103f, 10.0645237f),
            new Vector3(-0.0202237647f, 0.482755333f, 0.985583961f),
            new Vector3(8.02326298f, 8.08945751f, -0.0752170533f),
            new Vector3(10.1561232f, 5.52128792f, 9.89658356f),
            new Vector3(9.98316479f, 8.92687416f, 10.0423365f)
        };
        internal static readonly Vector3[] Peak4000 = {
            new Vector3(0.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.168242127f, 0.168241858f, 0.168242201f),
            new Vector3(1.33882630f, 1.33882546f, 1.33882689f),
            new Vector3(1.08882928f, 0.0134962024f, -0.00258395378f),
            new Vector3(-0.0106852418f, 1.21315444f, -0.0171701666f),
            new Vector3(0.0103964638f, 0.0193503257f, 0.743704200f),
            new Vector3(8.32161331f, 8.53551197f, -0.154916376f),
            new Vector3(26.3552322f, 26.5119991f, -0.307347447f),
            new Vector3(44.3363724f, 9.57174683f, 4.29315424f),
            new Vector3(40.2843704f, 40.2842598f, 40.2843170f),
            new Vector3(-0.0152946441f, 0.569415390f, 1.15108144f),
            new Vector3(18.5241928f, 18.5975151f, -0.257488132f),
            new Vector3(39.6240463f, 6.17263126f, 40.4600563f),
            new Vector3(39.3782692f, 26.9915810f, 40.4772911f)
        };
        internal static readonly Vector3[] Peak10000 = {
            new Vector3(0.00000000f, 0.00000000f, 0.00000000f),
            new Vector3(0.177862555f, 0.177862450f, 0.177862704f),
            new Vector3(1.44550312f, 1.44550228f, 1.44550455f),
            new Vector3(1.12358260f, 0.0266149659f, 0.000404266553f),
            new Vector3(0.0159813017f, 1.26996446f, -0.00736485608f),
            new Vector3(0.0242653694f, 0.0374039337f, 0.703696728f),
            new Vector3(10.0259790f, 10.2594156f, -0.247419536f),
            new Vector3(42.3339310f, 42.4508057f, -0.597667873f),
            new Vector3(108.426682f, 8.00832462f, 1.78490305f),
            new Vector3(100.509300f, 100.509178f, 100.509239f),
            new Vector3(0.00462206546f, 0.602060854f, 1.18831825f),
            new Vector3(26.1340694f, 26.1738834f, -0.431769460f),
            new Vector3(80.6606522f, -1.97865450f, 88.7194748f),
            new Vector3(94.3722534f, 40.5191040f, 102.391647f)
        };
        internal static Vector3[] ForPeak(int peak) => peak switch { 100 => Peak100, 1000 => Peak1000, 4000 => Peak4000, 10000 => Peak10000, _ => throw new System.ArgumentOutOfRangeException(nameof(peak)) };
    }
}
