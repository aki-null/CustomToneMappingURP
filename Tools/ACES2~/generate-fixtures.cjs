// Writes Tests/Editor/Aces2ReferenceData.cs from the official CTL interpreter (ACES_CTL_CHECK).
const fs=require('node:fs'),path=require('node:path');
const {officialReference}=require('./validate-reference.cjs');
const inputs=[[0,0,0],[.18,.18,.18],[1,1,1],[1,0,0],[0,1,0],[0,0,1],
 [5.8435,5.9848,.00979],[29.55,29.55,0],[100,0,0],[1000,1000,1000],[-.1,.5,1],
 [16,16,0],[56.91938,.0090394,62.25901],[187.191,.1495014,252.8753]];
const f=x=>Number(x).toPrecision(9)+'f';
const vec=v=>'new Vector3('+v.map(f).join(', ')+')';
let source='// Frozen with the unmodified Academy CTL interpreter (e280b6c4cf49726914260f1cf0cb2949d8db1027).\n'
 +'// ACES source: 069b0bc3e1f6c62820f19fdae2fecec3f4fc0f80. AP1 input; linear output before peak clamp.\n'
 +'// Regenerate with Tools/ACES2~/generate-fixtures.cjs and ACES_CTL_CHECK set.\n'
 +'using UnityEngine;\nnamespace CustomToneMapping.Tests\n{\n    internal static class Aces2ReferenceData\n    {\n';
source+='        internal static readonly Vector3[] Inputs = {\n'+inputs.map(v=>'            '+vec(v)).join(',\n')+'\n        };\n';
for(const peak of [100,1000,4000,10000]){
 const values=officialReference(inputs.flat(),peak),rows=[];
 for(let i=0;i<values.length;i+=3)rows.push(vec(Array.from(values.subarray(i,i+3))));
 source+=`        internal static readonly Vector3[] Peak${peak} = {\n`+rows.map(v=>'            '+v).join(',\n')+'\n        };\n';
}
source+='        internal static Vector3[] ForPeak(int peak) => peak switch { 100 => Peak100, 1000 => Peak1000, 4000 => Peak4000, 10000 => Peak10000, _ => throw new System.ArgumentOutOfRangeException(nameof(peak)) };\n    }\n}\n';
const target=path.resolve(__dirname,'../../Tests/Editor/Aces2ReferenceData.cs');
fs.writeFileSync(target,source);
console.log('wrote '+path.relative(path.resolve(__dirname,'../..'),target));
