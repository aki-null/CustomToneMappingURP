// Mechanical CTL -> Burst-compatible C# port of the pinned Academy reference.
// Writes Runtime/Baker/ACES2/AcademyTransform.cs. --check only reports whether it is up to date (README.md).
// Not a general CTL compiler: it handles the constructs these three library files use.
//   float[3] -> double3, float[2] -> double2, int[2] -> int2, float[3][3] -> double3x3 (row i in column i),
//   other arrays -> NativeArray<double|int|double3> (Allocator.Temp), structs -> structs, no heap allocations.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const sources = ['Utilities', 'Tonescale', 'OutputTransform'];
const types = new Map();       // variable/field/parameter name -> {t, dims}
const structs = new Map();     // struct name -> [{t, name, dims}]
const strip = s => s.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
const files = sources.map(n => strip(fs.readFileSync(path.join(__dirname, 'Reference', `Lib.Academy.${n}.ctl`), 'utf8')));
structs.set('Chromaticities', []);
for (const s of files) for (const m of s.matchAll(/struct\s+(\w+)/g)) structs.set(m[1], []);
const typePattern = 'float|int|bool|' + [...structs.keys()].join('|');
const scalar = t => t === 'float' ? 'double' : t;
const vectorDims = d => d.length === 1 && /^[23]$/.test(d[0]) || d.length === 2 && d[0] === '3' && d[1] === '3';
function csType(t, dims) {
    if (!dims.length) return scalar(t);
    if (vectorDims(dims)) return dims.length === 2 ? 'double3x3' : `${scalar(t)}${dims[0]}`;
    return `NativeArray<${dims.length === 2 ? 'double3' : scalar(t)}>`;
}
const isTable = type => type && type.dims.length && !vectorDims(type.dims);
function allocation(t, dims) {
    if (!dims.length) return structs.has(t) && structs.get(t).some(f => isTable(f)) ? `${t}.Create()` : 'default';
    if (vectorDims(dims)) return 'default';
    return `new ${csType(t, dims)}(${dims[0]}, Allocator.Temp)`;
}
function literal(t, dims, s) {
    if (t === 'Chromaticities') {
        const rows = [...s.matchAll(/\{([^{}]+)\}/g)].map(m => `new double2(${m[1]})`);
        return `new Chromaticities {red=${rows[0]}, green=${rows[1]}, blue=${rows[2]}, white=${rows[3]}}`;
    }
    if (dims.length === 2) return `new double3x3(${[...s.matchAll(/\{([^{}]+)\}/g)].map(m => `new double3(${m[1]})`).join(', ')})`;
    return `new ${csType(t, dims)}(${s.slice(1, -1)})`;
}
function dimsOf(s) { return [...s.matchAll(/\[([^\]]*)\]/g)].map(m => m[1].trim()); }
const declPattern = new RegExp(`\\b(?:const\\s+)?(${typePattern})\\s+(\\w+)((?:\\[[^\\]]*\\])*)\\s*(?:=\\s*([^;]+))?;`, 'g');
// `types` has no scoping, and nine CTL names bind to more than one type (test_JMh, J_intersect_source and
// J_intersect_cusp are each a scalar somewhere and a table elsewhere). What keeps the table rewrites below correct
// is order: this runs per function, just before that function's body is rewritten. Do not hoist or defer it.
function declarations(s) {
    return s.replace(declPattern, (_, t, name, d, value) => {
        const dims = dimsOf(d);
        types.set(name, {t, dims});
        let init;
        if (!value) init = allocation(t, dims);
        else if (value.trim().startsWith('{')) init = literal(t, dims, value.trim());
        else init = isTable({t, dims}) && /^[\w.]+(\[[^\]]+\])*$/.test(value.trim()) ? `Copy(${value.trim()})` : value;
        return `${csType(t, dims)} ${name} = ${init};`;
    });
}
function clean(s) {
    return s.replace(/\bunsigned\s+int\b/g,'int').replace(/\b(params|in|out|base)\b/g,'@$1')
        .replace(/\bfloat\s*\(/g,'(double)(').replace(/\bint\s*\(/g,'(int)(')
        .replace(/\b(\d+)\.(?!\d)/g,'$1.0')
        .replace(/\binput\s+|\boutput\s+|\bvarying\s+/g,'');
}
// Global array constants that are not vectors or matrices become switch functions: Burst has no static tables.
const constantTables = new Map();
const parallelLoops = new Set(['make_reach_m_table', 'build_cusp_table', 'make_upper_hull_gamma_table']);
// Emitted pieces in CTL order. Functions carry a name, so the reachability pass below can drop unused ones.
const pieces = [];
for (let source of files) {
    // Debug printing utilities do not participate in rendering. The lookahead must list every type a top-level
    // declaration can start with, struct returns included, or it runs past the print function to end of file.
    source = source.replace(new RegExp(`void print_\\w+\\([^]*?(?=\\n(?:${typePattern}|void) |$)`, 'g'), '');
    source = source.replace(/struct\s+(\w+)\s*\{([^]*?)\};/g, (_, name, body) => {
        const fields = [];
        for (const m of body.matchAll(declPattern)) { const dims = dimsOf(m[3]); fields.push({t: m[1], name: m[2], dims}); types.set(m[2], {t: m[1], dims}); }
        structs.set(name, fields);
        let cs = `internal struct ${name}\n{\n${fields.map(f => `internal ${csType(f.t, f.dims)} ${f.name};`).join('\n')}\n`;
        const tables = fields.filter(isTable);
        if (tables.length) cs += `internal static ${name} Create()\n{\n${name} r = default;\n${tables.map(f => `r.${f.name} = ${allocation(f.t, f.dims)};`).join('\n')}\nreturn r;\n}\n`;
        pieces.push({code: cs + '}\n'});
        return '';
    });
    const fnRegex = new RegExp(`^(${typePattern}|void)((?:\\[[^\\]]*\\])*)\\s+(\\w+)\\s*\\(([^]*?)\\)\\s*\\{`, 'gm');
    let match;
    while ((match = fnRegex.exec(source))) {
        let depth = 1, end = fnRegex.lastIndex;
        while (depth && end < source.length) { if (source[end] === '{') depth++; if (source[end] === '}') depth--; end++; }
        const [, t, d, name, args] = match;
        // CTL passes everything by value and nothing here mutates a struct parameter, so the large structs go by `in`.
        const parameters = args.replace(new RegExp(`(?:(input|output)\\s+)?(${typePattern})\\s+(\\w+)((?:\\[[^\\]]*\\])*)`, 'g'),
            (_, mode, pt, pn, pd) => { const dims = dimsOf(pd); types.set(pn, {t: pt, dims}); return `${structs.has(pt) && !dims.length ? 'IN_PARAM ' : ''}${csType(pt, dims)} ${pn}`; });
        let body = declarations(source.slice(fnRegex.lastIndex, end - 1));
        // Table element writes: NativeArray<double3> elements are copies, so write back through With().
        body = body.replace(/\b([\w.]+)\[([^\]]+)\]\[([^\]]+)\]\s*=(?!=)\s*([^;]+);/g, (all, lhs, a, b, rhs) => {
            const type = types.get(lhs.split('.').at(-1));
            return type && type.dims.length === 2 && isTable(type) ? `${lhs}[${a}] = With(${lhs}[${a}], ${b}, ${rhs});` : all;
        });
        // Whole-table assignment from another table copies (CTL arrays are values); assignment from a call does not.
        body = body.replace(/\b([\w.]+)\s*=(?!=)\s*([\w.]+(?:\[[^\]]+\])*)\s*;/g, (all, lhs, rhs) => {
            const type = types.get(lhs.split('.').at(-1));
            return isTable(type) && !dimsOf(lhs).length && !/^(default|new )/.test(rhs) ? `${lhs} = Copy(${rhs});` : all;
        });
        // CTL .size: vectors know their length at compile time, tables at run time.
        body = body.replace(/\b(\w+)\.size\b/g, (all, v) => { const type = types.get(v); return type && vectorDims(type.dims) ? type.dims[0] : `${v}.Length`; });
        // The per-hue loops of the three table builders also become NAME_entry(i, ...) functions, so
        // Aces2LutBaker can run them as parallel jobs while the serial function stays the reference.
        if (parallelLoops.has(name)) {
            const loop = /for \(int (\w+) = [^;]+; [^;]+; [^)]+\)\s*\{/.exec(body);
            if (!loop) throw new Error(`${name}: expected a per-hue loop`);
            let depth = 1, end = loop.index + loop[0].length;
            while (depth) { if (body[end] === '{') depth++; if (body[end] === '}') depth--; end++; }
            const loopBody = body.slice(loop.index + loop[0].length, end - 1), pre = body.slice(0, loop.index);
            const locals = [...pre.matchAll(/^\s*([\w<>]+) (\w+) = /gm)].map(m => ({type: m[1], name: m[2]})).filter(l => new RegExp(`\\b${l.name}\\b`).test(loopBody));
            // Hoisted locals are passed by value; a loop that updates one (loop-carried state, e.g. a revived `previous`
            // warm start in build_cusp_table) would make the entries order-dependent and no longer parallel.
            for (const l of locals) if (!l.type.startsWith('NativeArray') && new RegExp(`\\b${l.name}(\\[[^\\]]+\\])?\\s*=(?!=)`).test(loopBody))
                throw new Error(`${name}: loop-carried local ${l.name}; the per-hue entries are not independent`);
            const names = parameters.split(',').map(a => a.trim().split(/\s+/).at(-1));
            pieces.push({name: `${name}_entry`, code: `internal static void ${name}_entry(int ${loop[1]}, ${parameters}, ${locals.map(l => `${l.type} ${l.name}`).join(', ')})\n{\n${loopBody}\n}\n`});
            body = pre + loop[0] + `\n${name}_entry(${loop[1]}, ${[...names, ...locals.map(l => l.name)].join(', ')});\n}` + body.slice(end);
        }
        pieces.push({name, code: `internal static ${csType(t, dimsOf(d))} ${name}(${parameters})\n{\n${body}\n}\n`});
        source = source.slice(0, match.index) + source.slice(end);
        fnRegex.lastIndex = match.index;
    }
    source = source.replace(declPattern, (all, t, name, d, value) => {
        const dims = dimsOf(d);
        if (!isTable({t, dims})) return all;
        constantTables.set(name, {t, values: value.trim().slice(1, -1).split(',').map(v => v.trim())});
        return '';
    });
    const emitted = declarations(source).replace(/^(\s*)(double|int|bool|double2|double3|double3x3|int2|Chromaticities)\s/gm, '$1internal static readonly $2 ');
    // Consumed functions are spliced out of `source`, so only global declarations should remain. Anything else is
    // a construct fnRegex missed; appending it would surface as a C# error instead of the parse failure it is.
    const residue = emitted.replace(/^\s*internal static readonly [^\n]*$/gm, '').trim();
    if (residue) throw new Error(`unrecognised CTL construct left after parsing:\n${residue.slice(0, 400)}`);
    pieces.push({code: emitted});
}
for (const [name, {t, values}] of constantTables) {
    // This is the only rewrite that runs over the whole output after scope information is gone, so it is the only
    // one that could silently corrupt an unrelated symbol instead of failing in the C# compiler.
    const local = types.get(name);
    if (local && !(local.dims.length && !vectorDims(local.dims)))
        throw new Error(`constant table ${name} collides with a ${csType(local.t, local.dims)} declaration; rename one before the table rewrite can run`);
    for (const p of pieces) p.code = p.code.replace(new RegExp(`\\b${name}\\[([^\\]]+)\\]`, 'g'), `${name}($1)`);
    pieces.push({name, code: `internal static ${scalar(t)} ${name}(int i)\n{\nswitch (i)\n{\n${values.map((v, i) => `case ${i}: return ${v};`).join('\n')}\ndefault: return ${values.at(-1)};\n}\n}\n`});
}
// Emit only what the runtime reaches: the serial init_ODTParams (the reference that Aces2Tests checks the parallel bake
// against) and whatever the hand-written files next to the output call. The inverse output transform is unreachable.
const consumerDir = path.join(root, 'Runtime/Baker/ACES2');
const consumers = fs.readdirSync(consumerDir).filter(n => n.endsWith('.cs') && n !== 'AcademyTransform.cs')
    .map(n => strip(fs.readFileSync(path.join(consumerDir, n), 'utf8'))).join('\n');
