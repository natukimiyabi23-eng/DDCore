# M0 — Eureka Orthos B1 map-source feasibility

## Result

**Gate: UNKNOWN (not PASS).** The reviewed public API and DDCore live diagnostics do not provide the complete, current randomized map and current-room identity required by the gate. The reviewed materials identify a candidate static geometry source (`planmap.lgb`) and BossMod's native map-state source, but this checkout contains no extracted EO B1 map fixture or independently labelled set of multiple random layouts. Thus this review cannot establish that no permitted solution exists, and does not label the source categorically BLOCKED. Current evidence is insufficient to prove the remaining conditions.

No implementation code, game operation, memory reader, or BFS connection was added. No live replay originals were required or read for this source-feasibility review.

## Scope and method

Reviewed the approved baseline commit `6d31b81738415e4799c0a8ea15df0b0c99a9ee78` and existing repository research/design: `docs/autonomous-development-roadmap.md`, `docs/phase5.6-implementation-plan.md`, `docs/phase5.6-environment-research.md`, `docs/phase5.6-environment-model.md`, `docs/phase5.6b-live-verification.md`, and `docs/DDCore_Codex_MasterPlan.md`. Inspected the checked-in plugin/core navigation and observation code and searched this checkout for `planmap.lgb`, `.lgb`, `.exd`, and map-parser assets. No raw game-process access or in-game test was performed. The conclusions below inherit the scope of the prior API 15 audit; they are not a claim that every possible public source has been exhaustively disproved.

## Evidence by source

