# Research report

## Research baseline

- Observed on: 2026-11-07 JST
- BossMod repository: <https://github.com/awgil/ffxiv_bossmod>
- Commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- Commit time/message: 2026-11-03T20:00:37-05:00, `ok buddy`
- Branch at analysis: `master`
- License: BSD-3-Clause

Statements under **Fact** were verified from the pinned source, local build output, or official documentation. Statements under **Assessment** are design judgments or forecasts.

## 1. Current BossMod structure

### Fact

The solution separates `BossMod`, `BossMod.Modules`, `BossMod.Autorotation`, and `BossMod.Ultimate`. `BossMod.Modules` references the core `BossMod` project through `Module.targets`. The core project uses `Dalamud.NET.Sdk/15.0.0`; `global.json` pins .NET SDK 10.0.0 with feature roll-forward.

The DD-specific module inventory is 63 files/resources:

- Global shared: 10
- Palace of the Dead: 20
- Heaven-on-High: 10
- Eureka Orthos: 10
- Pilgrim's Traverse: 13

### Evidence

- [`BossMod.Modules.csproj`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/BossMod.Modules.csproj)
- [`Module.targets`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/Module.targets)
- [`BossMod.csproj`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/BossMod.csproj)

## 2. DD module dependency graph

### Fact

`AutoClear` derives from `ZoneModule`. Its partial classes consume `WorldState.DeepDungeon`, actors/events, `AIHints`, BossMod pathfinding/obstacle maps, action queue entries, camera drawing, config services, Lumina data, Dalamud ImGui, FFXIVClientStructs room flags and optional `Palantir.Traps` IPC.

`DeepDungeonState` stores dungeon ID, floor/tileset, gear levels, hoard, return/passage progress, 25 rooms, four party slots, 16 pomander slots, 16 chests and three magicite slots. `WorldStateGameSync.UpdateDeepDungeon` reads these from `InstanceContentDeepDungeon`.

`FloorPathfind` performs BFS over the 5x5 room map. Its source comment states the graph has one path between rooms and is small enough for brute force. `Rooms` reads `planmap.lgb` and `DeepDungeonLayer` to derive room boxes. `Walls.json` and `BadTraps.json` are embedded resources.

### Evidence

- [`AutoClear.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/AutoClear.cs)
- [`DeepDungeonState.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs)
- [`WorldStateGameSync.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941)
- [`FloorPathfind.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs)
- [`Rooms.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs)

### Assessment

The common DD code cannot be moved as a small isolated library without either porting a large part of BossMod core or introducing new contracts. Decomposition is safer than direct copy.

## 3. Dalamud API use

### Fact

Dalamud API 15 is current in the inspected official documentation and uses .NET 10. The official guidance recommends `Dalamud.NET.Sdk/15.0.0`; plugins implement `IDalamudPlugin` or `IAsyncDalamudPlugin`. The official technical guidance recommends the Windowing API for regular windows and Lumina for local game data.

The official API 16 page was also checked on 2026-11-07. It labels API 16 as not finalized, under development for Patch 8.0, with release date TBA. It is therefore a migration risk, not the DDCore implementation baseline.

FFXIVClientStructs is used by BossMod for native DD state and action hooks. These are not equivalent to stable, high-level Dalamud service contracts.

### Sources

- <https://dalamud.dev/versions/v15/>
- <https://dalamud.dev/versions/v16/>
- <https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IDalamudPlugin/>
- <https://dalamud.dev/plugin-development/technical-considerations/>

## 4. Reuse potential: AI, autorotation and movement

### Fact

`AIHints` is the shared exchange object for targets, forbidden zones, goal zones, interactions, queued actions, movement and other signals. `ActionQueue` prioritizes and validates candidate actions. `DeepDungeonAI` handles DD potion selection, transformation actions, ranged kiting and line-of-sight positioning advice. `AutoClear.CalculateAIHints` produces movement, interaction, target and item/action requests.

### Evidence

- [`AIHints.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/BossModule/AIHints.cs)
- [`ActionQueue.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/ActionQueue/ActionQueue.cs)
- [`DeepDungeon.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs)

### Assessment

The reusable asset is primarily the semantics and algorithms, not the concrete classes. DDCore should define its own `DecisionFrame`/intent contracts and adapters. Directly referencing BossMod would not satisfy independence.

## 5. Dungeon implementation range

### Fact