const functions = new Map(pieces.filter(p => p.name).map(p => [p.name, p]));
const reached = new Set();
function reach(code) {
    for (const m of code.matchAll(/\b(\w+)\s*\(/g)) {
        const fn = functions.get(m[1]);
        if (fn && !reached.has(fn.name)) { reached.add(fn.name); reach(fn.code); }
    }
}
reach('init_ODTParams(' + consumers);
for (const p of pieces) if (!p.name) reach(p.code);   // struct and global initializers
let generated = pieces.filter(p => !p.name || reached.has(p.name)).map(p => p.code).join('');
const prelude = fs.readFileSync(path.join(__dirname, 'reference-prelude.txt'), 'utf8');
generated = generated.replace(/x1 = (x [+-] 0\.5);/g, 'x1 = (int)($1);')
    .replace(/int result = \(wrapped_hue \/ hue_limit \* table_size\);/g, 'int result = (int)(wrapped_hue / hue_limit * table_size);')
    .replace(/i = midpoint\(([^;]+)\);/g, 'i = (int)midpoint($1);')
    .replace(/int nominal_idx = ([^;]+);/g, 'int nominal_idx = (int)($1);')
    .replace(/!found/g, 'found == 0')
    // Early returns re-declare the parameter name in a nested scope; return the value directly.
    .replace(/double3 JMh = (new double3\([^;]+\));\s*return JMh;/g, 'return $1;');
// The body arrives carrying the CTL's own indentation, plus a blank line wherever a comment was stripped. Re-indent
// it by brace depth and collapse the blank runs. Counting braces per line is exact here: the generated region has no
// strings, chars, comments or directives. The hand-written prelude keeps its own formatting.
function reindent(src, baseDepth) {
    const out = [];
    let depth = baseDepth, blank = false, wrapped = false;
    for (const raw of src.split('\n')) {
        const line = raw.trim();
        if (!line) { blank = out.length > 0; continue; }
        const brace = line === '{' || line === '}';
        const indent = depth - (line.startsWith('}') ? 1 : 0) + (wrapped && !brace ? 1 : 0);
        // One blank line before each declaration; inside a body, collapse the runs left by stripped comments to one.
        const prev = out[out.length - 1];
        // A declaration, not a constant whose initializer happens to call something: the '(' precedes any '='.
        const paren = line.indexOf('('), assign = line.indexOf('=');
        const declaration = (line.startsWith('internal ') && paren >= 0 && (assign < 0 || paren < assign)) ||
            (indent === baseDepth && prev === '    '.repeat(baseDepth) + '}');
        if (out.length && prev !== '' && !prev.endsWith('{') && (declaration || (blank && line !== '}'))) out.push('');
        blank = false;
        out.push('    '.repeat(Math.max(0, indent)) + line);
        depth += (line.match(/{/g) || []).length - (line.match(/}/g) || []).length;
        wrapped = !/[;{}:]$/.test(line);   // a wrapped parameter list or expression continues on the next line
    }
    return out.join('\n') + '\n';
}
const body = reindent(clean(generated).replace(/IN_PARAM /g, 'in '), 1);
const output = ('// SPDX-License-Identifier: Apache-2.0\n// Copyright Contributors to the ACES Project.\n// Mechanically adapted from aces-core 069b0bc3e1f6c62820f19fdae2fecec3f4fc0f80.\n// Generated by Tools/ACES2~/port-ctl.cjs. Doubles keep the reference precision.\n' + prelude + body + '}\n}\n').replace(/[ \t]+$/gm, '');
const target = path.join(root, 'Runtime/Baker/ACES2/AcademyTransform.cs');
// --check exits 1 when the committed file is not what this run produces. Otherwise the file is written.
// CRLF checkouts compare equal.
const current = fs.existsSync(target) ? fs.readFileSync(target, 'utf8').replace(/\r\n/g, '\n') : null;
if (process.argv.includes('--check')) {
    console.log((current === output ? 'up to date: ' : 'out of date: ') + path.relative(root, target));
    process.exitCode = current === output ? 0 : 1;
} else {
    fs.writeFileSync(target, output);
    console.log('wrote ' + path.relative(root, target));
}
