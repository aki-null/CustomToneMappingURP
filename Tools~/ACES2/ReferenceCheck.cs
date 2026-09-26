using System;
using System.IO;
using System.Globalization;
using Unity.Mathematics;
using CustomToneMapping.Baker.ACES2;
// Binary float32 AP1 RGB on stdin -> float32 display-linear RGB (100-nit units, before the peak clamp) on stdout.
internal static class ReferenceCheck
{
    static void Main(string[] args)
    {
        var peak=double.Parse(args[0],CultureInfo.InvariantCulture);
        bool hdr=peak!=100;
        var p=AcademyTransform.init_ODTParams(peak,hdr?AcademyTransform.Rec2020Primaries:AcademyTransform.Rec709Primaries);
        using var input=new BinaryReader(Console.OpenStandardInput());
        using var output=new BinaryWriter(Console.OpenStandardOutput());
        while(true)
        {
            double3 ap1;
            try { ap1=new double3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle()); }
            catch(EndOfStreamException) { break; }
            var rgb=AcademyTransform.outputTransform_fwd(math.mul(AcademyTransform.AP1_TO_AP0,ap1),p);
            for(int c=0;c<3;c++)output.Write((float)rgb[c]);
        }
    }
}
