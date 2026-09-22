# ACES 2.0 tooling

Everything under this folder is development-only. The `~` suffix keeps Unity from importing it, so nothing here
ships in the package or appears in the asset database.

`Reference/` holds the pinned Academy CTL (aces-core `069b0bc3e1f6c62820f19fdae2fecec3f4fc0f80`) under its own
LICENSE.

## Generators

Both read `Reference/*.ctl` and write into the package, in either order. Never hand-edit the generated files. Change
a generator or one of the hand-written `.txt` fragments and regenerate.

| Script | Produces | Hand-written input |
| --- | --- | --- |
| `port-ctl.cjs` | `Runtime/Baker/ACES2/AcademyTransform.cs` | `reference-prelude.txt` |
| `port-shader.cjs` | `Runtime/URP/Shaders/Aces2.hlsl`, `Runtime/Baker/ACES2/Aces2Atlas.Generated.cs` | `shader-prelude.txt`, `shader-postlude.txt` |

```
node Tools/ACES2~/port-ctl.cjs             # regenerate
node Tools/ACES2~/port-shader.cjs --check  # exit 1 if a committed file is out of date
```

Run both with `--check` before committing. `git diff` shows what a regeneration changed.

The prelude/postlude fragments are the only place the port deliberately departs from the CTL. Each deviation is
commented where it lives. Everything else in the generated files is mechanical transcription.

## Validation

The managed port against the unmodified Academy CTL interpreter (`e280b6c4cf49726914260f1cf0cb2949d8db1027`), 12,065
samples per display. Worst deviation as a fraction of peak, against a bound of 2e-4:

| Peak | 100 nits | 1000 | 4000 | 10000 |
| --- | --- | --- | --- | --- |
| Deviation | 2.8e-5 | 5.4e-5 | 4.1e-5 | 3.8e-5 |

`Aces2.hlsl` is checked against the port by the Unity test `Aces2Tests.ShaderMatchesThePort`.

To re-measure (needs .NET 10, and the package inside an opened Unity project, whose `Library/PackageCache` provides
the Unity.Mathematics sources; or set `UNITY_MATHEMATICS`):

```
cmake -S Tools/ACES2~ -B build -DACES_CTL_SOURCE=<CTL interpreter checkout>   # needs OpenEXR 3 and Imath
cmake --build build --target official-ctl-check
export ACES_CTL_CHECK=$PWD/build/official-ctl-check
node Tools/ACES2~/validate-reference.cjs    # prints a JSON report, exit 1 past the bound
node Tools/ACES2~/generate-fixtures.cjs     # after changing the CTL pin: rewrites Tests/Editor/Aces2ReferenceData.cs
```

To move the aces-core pin: replace `Reference/`, regenerate, re-measure, regenerate the fixtures, and update the
commit hash here, in the generators and in `THIRD_PARTY_NOTICES.md`.