- PotD registers floor-zone modules 10 through 200 and has named boss files through 190.
- HoH registers 10 through 100 and has named boss files through 90.
- EO registers 10 through 100 and has named boss files through 90.
- PT registers 10 through 100, has bosses through 90, and a separate floor 99 module/resources.

Each dungeon floor module derives from shared `AutoClear` and adds dungeon-specific actions/status rules. Boss files build on BossMod boss-module components and state machines.

### Assessment

Compilation confirms structural compatibility at the pinned commit, not mechanic correctness or complete floor coverage. A support matrix must distinguish “file exists,” “replay tested,” and “in-game verified.”

## 6. Functions requiring new implementation

### Fact

BossMod already supplies DD state capture, room routing advice, hazard/target hints, some resource use, minimap and boss mechanics. It does not provide DDCore's proposed isolation boundaries, multi-mode contract, provenance registry, independent replay format, explicit execution approval model or DDCore-specific recovery state machine.

### Assessment

New implementation is required for:

- Independent plugin lifecycle/config/UI.
- Domain snapshots detached from BossMod `WorldState`.
- Decision explanation and confidence/source status.
- Adapter contracts and fail-closed safety gates.
- Replay-driven unit/integration test harness.
- Long-run recovery and operation audit log.

## 7. Build and test environment

### Fact

Local environment used .NET SDK 10.0.302 and the installed XIVLauncher Dalamud development assemblies. Command:

```powershell
dotnet build .\BossMod.Modules\BossMod.Modules.csproj -c Release
```

Result: success, 0 errors, 25 warnings, elapsed 1:52.88. Warnings included Roslyn analyzer RS1038 and NU1902/NU1903 advisories for transitive ImageSharp 3.1.12.

No `BossMod.Tests` directory was present, and a source search found replay parsing/recording for DD operations but no dedicated DD automated test suite.

### Assessment

DDCore needs unit tests from Phase 2 and replay fixtures before mechanic migration. Building upstream alone is not sufficient verification.

## 8. License and distribution

### Fact

BSD-3-Clause permits source/binary redistribution with conditions: preserve copyright, license conditions and disclaimer; do not use contributor names for endorsement without permission.

Official Dalamud restrictions state that automatic server interaction and combat/gameplay alteration are not accepted, and list automated activities as common non-starters. The official repository also requires combat elements to be informational and limited to normally known information.

### Sources

- <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/LICENSE>
- <https://dalamud.dev/plugin-publishing/restrictions/>
- <https://github.com/goatcorp/DalamudPluginsD17/blob/main/README.md>

### Assessment

Autonomous DDCore should be treated as private/custom-distribution work, not an official repository candidate. Observer/Advisor-only distribution would still require a separate compliance review.

## 9. Recommended implementation order and risks

1. Plugin shell, provenance and mode safety.
2. State snapshots and replay format.
3. Room graph/geometry in Advisor mode.
4. EO mechanic slice and replay tests.
5. HoH, PotD, then optional PT.
6. Decision engine and simulation soak tests.
7. Assisted adapters.
8. Autonomous integration only after explicit approval.

Highest risks are native-state drift after patches, hidden coupling to BossMod core, incomplete hazard knowledge, lack of upstream DD tests, long-run decision compounding, and distribution/account risk from automation.

## 10. Phase completion conditions

The detailed per-phase conditions are in [DDCore_Codex_MasterPlan.md](DDCore_Codex_MasterPlan.md). In summary: every phase requires reproducible evidence, replay/unit tests appropriate to its layer, explicit unsupported-state behavior, and user approval before implementation advances.

## Feasibility conclusion

### Fact

The pinned upstream source builds, and substantial DD observation, routing, resource and mechanic logic exists.

### Assessment

An independent Observer/Advisor plugin is technically feasible. Full autonomy is also technically plausible, but it is a large, high-risk rewrite rather than a simple extraction. The project should be approved one safety-gated slice at a time; claiming complete automatic clearing before a replay corpus and recovery testing would not be supported by current evidence.

## Open questions

- Whether Pilgrim's Traverse is included in DDCore v1 scope.
- Whether BossMod may remain installed as an optional runtime provider, or independence must be absolute.
- Which movement/action providers, if any, are permitted as optional adapters.
- Which solo job and dungeon/floor range defines the first end-to-end target.
- Whether Autonomous mode is for private local use only; official distribution is not a viable default assumption.
