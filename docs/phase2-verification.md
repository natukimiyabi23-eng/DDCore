# Phase 2 verification

## Scope

This record covers the standalone plugin foundation on branch `phase2/plugin-foundation`. It does not claim any Deep Dungeon mechanic, navigation, combat, resource-use or automation capability.

## Documentation reconciliation

Verified on 2026-11-07:

- The official Dalamud v15 documentation identifies API 15 and .NET 10 as the current baseline and recommends `Dalamud.NET.Sdk/15.0.0`.
- The current official SamplePlugin project at commit `b8477daaa678a5dd72cdcd4a32bc249d3c23a598` uses `Dalamud.NET.Sdk/15.0.0`, manifest metadata in the project file, `IDalamudPlugin`, `WindowSystem`, and `IPluginConfiguration`.
- The SamplePlugin README still mentions .NET 8 in its prerequisite text. That text conflicts with its current API 15 project and the official v15 documentation; DDCore follows the project file and v15 documentation, not the stale README line.
- DDCore documentation still described the repository as private after it was made public. README and continuation state were corrected.
- The architecture document described contracts as proposals. Phase 2 now implements their initial, non-operating forms.

## Local verification

Environment:

- .NET SDK: 10.0.302
- Dalamud SDK: 15.0.0
- Platform target: `net10.0-windows`, x64 for the plugin

Commands:

```powershell
dotnet restore DDCore.slnx
dotnet build DDCore.slnx -c Release --no-restore
dotnet test tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj -c Release --no-build --no-restore
dotnet format DDCore.slnx --verify-no-changes --no-restore
dotnet list DDCore.slnx package --vulnerable --include-transitive
```

Results:

- Release build: succeeded, 0 warnings, 0 errors.
- Unit tests: 26 passed, 0 failed, 0 skipped.
- Generated manifest: `InternalName=DDCore`, version `0.1.0.0`, Dalamud API level 15.
- Generated package includes `DDCore.dll`, `DDCore.Core.dll`, dependency metadata and the plugin manifest.
- Package SHA-256: `9518899C84EE86BB873F38AB19FA8EC26BC5F331AF5DA645AEE2669AAF3A55F0`.
- `DDCore.dll` references Dalamud, ImGui and `DDCore.Core`; it has no BossMod reference.
- `DDCore.Core` has no Dalamud or BossMod assembly reference.
- `DisabledExecutionGateway.IsAvailable` is always false and returns `ExecutionStatus.Disabled` for every request.
- Configuration load exceptions recover to Observer with the emergency-stop latch enabled and attempt one safe replacement save.
- Configuration save exceptions are logged; safe in-memory state remains active for the current session.
- Plugin initialization records every registered command, UI callback and window cleanup action and rolls them back in reverse order if construction fails.
- Format verification succeeded, and the NuGet audit found no known vulnerable direct or transitive package in all three projects.

## Tests covered

- Observer is the default mode.
- Advisor and Simulation are non-operating mode transitions.
- Assisted and Autonomous are rejected when execution is unavailable.
- Emergency stop returns to Observer and blocks other modes until reset.
- Reset does not arm an execution session.
- Saved modes are normalized to Observer on startup.
- Configuration version migration and persistence calls.
- Phase 2 execution gateway never executes.
- Core assembly dependency boundary excludes BossMod and Dalamud.
- Undefined modes are rejected without changing state.
- Execution-capable modes require explicit session authorization from Observer.
- Entering a non-operating mode, emergency stop, and restored emergency stop revoke authorization.
- Resetting emergency stop does not restore authorization; a fresh authorization is required.
- Load and save exceptions preserve an Observer/emergency-stopped in-memory state and emit error logs.
- The execution interface requires a fresh `IExecutionSafetyGate` at the gateway call boundary; the disabled gateway never requests authorization.

## Not yet verified

- Loading the built plugin inside a live Dalamud session.
- Visual rendering and Japanese font display in the live game UI.
- Actual Dalamud configuration persistence across a live plugin reload.
- Plugin unload/reload behavior in game.
- Corrupt-setting recovery, storage-write failure, and constructor rollback inside a live Dalamud session.
- GitHub Actions result for the review-fix commit; this becomes available after the branch is pushed and the PR workflow runs.

The user performs plugin enablement, reload and in-game verification. These items must not be reported as complete until observed.
