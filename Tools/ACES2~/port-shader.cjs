// Generate the forward-only HLSL and the atlas sizes and display constants (Aces2Atlas.Generated.cs) from the same
// pinned Academy CTL as the managed reference port. --check only reports whether the committed files are up to date (README.md).
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
let src=['Utilities','Tonescale','OutputTransform'].map(n=>fs.readFileSync(path.join(__dirname,'Reference',`Lib.Academy.${n}.ctl`),'utf8')).join('\n');
src=src.replace(/\/\*[\s\S]*?\*\//g,'').replace(/\/\/[^\n]*/g,'');
const structs=new Map();
for(const m of src.matchAll(/struct\s+(\w+)\s*\{([^]*?)\};/g)) structs.set(m[1],m[2]);
const funcs=new Map(), re=/^(float(?:\[[^\]]*\])*|int(?:\[[^\]]*\])*|bool|void|JMhParams|ODTParams|TSParams|HueDependentGamutParams)\s+(\w+)\s*\(([^]*?)\)\s*\{/gm;
let m;
while((m=re.exec(src))){let depth=1,end=re.lastIndex;while(depth){if(src[end]==='{')depth++;if(src[end]==='}')depth--;end++;}funcs.set(m[2],{type:m[1],args:m[3],body:src.slice(re.lastIndex,end-1)});re.lastIndex=end;}
// Hand-written HLSL replaces these CTL functions. shader-prelude.txt (before the generated functions): the atlas
// lookups and init_HueDependentGamutParams. shader-postlude.txt (after them): Aces2OutputTransformAP1 (the CTL's
// outputTransform_fwd on ACEScg), the tonescale / chroma-compression stage and gamut_compress_fwd, which read
// per-display tables the atlas builder bakes from the reference port.
// reach_M_from_table is not emitted either: translate() turns its call in compress_gamut into the hdp field.
const overrideNames=new Set(['init_HueDependentGamutParams','chroma_compress_fwd',
 'gamut_compress_fwd','reach_M_from_table','min','max','clamp','lerp','sign']);
const prelude=fs.readFileSync(path.join(__dirname,'shader-prelude.txt'),'utf8');
const postlude=fs.readFileSync(path.join(__dirname,'shader-postlude.txt'),'utf8');
// Code only: both files name generated functions in their comments, which must not count as calls.
const handWritten=(prelude+postlude).replace(/\/\*[\s\S]*?\*\//g,'').replace(/\/\/[^\n]*/g,'');
// Roots are whatever the hand-written HLSL calls, so adding a call to the postlude is enough to pull a generated
// function in. Emission follows CTL declaration order, which does not shift when those files are reworded.
const roots=[...funcs.keys()].filter(n=>!overrideNames.has(n)&&new RegExp(`\\b${n}\\s*\\(`).test(handWritten));
const visited=new Set(),ordered=[];
function visit(n){if(visited.has(n)||overrideNames.has(n)||!funcs.has(n))return;visited.add(n);for(const m of funcs.get(n).body.matchAll(/\b(\w+)\s*\(/g))visit(m[1]);ordered.push(n);}
for(const r of roots)visit(r);
if(!roots.length)throw new Error('no generated function is called from shader-prelude.txt or shader-postlude.txt');
function translate(s){
 return s.replace(/float\[3\]\[3\]/g,'float3x3').replace(/float\[([234])\]/g,'float$1')
  .replace(/\bfloat\s+(\w+)\[3\]\[3\]/g,'float3x3 $1')
  .replace(/\bfloat\s+(\w+)\[([234])\]/g,'float$2 $1')
  .replace(/\bfloat([234])\s+(\w+)\s*=\s*\{([^}]+)\};/g,'float$1 $2 = float$1($3);')
  .replace(/\bfloat([234])\s+(\w+)\s*;/g,'float$1 $2 = 0;')
  .replace(/\bmult_f3_f33\b/g,'mul').replace(/\bfabs\b/g,'abs').replace(/\bpow\(/g,'aces2_pow(')
  .replace(/\bM_PI\b/g,'3.14159265358979323846')
  .replace(/\binput\s+|\bvarying\s+/g,'').replace(/\b(in|out|base|linear)\b/g,'aces_$1')
  .replace(/reach_M_from_table\([^,]+,\s*p.TABLE_reach_M\)/g,'hdp.reach_max_M')
  .replace(/\bint\s+(\w+)\[2\]/g,'int2 $1');
}
// Atlas (RGBA32F, WIDTH x HEIGHT texels), built by the hand-written Aces2Atlas.cs, which documents the layout.
// Rows 0, 3 and 4: the CTL's cusp and upper-hull gamma as exact linear pieces, found through a uniform grid of HUE_SAMPLES hue cells. Row 1: reach
// M and the chroma-compression norm on that grid (HUE_SAMPLES + 1 columns, the last repeating hue 0). The grid includes
// the entries of the CTL's one-degree reach table, so reach M interpolates exactly; the norm is a smooth function of hue
// and interpolates to about 1e-6 relative. Row 2: the tonescale /
// chroma-compression stage as a function of the input achromatic response A. The display constants are a uniform
// array, not texels.
const HUE_SAMPLES=1440,WIDTH=HUE_SAMPLES+1,HEIGHT=5;
// Parameter-block fields the forward-only shader never reads, dropped from the generated structs and the uniform
// array alike: reach M and the chroma-compression norm come from atlas row 1, and the inverse tonescale limit
// belongs to the inverse transform. The check after the function bodies fails the build if one is read again.
const deadFields=new Map([['ODTParams',['reach_params','chroma_compress_scale']],['TSParams',['inverse_limit']]]);
// Layout only. A statement longer than 120 columns breaks after its first open parenthesis, with the arguments
// packed one indent deeper.
function wrap(line){
 const open=line.indexOf('(');
 if(line.length<=120||open<0)return line;
 const indent=line.match(/^ */)[0]+'    ',args=[];
 let depth=0,start=open+1,close=open;
 for(;close<line.length;close++){
  const c=line[close];
  if(c==='(')depth++;
  else if(c===')'&&--depth===0)break;
  else if(c===','&&depth===1){args.push(line.slice(start,close).trim());start=close+1;}
 }
 args.push(line.slice(start,close).trim());
 const lines=[line.slice(0,open+1)];let current='';
 args.forEach((a,k)=>{
  const piece=a+(k<args.length-1?',':line.slice(close));
  if(current&&(indent+current+' '+piece).length>120){lines.push(indent+current);current=piece;}
  else current+=(current?' ':'')+piece;
 });
 lines.push(indent+current);
 return lines.join('\n');
}
// Joins statements the CTL spreads over several lines, then wraps them. Stripping the CTL's comments leaves runs of
// blank lines, which collapse to one, and none at the start or end of a block.
function tidy(code){
 const out=[];let pending=null;
 for(const raw of code.split('\n')){
  const line=raw.trimEnd();
  pending=pending===null?line:pending+(pending.endsWith('(')?'':' ')+line.trim();
  let depth=0;for(const c of pending)depth+=c==='('?1:c===')'?-1:0;
  if(depth>0)continue;
  out.push(wrap(pending));pending=null;
 }
 if(pending!==null)throw new Error(`unbalanced parentheses in the generated code: ${pending.slice(0,60)}`);
 return out.join('\n').replace(/\n{3,}/g,'\n\n').replace(/\{\n\n/g,'{\n').replace(/\n\n(\s*})/g,'\n$1');
}
let generated='';
for(const n of ordered){const f=funcs.get(n);generated+=translate(`${f.type} ${n}(${f.args})\n{${f.body}\n}\n\n`);}
generated=tidy(generated).trimEnd()+'\n';
// reach_M_from_table becomes a HueDependentGamutParams field, so it may only appear where hdp is a parameter.
for(const n of ordered) if(/hdp\.reach_max_M/.test(translate(funcs.get(n).body))&&!/HueDependentGamutParams\s+hdp/.test(funcs.get(n).args))
 throw new Error(`${n} reads the reach table without a HueDependentGamutParams hdp parameter.`);
for(const [type,names] of deadFields) for(const f of names)
 if(new RegExp(`\\.${f}\\b`).test(generated+handWritten))
  throw new Error(`${type}.${f} is in deadFields but the shader reads it: drop it from deadFields and regenerate.`);
// Two entry points: ACES2065-1 (URP's ACES grading branch hands AP0 to the hooks) and ACEScg, which the package's
// standard-grading callers reach with a single matrix from their own space.
const entry='float3 CustomAces2TonemapAP1(float3 ap1)\n{\n    ODTParams p = Aces2Parameters();\n'+
 '    return clamp(Aces2OutputTransformAP1(ap1, p), 0.0, p.peakLuminance / 100.0);\n}\n\n'+
 'float3 CustomAces2Tonemap(float3 ap0)\n{\n    return CustomAces2TonemapAP1(mul(ap0, AP0_TO_AP1));\n}\n';
// The uniform array holds only the ODTParams fields the shader reads. Reads are followed through struct arguments:
// Aab_to_RGB(Aab, p.limit_params) reads p.limit_params.MATRIX_Aab_to_cone_response because Aab_to_RGB reads
// p.MATRIX_Aab_to_cone_response of its JMhParams parameter.
const shaderCode=(prelude+generated+postlude+entry).replace(/\/\*[\s\S]*?\*\//g,'').replace(/\/\/[^\n]*/g,'');
const shaderFuncs=new Map();
for(const m of shaderCode.matchAll(/^\w+\s+(\w+)\s*\(([^)]*)\)\s*\{/gm)){
 let depth=1,end=m.index+m[0].length;
 while(depth){if(shaderCode[end]==='{')depth++;if(shaderCode[end]==='}')depth--;end++;}
 const params=m[2].split(',').filter(a=>a.trim()).map(a=>{const t=a.replace(/=.*/,'').trim().split(/\s+/);return {type:t.at(-2),name:t.at(-1)};});
 shaderFuncs.set(m[1],{params,body:shaderCode.slice(m.index+m[0].length,end-1)});
}
function callArgs(body,open){
 const args=[];let depth=0,start=open+1,i=open;
 for(;;i++){const c=body[i];if(c==='(')depth++;else if(c===')'){if(--depth===0)break;}else if(c===','&&depth===1){args.push([start,i]);start=i+1;}}
 args.push([start,i]);return args;
}
const readCache=new Map();
// Field paths below `v` that `body` reads, such as '.input_params.cz'.
function reads(body,v){
 const paths=new Set(),consumed=[];
 for(const m of body.matchAll(/\b(\w+)\s*\(/g)){
  const callee=shaderFuncs.get(m[1]);if(!callee)continue;
  callArgs(body,m.index+m[0].length-1).forEach(([a,b],k)=>{
   const arg=body.slice(a,b).trim(),chain=new RegExp(`^${v}((?:\\.\\w+)*)$`).exec(arg),param=callee.params[k];
   if(!chain||!param||!structs.has(param.type))return;
   const key=`${m[1]}:${k}`;
   if(!readCache.has(key))readCache.set(key,reads(callee.body,param.name));
   for(const r of readCache.get(key))paths.add(chain[1]+r);
   consumed.push([a,b]);
  });
 }
 for(const m of body.matchAll(new RegExp(`\\b${v}\\b((?:\\.\\w+)*)`,'g'))){
  if(consumed.some(([a,b])=>m.index>=a&&m.index<b))continue;
  if(!m[1]){if(new RegExp(`^${v}\\s*=(?!=)`).test(body.slice(m.index)))continue;throw new Error(`cannot follow the whole ${v} in: ${body.slice(m.index,m.index+60)}`);}
  paths.add(m[1]);
 }
 return paths;
}
const shaderReads=[...reads(shaderFuncs.get('CustomAces2TonemapAP1').body,'p')].map(r=>'p'+r);
const isRead=key=>shaderReads.some(r=>r===key||key.startsWith(r+'.')||r.startsWith(key+'.'));
let fields='',csStore='',hlslLoad='',offset=0;
// A matrix of consecutive constants, which float3x3(...) takes row by row.
function matrix(type,base,rows,cols,indent){
 const r=[];
 for(let i=0;i<rows;i++)r.push([...Array(cols)].map((_,j)=>`Aces2Constant(${base+i*cols+j})`).join(', '));
 return `${type}(\n${indent}${r.join(`,\n${indent}`)})`;
}
function layout(type, prefix){
 for(const m of structs.get(type).matchAll(/\b(\w+)\s+(\w+)((?:\[[^\]]*\])*)\s*;/g)){
  const [_,t,n,d]=m; if(n.startsWith('TABLE_')||(deadFields.get(type)||[]).includes(n))continue;
  const key=prefix+'.'+n;
  if(!isRead(key))continue;
  if(structs.has(t)){layout(t,key);continue;}
  const dims=[...d.matchAll(/\[(\d+)\]/g)].map(x=>+x[1]);
  const size=dims.reduce((a,b)=>a*b,1);
  if(dims.length===2&&t==='float'){
   hlslLoad+=`    ${key} = ${matrix(`float${dims[0]}x${dims[1]}`,offset,dims[0],dims[1],'        ')};\n`;
   for(let i=0;i<size;i++)csStore+=`            Put(constants, ${offset+i}, ${key}[${Math.floor(i/dims[1])}][${i%dims[1]}]);\n`;
   offset+=size;continue;
  }
  for(let i=0;i<size;i++){
   const index=dims.length===2?`[${Math.floor(i/dims[1])}][${i%dims[1]}]`:dims.length?`[${i}]`:'';
   csStore+=`            Put(constants, ${offset}, ${key}${index});\n`;
   hlslLoad+=`    ${key}${index} = ${t==='int'?'(int)':''}Aces2Constant(${offset});\n`;offset++;
  }
 }
}
layout('ODTParams','p');
// Per-display constants beyond ODTParams, computed by the atlas builder and read by the hand-written HLSL.
// Aces2InvAInMax scales the input achromatic response to the J table. Aces2AP1ToCAM16 fuses the CTL's AP1->AP0 (after
// the input clamp) with the input model's RGB->CAM16 matrix, so the shader clamps in AP1 and needs one matrix, not two.
// It holds CTL row i in constants 3i..3i+2 and is used as mul(v, M) like the generated code.
const extras=[['Aces2InvAInMax','1.0/aInMax'],['Aces2InvLimitJMax','1.0/p.limit_J_max']];
const hlslExtras=[];let extraCount=extras.length;
extras.forEach(([n,expr],k)=>{csStore+=`            Put(constants, ${offset+k}, ${expr});\n`;hlslExtras.push(`float ${n}() { return Aces2Constant(${offset+k}); }\n`);});
{
 const base=offset+extraCount;
 csStore+=`            var ap1ToCam16=math.mul(p.input_params.MATRIX_RGB_to_CAM16_c,AcademyTransform.AP1_TO_AP0); // CTL AP1_TO_AP0 x RGB_to_CAM16\n`;
 for(let i=0;i<3;i++)for(let j=0;j<3;j++)csStore+=`            Put(constants, ${base+3*i+j}, ap1ToCam16[${i}][${j}]);\n`;
 hlslExtras.push(`float3x3 Aces2AP1ToCAM16()\n{\n    return ${matrix('float3x3',base,3,3,'        ')};\n}\n`);
 extraCount+=9;
}
const constantVectors=Math.ceil((offset+extraCount)/4);
for(const type of ['TSParams','JMhParams','HueDependentGamutParams','ODTParams']) {
 let body=structs.get(type).replace(/^.*\bTABLE_.*$/gm,'');
 for(const f of deadFields.get(type)||[]) body=body.replace(new RegExp(`^.*\\b${f}\\b.*$`,'gm'),'');
 // compress_gamut reads reach M from the hue grid with the CTL's gamut parameters.
 if(type==='HueDependentGamutParams') body+='\n    float reach_max_M;\n';
 const lines=translate(body).replace(/ = 0;/g,';').split('\n').map(l=>l.trim()).filter(Boolean);
 fields+=`\nstruct ${type}\n{\n${lines.map(l=>'    '+l).join('\n')}\n};\n`;
}
const neededConstants=['ref_luminance','cam_nl_Y_reference','cam_nl_offset','J_scale','smooth_cusps','cusp_mid_blend','focus_gain_blend','compression_threshold'];
let globals='';
for(const n of neededConstants){const m=src.match(new RegExp(`const (float|int) ${n}\\s*=([^;]+);`));if(m)globals+=`static const ${m[1]} ${n} = ${m[2].trim()};\n`;}
let hlsl='// SPDX-License-Identifier: Apache-2.0\n// Copyright Contributors to the ACES Project.\n// Forward ACES 2.0, generated from the pinned CTL by Tools/ACES2~/port-shader.cjs.\n// Why the transform runs per LUT texel instead of being sampled from a strip: see Aces2LutBaker.cs.\n//\n// GPU cost per LUT texel, from compiled mobile shader code: the Renderer Feature\'s pass costs about 0.65x URP\'s\n// ACES 1.x LutBuilderHdr, and URP\'s LutBuilderHdr with ACES 2.0 about 1.3x the same with ACES 1.x. The shader is\n// arithmetic-bound. The gamut mapper is the largest part, then the appearance model\'s powers. The per-hue and\n// per-J values are tables; see shader-postlude.txt for the register budget any further change must keep.\n#ifndef CUSTOM_ACES2_INCLUDED\n#define CUSTOM_ACES2_INCLUDED\n';
hlsl+=`\n#define ACES2_ATLAS_WIDTH ${WIDTH}\n#define ACES2_ATLAS_HEIGHT ${HEIGHT}\n#define ACES2_HUE_SAMPLES ${HUE_SAMPLES}\n\n`+globals+fields;
// Display constants arrive as a uniform array bound by Aces2LutBaker.Bind: register reads instead of texture fetches
// per LUT texel. Constant indices fold at compile time.
hlsl+='\nfloat4 _Aces2Constants['+constantVectors+'];\n\nfloat Aces2Constant(int i) { return _Aces2Constants[i >> 2][i & 3]; }\n\n'+
 hlslExtras.join('\n');
hlsl+='\n'+prelude;
// Academy row-vector AP0/AP1 conversion matrices.
hlsl+='\nstatic const float3x3 AP0_TO_AP1 = float3x3(\n    1.4514393161, -.0765537734, .0083161484,\n'+
 '    -.2365107469, 1.1762296998, -.0060324498,\n    -.2149285693, -.0996759264, .9977163014);\n';
hlsl+='\nstatic const float3x3 AP1_TO_AP0 = float3x3(\n    .6954522414, .0447945634, -.0055258826,\n'+
 '    .1406786965, .8596711185, .0040252103,\n    .1638690622, .0955343182, 1.0015006723);\n';
hlsl+='\n'+generated;
hlsl+='\n'+postlude;
// Zero-initialised, because only the fields the shader reads are loaded.
hlsl+='\nODTParams Aces2Parameters()\n{\n    ODTParams p = (ODTParams)0;\n'+hlslLoad+'    return p;\n}\n';
hlsl+='\n'+entry+'\n#endif\n';
const cs=`// SPDX-License-Identifier: Apache-2.0
// Copyright Contributors to the ACES Project.
// Generated by Tools/ACES2~/port-shader.cjs: the atlas sizes and the display constants. Aces2Atlas.cs builds the texels.
using Unity.Mathematics;
using UnityEngine;
namespace CustomToneMapping.Baker.ACES2
{
    internal static partial class Aces2Atlas
    {
        internal const int HueSamples=${HUE_SAMPLES}, Width=${WIDTH}, Height=${HEIGHT}, ConstantVectors=${constantVectors};
        // The display constants, in the order Aces2.hlsl's Aces2Parameters() reads them.
        internal static void BuildConstants(in AcademyTransform.ODTParams p, Vector4[] constants, double aInMax)
        {
${csStore}        }
        static void Put(Vector4[] constants, int i, double x) => constants[i/4][i%4]=(float)x;
    }
}
`;
const outputs=[['Runtime/URP/Shaders/Aces2.hlsl',hlsl],['Runtime/Baker/ACES2/Aces2Atlas.Generated.cs',cs]];
const norm=s=>s.replace(/\r\n/g,'\n').trimEnd().split('\n').map(l=>l.trimEnd()).join('\n')+'\n';
// --check exits 1 when a committed file is not what this run produces. Otherwise the files are written.
for(const [p,s] of outputs){
 const target=path.join(root,p),output=norm(s),current=fs.existsSync(target)?norm(fs.readFileSync(target,'utf8')):null;
 if(process.argv.includes('--check')){console.log((current===output?'up to date: ':'out of date: ')+p);if(current!==output)process.exitCode=1;}
 else{fs.writeFileSync(target,output);console.log('wrote '+p);}
}
