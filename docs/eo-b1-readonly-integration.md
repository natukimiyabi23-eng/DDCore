# EO B1 Read-Only Integration Status

Updated: 2026-11-16

## Purpose and boundary

This note describes what can currently be exercised for an EO B1 read-only prototype. It is an implementation status record, not a claim that the plugin reconstructs or autonomously clears a floor. No movement, combat, item use, target change, interaction, input injection, private-memory read, or BossMod runtime dependency is enabled.

## Existing plugin capabilities

| Area | Current state | Evidence |
|---|---|---|
| Player pose and DD context | Public Dalamud observation is normalized with source/quality/reason metadata; unknown dungeon or exact floor remains unknown. | `src/DDCore.Plugin/Observation/DalamudObservationSource.cs`; `src/DDCore.Observation/LiveWorldSnapshotProvider.cs`; `src/DDCore.Core/World/WorldSnapshot.cs` |
| Enemies and hazards | Read-only actor/cast/AoE observations exist. Incomplete or unsupported mechanics are not treated as safe. | `src/DDCore.Plugin/Windows/MainWindow.cs` (`DrawObservation`); `src/DDCore.Core/Mechanics/` |
| Environment objects | Explicit opt-in, bounded anonymous diagnostic for EventObj/Treasure/Aetheryte and same-frame selected-object correlation exists. It does not infer semantic meaning from BaseId or disappearance. | `src/DDCore.Plugin/Observation/DalamudEnvironmentObjectDiagnosticSource.cs`; `src/DDCore.Plugin/Windows/MainWindow.cs` (`DrawEnvironmentObjectDiagnostics`) |
| Static EO map candidates | The offline Lumina probe loads the installed `planmap.lgb`, extracts Room A/B box candidates, and compares pinned BossMod wall-template coordinates. This data is not loaded by the plugin. | `tools/LuminaMapProbe/Program.cs`; `docs/phase5.6c-lumina-extraction-verification.md` |
| Route algorithm | `RoomPathfinder` computes a route for a supplied reciprocal 25-room grid. The offline probe's schema-3 labels bind screenshot-backed claims to local image hashes and replay snapshot context. It can report a partial offline route only over explicitly Open passages between explicitly Present rooms; unknown edges are excluded. It does not discover live topology or classify an in-game route. | `src/DDCore.Core/Navigation/RoomPathfinder.cs`, `tools/LuminaMapProbe/ManualRoomGraphValidator.cs`, `tools/LuminaMapProbe/ManualEvidenceCatalog.cs` |
| Strategy and execution boundary | Strategy logic has synthetic/offline coverage; game-operation gateway remains disabled. | `src/DDCore.Core/Decision/DeepDungeonStrategyEngine.cs`; `src/DDCore.Core/Execution/IExecutionGateway.cs` (`DisabledExecutionGateway`) |

## Enemy observation diagnostics (offline verification only)

The live read-only diagnostics summarize the observed enemy set with: confirmed resolved references to the local player (not a complete aggro count), enemies with/without a reported maximum HP, total observed status effects, and usable/unusable enemy positions. A bounded sample of up to 40 rows displays BaseId, HP, observed combat flag, resolved target relation, cast action/lifecycle/time, status IDs, distance to the player only when both positions are `Usable`, and usable position or quarantine/missing reason. Japanese cast-action and status names are looked up from public `IDataManager` Excel sheets for display only; failed or missing lookups fall back to numeric IDs. These labels do not classify mechanics or imply threat. Target relation and distance remain unknown when required observations are unavailable. This is not a threat classification, room assignment, or combat recommendation. No enemy names, source-status IDs, or raw runtime identifiers are included in the sample. Tests exercise the observation calculations using synthetic observations; no live verification was performed.

The installed Dalamud API 15 XML contract documents `IBattleChara.StatusList` and `IStatus` fields for status ID, parameter, source ID, and remaining time. It does not document an explicit stack-count property. DDCore continues to store stack count as unknown and does not reinterpret `IStatus.Param` as stacks. See `src/DDCore.Plugin/Observation/DalamudObservationSource.cs` and the local API reference `Dalamud.xml` in the installed API 15 hook directory.

## Offline schema-5 enemy/cast evidence

