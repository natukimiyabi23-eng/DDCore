# Phase 5 verification

## Scope

Verification applies to the user-defined Phase 5 DD state and read-only strategy engine. It does not cover live Dalamud behavior or any game operation.

## Results

| Check | Result |
|---|---|
| Locked restore | Passed for all five projects |
| Release solution build | Passed, 0 warnings / 0 errors |
| Core tests | 173 passed, 0 failed, 0 skipped |
| Observation tests | 40 passed, 0 failed, 0 skipped |
| Total tests | 213 passed |
| Format verification | Passed with no changes required |
| NuGet vulnerability audit | No known vulnerable direct or transitive package in all five projects |
| Core project references | None |
| Core assembly references | Framework assemblies only; no Dalamud, BossMod or ClientStructs |
| Static prohibited-operation scan | No targeting, action/item use, input, hook, scanner, ClientStructs or NavMesh operation symbol found in production source |
| Package | `0.5.0.0`, Dalamud API 15 |
| Package SHA-256 | `6EC28A9568A34B33221174CC4DE6E77E8E6A0EB4D8E3C6C60DBE8D06B46F2D74` |

## Real-data replay validation

Two local schema-version 3 exports (412 snapshots total) were decoded without modifying or committing the source JSON. All snapshots entered the read-only strategy adapter and engine; all returned fail-closed `Stop` decisions because floor/room/exit/resource observations were unavailable. The labelled candidate contributed 124 of those inputs. See [phase5-real-data-validation.md](phase5-real-data-validation.md) for the evidence, limitations and Phase 6 readiness decision.

The package contains `DDCore.dll`, Core and Observation assemblies, deps/manifest files, symbols, `THIRD-PARTY-NOTICES.md`, and Phase 3/4 provenance manifests. It contains no BossMod assembly.

The original Phase 5 push run `42412810484` and Draft PR run `42412814600` passed at verified head `bc2e5dc`. CI for the real-data validation update is recorded after its push; local locked restore, Release build, 213 tests, format and package audits passed before that push.

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
