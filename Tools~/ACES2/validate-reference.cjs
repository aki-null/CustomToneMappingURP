// Checks the managed Academy CTL port against the official CTL interpreter.
const {execFileSync}=require('node:child_process');
const path=require('node:path');
const assert=require('node:assert/strict');

// Linear AP1 float32 in, display-linear float32 out (100-nit units, before the peak clamp).
function officialReference(values, peak) {
    assert(process.env.ACES_CTL_CHECK,'Set ACES_CTL_CHECK to the official-ctl-check executable.');
    const data=Float32Array.from(values);
    const out=execFileSync(process.env.ACES_CTL_CHECK,[String(peak),path.join(__dirname,'Reference')],
        {input:Buffer.from(data.buffer),maxBuffer:256*1024*1024});
    return new Float32Array(out.buffer.slice(out.byteOffset,out.byteOffset+out.byteLength));
}

function run() {
    // The generated port uses Unity.Mathematics types; compile the installed package's own sources.
    const mathematics=require('node:fs').globSync?.(path.join(__dirname,'../../../../Library/PackageCache/com.unity.mathematics@*/Unity.Mathematics'))?.[0]
        ?? process.env.UNITY_MATHEMATICS;
    assert(mathematics,'Set UNITY_MATHEMATICS to a com.unity.mathematics package\'s Unity.Mathematics source directory.');
    execFileSync('dotnet',['build',path.join(__dirname,'ReferenceCheck.csproj'),'--nologo','-v:q',`-p:UnityMathematics=${mathematics}`],{stdio:'pipe'});
    let seed=0xace520;
    const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
    const inputs=[];
    for(let i=0;i<12000;i++) inputs.push(...[0,1,2].map(()=>.18*2**(-14+30*random())));
    for(const v of [0,.00001,.18,1,16,59,100,1000,10000])for(const ray of [[1,1,1],[1,0,0],[0,1,0],[0,0,1],[1,1,0],[1,0,1],[0,1,1]])inputs.push(...ray.map(x=>x*v));
    inputs.push(-.1,.5,1,-1,-1,-1);
    const data=Float32Array.from(inputs),results=[];
    for(const peak of [100,1000,4000,10000]) {
        const buf=execFileSync('dotnet',[path.join(__dirname,'bin/Debug/net10.0/ReferenceCheck.dll'),String(peak)],{input:Buffer.from(data.buffer),maxBuffer:10000000});
        const actual=new Float32Array(buf.buffer.slice(buf.byteOffset,buf.byteOffset+buf.byteLength));
        const expected=officialReference(data,peak),errors=[];
        let worst;
        for(let i=0;i<actual.length;i++) {
            assert(Number.isFinite(actual[i]));
            // Compare final display-linear outputs after the official peak clamp.
            const clip=x=>Math.max(0,Math.min(peak/100,x));
            const error=Math.abs(clip(actual[i])-clip(expected[i]))/(peak/100);
            errors.push(error);
            if(!worst||error>worst.error)worst={error,input:Array.from(data.subarray(i-i%3,i-i%3+3)),channel:i%3,actual:actual[i],expected:expected[i]};
        }
        errors.sort((a,b)=>a-b);
        results.push({peak,samples:data.length/3,p99:errors[Math.floor(errors.length*.99)],max:errors.at(-1),worst});
    }
    console.log(JSON.stringify({reference:'Official CTL interpreter, aces-core 069b0bc3e1f6c62820f19fdae2fecec3f4fc0f80',units:'fraction of display peak',results},null,2));
    assert(results.every(r=>r.max<.0002),'Reference agreement exceeds 0.02% of peak');
}

module.exports={officialReference};
if(require.main===module) try { run(); } catch(e) { console.error(e.message);process.exitCode=1; }
