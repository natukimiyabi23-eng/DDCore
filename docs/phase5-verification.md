# Phase 5 verification

## Scope

Verification applies to the user-defined Phase 5 DD state and read-only strategy engine. It does not cover live Dalamud behavior or any game operation.

## Results

| Check | Result |
|---|---|
| Locked restore | Passed for all five projects |
| Release solution build | Passed, 0 warnings / 0 errors |
| Core tests | 169 passed, 0 failed, 0 skipped |
| Observation tests | 40 passed, 0 failed, 0 skipped |
| Total tests | 209 passed |
| Format verification | Passed with no changes required |
| NuGet vulnerability audit | No known vulnerable direct or transitive package in all five projects |
| Core project references | None |
| Core assembly references | Framework assemblies only; no Dalamud, BossMod or ClientStructs |
| Static prohibited-operation scan | No targeting, action/item use, input, hook, scanner, ClientStructs or NavMesh operation symbol found in production source |
| Package | `0.5.0.0`, Dalamud API 15 |
| Package SHA-256 | `558A4A8E6C688BEEE3D9D199D93EFD0DFD80C06BCA6A9636EA503C147278BD0A` |

The package contains `DDCore.dll`, Core and Observation assemblies, deps/manifest files, symbols, `THIRD-PARTY-NOTICES.md`, and Phase 3/4 provenance manifests. It contains no BossMod assembly.

Push run `42412644674` and Draft PR run `42412649333` both passed the Windows build/test/package workflow at head `1b0e938`. The existing `actions/upload-artifact@v4` Node.js 20 deprecation annotation was emitted while GitHub forced Node.js 24; it did not fail either run.

## Required checks

- locked restore;
- Release solution build with warnings as errors;
- all Core and Observation tests;
- format verification;
- direct and transitive NuGet vulnerability audit;
- Core project/reference audit;
- prohibited operation and runtime BossMod dependency scan;
- package manifest and contents inspection;
- Git diff and whitespace review.

Commands used:

```powershell
dotnet restore DDCore.slnx --locked-mode
dotnet build DDCore.slnx -c Release --no-restore
dotnet test DDCore.slnx -c Release --no-build --no-restore
dotnet format DDCore.slnx --verify-no-changes --no-restore
dotnet list DDCore.slnx package --vulnerable --include-transitive
dotnet list src/DDCore.Core/DDCore.Core.csproj reference
```

Static scans separately searched Core/Observation references and all production source for BossMod, Dalamud leakage into lower layers, FFXIVClientStructs, target/action/item operations, input state, hooks, scanners and NavMesh symbols. No forbidden match was found. This is a source/reference audit, not proof of live runtime behavior.

## Known gaps

- No live FFXIV/Dalamud test was performed.
- Public API observations cannot currently populate authoritative room, exit, trap, pomander or floor state.
- Synthetic decision correctness does not prove real-map routing or policy quality.
- The strategy engine is not connected to an execution gateway.
- CI success does not replace live FFXIV/Dalamud validation.