| Source | Confirmed available | Not established by this source | Assessment |
|---|---|---|---|
| Dalamud API 15 public contracts | Local player pose; current generic spawned objects; territory/map/instance/duty identifiers; static game-data access. See pinned API audit in `docs/phase5.6-environment-research.md`. | Typed DD room index/graph, current randomized layout key, collision/navmesh enumeration, complete wall geometry, passage progress. | Public observation supports position and generic candidates, not a complete map. `MapId` is not a room graph. |
| Static `planmap.lgb` / Lumina | BossMod's `Rooms` implementation reads `planmap.lgb` and `DeepDungeonLayer` `EventRange` geometry; static room-box candidates are therefore a concrete research lead. | This review did not parse an EO B1 asset, confirm the coordinate transform against labelled B1 geometry, bind a static template to the current random selection, or establish current connections/walls from that asset alone. | Candidate only. Static geometry does not identify which randomized layout is active. |
| BossMod pinned source | At commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`, `WorldStateGameSync.UpdateDeepDungeon` reads `InstanceContentDeepDungeon` including floor/map flags; `FloorPathfind` consumes 25 room flags; `Rooms` combines flags with static geometry. See source links below. | Not evidence that these live flags are available through Dalamud's public API. | Its map path depends on FFXIVClientStructs/native state, which is patch-sensitive and outside this M0 permission boundary. BSD-3-Clause applies to any future copied/adapted code/data. |
| DDCore runtime and diagnostics | Player pose, enemy and generic environment object observations; schema-2 selected-object match and transition reporting. | Room membership, room edges, current map variant, complete obstacles and passage state. | Object samples do not by themselves define a floor graph. |
| Existing BFS | `RoomPathfinder` accepts/validates a complete 25-node reciprocal cardinal graph. | It does not discover rooms, map layout, current room or obstacles. | No valid live input currently reaches it. |

### Relevant source references

- BossMod [`WorldStateGameSync.cs` lines 941-990](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941-L990): native DD state synchronization.
- BossMod [`FloorPathfind.cs` lines 14-60](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs#L14-L60): graph input to path search.
- BossMod [`Rooms.cs` lines 14-53 and 69-163](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs): static room geometry and wall-related processing.
- DDCore `docs/phase5.6-environment-research.md` sections 3-6: API acquisition matrix and comparison.
- DDCore `docs/phase5.6-environment-model.md` section 8: topology/BFS validation gate.
- DDCore `src/DDCore.Core/Navigation/RoomPathfinder.cs`: complete graph validation and BFS.
- DDCore `src/DDCore.Plugin/Observation/DalamudEnvironmentObjectDiagnosticSource.cs`: generic object evidence, not semantic map inference.

## Five required facts

| Required item | M0 finding | Evidence state |
|---|---|---|
| Current room | A position can be observed with quality metadata. No checked source currently maps it to a verified room boundary for the active random floor. | **Unknown** |
| Room connectivity | Static boxes may describe candidate geometry; public live connection flags were not found in the audited contracts. | **Unknown** |
| Obstacles | No public complete collision/traversability source was established. Static wall data is a candidate requiring EO B1 extraction and runtime alignment validation. | **Unknown** |
| Active random layout | No supported key or matching procedure was verified that identifies the current B1 layout from the candidate templates. | **Unknown** |
| Complete BFS input | Missing confirmed active layout, current room, complete 25-room edge flags and validated safety overlays. | **Unavailable at present** |

## Offline verification

This checkout was searched for checked-in `.lgb`/`.exd` data and a `planmap.lgb` parser/fixture. None was found. The existing docs describe the BossMod extraction path, but do not include an EO B1 asset sample or golden labelled map. Therefore the following were **not tested**: room boundary reconstruction, player-to-room classification, distinguishing multiple random layouts, edge recovery, and BFS routes against real B1 ground truth. No synthetic result is presented as live or real-map validation.

The existing BFS requirements are concrete: exactly 25 rooms, valid reciprocal North/South/East/West edges within the 5x5 grid, confirmed current/destination rooms, and current-session/floor-epoch freshness. This M0 review found no evidence to satisfy those prerequisites from the present checked inputs.

## Alternative approaches

| Candidate | Needed to validate | Limitations / gate |
|---|---|---|
| Static `planmap.lgb` geometry plus public player pose | An authorized asset reader, extracted EO B1 candidates, coordinate transform, user-labelled positions/room boundaries over multiple layouts | Can help classify geometry only if active template and graph can be independently identified; otherwise keep room Unknown. Offline parser must be separately approved if new code is desired. |
| User-provided map/layout | Clear map image/data, coordinate frame, layout identity, and enough samples to label boundaries and edges | Manual input may be incomplete/stale; cannot assume every run/layout. Must include provenance and expire on floor/session change. |
| Public UI/map display interpretation | User-captured screenshots, stable visible room/connection semantics, mapping from pixels to world coordinates, multiple labels/locales and error bounds | This review did not verify a public structured UI map API or an image parser. Visual inference can be ambiguous and must fail closed. |
| Offline candidate-layout classifier | A corpus of candidate templates and several separately labelled EO B1 layouts; held-out testing | Candidate identification is not evidence until confusion/error rates and unknown rejection are measured. No such corpus was found in this checkout. |
| Native `InstanceContentDeepDungeon` read | Explicit separate authorization, current struct provenance/version validation, transition/re-entry fixtures, failure containment | Not a public API; patch-sensitive, policy and distribution risk. Not implemented or authorized here. |

## Decision and next gate

M0 is **UNKNOWN** because a specific static geometry lead and native reference exist, but no permitted end-to-end source has been validated for active layout, current room, connections and obstacles across multiple EO B1 layouts. It is not PASS. The audit also does not prove impossibility, so BLOCKED is not asserted as an absolute claim.

Recommended next step: a separately approved, offline-only `planmap.lgb` feasibility prototype with a small, licensed/provenanced EO B1 fixture set and user-labelled ground truth. Completion requires: (1) identify the asset/schema and coordinate transform; (2) enumerate room/wall candidates; (3) validate player-to-room assignment and active-layout identification on multiple distinct B1 layouts; (4) recover and verify all 25 graph nodes/edges against labels; (5) demonstrate deterministic BFS input and rejection of stale/ambiguous data. If the permitted static source cannot supply active topology, report the missing input and ask the user to choose among user-provided mapping, further policy review of another source, or stopping the autonomous route effort. Do not connect any candidate to BFS before that gate passes.

## Safety and limits

This is research, not legal advice. Existing DDCore research records Dalamud official distribution restrictions against automated combat/gameplay; an autonomous gameplay build cannot be presumed acceptable for official distribution. Review current official policies and FFXIV terms before any operational work. No movement, combat, target change, item use, interaction, input injection, private API, native memory reader, or BossMod runtime dependency is authorized by this report. Keep `DisabledExecutionGateway` and all unknown/stale fail-closed behavior.
