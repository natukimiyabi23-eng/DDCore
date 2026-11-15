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
| Route algorithm | `RoomPathfinder` computes a route for a supplied reciprocal 25-room grid. The offline probe can create an Unknown-only label template and validate a fully user-authored graph, but it does not discover live topology or classify an in-game route. | `src/DDCore.Core/Navigation/RoomPathfinder.cs`, `tools/LuminaMapProbe/ManualRoomGraphValidator.cs` |
| Strategy and execution boundary | Strategy logic has synthetic/offline coverage; game-operation gateway remains disabled. | `src/DDCore.Core/Decision/DeepDungeonStrategyEngine.cs`; `src/DDCore.Core/Execution/IExecutionGateway.cs` (`DisabledExecutionGateway`) |

## What the current integration does not do

- It does not identify the currently active Room A/B layout from a confirmed live source.
- Geometric containment of replay positions is an uncalibrated candidate only. Six local schema-5 EO recordings contain 869 snapshots; all positions are finite, but none records an exact floor. Three recordings have candidate containment and three have none. In the latest three, unique candidates were B-only in one recording but mixed A/B in two others; this cannot identify active layout because exact floor and independent layout labels are absent. There are no independent room labels or open-edge labels.
- The wall crosswalk is a static coordinate consistency check using nearest orientation-matched infinite lines. It is not a wall detector, collision model, connectivity map, or navigation mesh.
- No live room graph is produced, so the 25-room BFS is not connected to live observations and no live route is emitted.
- There is no integrated map overlay or live route preview in the plugin UI at this checkpoint.

## Current integration gate

The prototype may report pose, dungeon band, static-candidate availability, and diagnostic limitations. It must report current room, active template, open edges, collision/walkability, and live BFS eligibility as Unknown/Unavailable unless independently validated evidence is added. Do not change this gate based only on the current wall-distance results.

The next data milestone is a user-labelled capture that binds replay snapshot indices to the exact visible floor, active layout family, and room index, plus separately recorded edge states. The existing `--validate-room-labels` mode checks room-box candidates only; it does not validate edge states. Multiple distinct layouts and boundary/no-match cases are required before considering a live classifier.

## BossMod provenance

The wall template comparison reads the public `Walls.json` at fixed BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. It copies no BossMod code or data into the plugin and creates no runtime dependency. The existing BFS provenance and BSD-3-Clause notices remain in `THIRD-PARTY-NOTICES.md`.