A read-only aggregation of the six local schema-5 EO exports (producer 0.5.3.0) covered 869 snapshots and 10,225 enemy observations. All had reported maximum HP; 8,214 enemy positions were usable and 2,011 were unavailable. The exports contained 1,303 cast observations across eight action IDs and 1,711 status observations across five status IDs. Four action IDs (32808-32811; 244 casts) are outside the existing Gancanagh analyzer's action set. The other 1,059 cast records share four action IDs present in that analyzer, but the exports have no exact floor and do not establish that all such casts belong to Gancanagh or share those encounter mechanics. These totals are observation counts, not unique enemies or verified threat labels.

The local `xivapi/ffxiv-datamining` `Action.csv` at commit `81fc999fefe3c77b32e2bdb4132d72371db2e66d` labels IDs 32808-32811 as Allagan Stone III, Allagan Tornado, Allagan Stoneskin, and Allagan Cure II. A search of the ten Eureka Orthos boss encounter scripts in BossModReborn commit `8d5ff8e2195ebbd519334852eab478414cdea67b` found no references to those four IDs. This is only a bounded source search: it does not prove the actions are absent from every BossMod module or identify their DD enemy/mechanic. The display-name resolver uses installed game sheets dynamically; these names are not treated as mechanic support.

BossModReborn public source was checked at commit `8d5ff8e2195ebbd519334852eab478414cdea67b`. Its [`FloorPathfind.cs`](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/8d5ff8e2195ebbd519334852eab478414cdea67b/BossMod/Modules/Global/DeepDungeon/FloorPathfind.cs) consumes `FFXIVClientStructs` `RoomFlags`; [`Walls.cs`](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/8d5ff8e2195ebbd519334852eab478414cdea67b/BossMod/Modules/Global/DeepDungeon/Walls.cs) reads `Palace.Floor`, `Palace.Progress.Tileset`, and `Palace.Rooms`; [`WorldStateGameSync.cs`](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/8d5ff8e2195ebbd519334852eab478414cdea67b/BossMod/Framework/WorldStateGameSync.cs) imports FFXIVClientStructs, hooks, and packet-related components. These acquisition paths are not transplanted into DDCore. Its `AutoClear` gameplay code is not reused. The repository's [`LICENSE`](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/8d5ff8e2195ebbd519334852eab478414cdea67b/LICENSE) declares BSD-3-Clause; no BossModReborn code or data was copied in this change.

## What the current integration does not do

- It does not identify the currently active Room A/B layout from a confirmed live source.
- Geometric containment of replay positions is an uncalibrated candidate only. Six local schema-5 EO recordings contain 869 snapshots; all positions are finite, but none records an exact floor. Three recordings have candidate containment and three have none. In the latest three, unique candidates were B-only in one recording but mixed A/B in two others; this cannot identify active layout because exact floor and independent layout labels are absent. There are no independent room labels or open-edge labels.
- The wall crosswalk is a static coordinate consistency check using nearest orientation-matched infinite lines. It is not a wall detector, collision model, connectivity map, or navigation mesh.
- No live room graph is produced, so the 25-room BFS is not connected to live observations and no live route is emitted.
- An offline partial route is only the shortest route in the known-open subgraph; unobserved edges could contain another or shorter route. If no known-open path exists while labels remain unknown, the result stays `incomplete` rather than claiming no route.
- There is no integrated map overlay or live route preview in the plugin UI at this checkpoint.

## Current integration gate

The prototype may report pose, dungeon band, static-candidate availability, and diagnostic limitations. It must report current room, active template, open edges, collision/walkability, and live BFS eligibility as Unknown/Unavailable unless independently validated evidence is added. Do not change this gate based only on the current wall-distance results.

The next data milestone is a user-labelled capture that binds replay snapshot indices to the exact visible floor, active layout family, and room index, plus separately recorded edge states. The existing `--validate-room-labels` mode checks room-box candidates only; it does not validate edge states. Multiple distinct layouts and boundary/no-match cases are required before considering a live classifier.

## BossMod provenance

The wall template comparison reads the public `Walls.json` at fixed BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. It copies no BossMod code or data into the plugin and creates no runtime dependency. The existing BFS provenance and BSD-3-Clause notices remain in `THIRD-PARTY-NOTICES.md`.
