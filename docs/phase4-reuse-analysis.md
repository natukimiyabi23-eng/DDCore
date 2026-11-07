# Phase 4 BossMod Deep Dungeon reuse analysis

## Scope and evidence

This analysis is pinned to BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. No conclusion in this document is based on the moving upstream branch.

The reproducible inventory is produced by:

```powershell
./tools/Measure-Phase4BossMod.ps1 -BossModPath ../ffxiv_bossmod
```

The script defines three explicit scopes:

- **DD direct:** every path matching `BossMod.Modules/.*/DeepDungeon/`.
- **DD support:** `BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs` and `BossMod/Data/DeepDungeonState.cs`.
- **Cross-cutting:** `BossMod/BossModule/AIHints.cs` and `BossMod/ActionQueue/ActionQueue.cs`.

Physical lines are every line emitted by `git show`, including blank lines. Dependency-family counts are lexical regex matches, not a compiler-semantic call graph. This limitation is intentional and is reported rather than hidden.

| Scope | Files | Physical lines | C# | JSON |
|---|---:|---:|---:|---:|
| DD direct | 63 | 27,521 | 59 | 4 |
| DD support | 2 | 432 | 2 | 0 |
| Cross-cutting | 2 | 656 | 2 | 0 |
| **Total** | **67** | **28,609** | **63** | **4** |

Lexical dependency-family incidence across the 67-file corpus:

| Family | Files containing a match |
|---|---:|
| BossMod world model | 51 |
| BossMod decision/action model | 33 |
| BossMod geometry | 30 |
| BossMod framework/services | 10 |
| FFXIVClientStructs/native DD types | 6 |
| Dalamud | 6 |
| Lumina | 3 |
| Embedded resource data | 2 |

These counts establish that most DD modules consume BossMod actor/world types, while a smaller but critical shared layer reaches native DD state, Dalamud IPC/UI and Lumina geometry.

## Required component analysis

The eight requested files total 2,249 physical lines.

| Component and pinned lines | Verified behavior | Main dependencies | Primary disposition |
|---|---|---|---|
| `FloorPathfind.cs:5-60` | N/S/W/E ordered BFS over a 25-room grid; result excludes start | Native `RoomFlags` only | **Dependency-removal adaptation**. Implemented as `RoomPathfinder` with a DDCore enum and validation. |
| `FloorPathfind.cs:63-182` | Selects room goals and emits goal zones for the next room/hallway | `AutoClear`, actor world, `AIHints`, room geometry/config | Reimplement after room and objective snapshots exist. |
| `AutoClear.cs:111-160,243-608` | Event state, chests/resources, target policy, hazards, interactions and navigation policy | `ZoneModule`, `WorldState`, `Actor`, `AIHints`, `ActionQueue`, config, IPC and resource data | Reimplement as separate observation, decision and intent layers; do not transplant the partial class. |
| `DeepDungeon.cs:46-243` | Transformation actions, ranged kiting, regen/emergency potion thresholds | Autorotation manager, actor status extras, job/category, action definitions and queue | Reuse policy evidence; reimplement against future immutable combat/resource inputs. |
| `DeepDungeonState.cs:5-60` | Dungeon/floor, 25 rooms, party rooms, pomanders, chests, magicite and passage/return state | Native `RoomFlags`, Lumina DD sheet, BossMod action IDs | Reimplement model and adapter. The current public Dalamud adapter cannot observe most fields. |
| `DeepDungeonState.cs:62-186` | World-operation events and replay serialization | `WorldState`, events and BossMod replay writer | Reimplement with DDCore snapshot/replay schemas. |
| `AIHints.cs:6-299` | Target priorities, forbidden/goal zones, interaction and queued-action exchange | BossMod actors, geometry, config and `ActionQueue` | Reimplement as typed, immutable decision outputs; direct reuse would import the BossMod architecture. |
| `AIHints.cs:301-530` | Path-map rasterization and targeting/goal helpers | BossMod pathfinding map, geometry and actors | Selective behavioral reference after DDCore navigation geometry exists. |
| `ActionQueue.cs:9-124` | Priority/deadline selection with cooldown, unlock, range and execution checks | World/client state, action definitions, cooldowns, actors and config | Phase 5 reimplementation behind execution safety contracts. Not usable in Phase 4. |
| `Rooms.cs:14-166` | Loads wall data and Lumina plan-map room rectangles; room adjacency/location | Native room flags, Lumina LGB/sheets, BossMod services/geometry | Reimplement an observation/import adapter; geometry cannot be directly reused. |
| `Rooms.cs:168-230` | ImGui diagnostic drawing | Dalamud ImGui and camera helpers | Not part of Core algorithm reuse. |
| `Traps.cs:15-78` | Loads traps through Palantir IPC, filters by room and removes triggered/revealed traps | Dalamud IPC, Palantir contract, actors, native room flags | Reimplement as optional provider with explicit availability and provenance. |

### Algorithms identified

- **Exploration:** room-goal selection (`PickARoom`), 5x5 BFS, next-room/hallway goal zones and passage selection.
- **Combat:** maximum-pull targeting, aggro/passive priority, petrified/low-HP overrides, transformation actions and ranged kiting.
- **Recovery:** regen potion threshold by role, emergency potion at 30%, external-heal suppression and incapacitating-status handling.
- **Resources:** silver/gold/bronze chest rules, overcap tracking, pomander usability, magicite/demiclone state and purity usage.
- **Hazards:** trap avoidance, circles/donuts/gazes/knockbacks/LOS/voidzones, interrupt/stun/spikes signals.
- **Action selection:** priority then expiry ordering, deadline-aware lower-priority insertion, unlock/cooldown/cast/range/LOS/inventory checks.

## WorldSnapshot input gap analysis

| Required input | Current DDCore state | Consequence |
|---|---|---|
| Actor identity, position, rotation, hitbox, HP, combat, target | Available, with partial-observation diagnostics | Supports geometry and a subset of target analysis. |
| Cast action, predicted finish, statuses | Available; status extra/stack and cast-specific ground position/rotation can be unavailable | Supports fail-closed hazard analysis, not all combat policy. |
| DD kind/floor | Public adapter cannot authoritatively observe it; EO10 is currently an explicit estimate | Strategy must remain unknown outside verified/imported context. |
| 25-room flags and player/party room | **Absent** | `RoomPathfinder` is testable but cannot run from live snapshots yet. |
| Room rectangles, walls and hallway geometry | **Absent** | Cannot convert a room route into spatial goals. |
| Passage/return activation and progress | **Absent** | Cannot select or confirm floor transition. |
| Pomander, magicite/demiclone, chest and hoard state | **Absent** | Resource and chest policy cannot be evaluated. |
| Player job/category, level, pending HP and transformation extra data | Partial/absent | Potion, kite and transformation policy is incomplete. |
| Aggro-player, targetability, treasure-open state | Partial/absent | BossMod target/object policy cannot be reproduced safely. |
| Action unlocks, cooldowns, animation lock, inventory quantity, LOS | **Absent by design** | `ActionQueue` is not portable into the current read-only Core. |
| Trap provider and authoritative trap positions | **Absent** | Trap policy remains unsupported, not safe. |

**Finding:** only the room-graph BFS is independent enough to extract now. Live use remains disabled because the room graph is not available through the verified public observation adapter.

## Extracted module and comparison

`src/DDCore.Core/Navigation/RoomPathfinder.cs` adapts BossMod `FloorPathfind.cs:5-60`:

- preserves the 5x5 grid and N/S/W/E traversal order;
- preserves shortest-path behavior and the result contract (start excluded, destination included);
- replaces native `RoomFlags` with `RoomConnectionFlags`;
- moves explored/queue state into each invocation;
- validates map size, indices, grid boundaries and reciprocal edges;
- uses predecessor reconstruction instead of copying a list for every queued path.

The actual BossMod type was not executed. It is `internal` to a project coupled to BossMod and the FFXIVClientStructs `RoomFlags` type. Instead, the test project contains a source-faithful transcription of lines 22-60 with only that enum substituted. The same valid graph fixtures and all 625 start/destination pairs on a deterministic 25-room tree are fed to both implementations. This proves matching single-call route results for that corpus; it does **not** claim binary-level equivalence or live-game equivalence.

The pinned implementation stores `Explored` and `Queue` on the object and does not reset them in `Pathfind`. DDCore deliberately uses per-call state and tests repeated calls. This is a documented behavioral hardening, not an assertion that the original repeated-call behavior matches.

## Measured reuse

Two non-interchangeable measures are reported:

1. **Component portability:** 1 of the 8 requested components can be adapted now without importing BossMod runtime architecture: **12.5%**. Directly reusable without dependency removal: **0 of 8 (0%)**. The other 7 require model/adapter/decision reimplementation.
2. **Reviewed-line extraction coverage:** the adapted source slice is lines 5-60 (56 physical lines) out of 2,249 physical lines in the eight requested files: **2.49%**. This measures reviewed source coverage, not identical copied lines or total system completion.

No percentage is projected onto the full 28,609-line corpus because JSON data, boss modules, shared infrastructure and algorithmic value are not comparable by raw line count.

## Phase 5-8 reassessment

The current master plan formally defines only Phases 1-6. To answer the requested Phase 5-8 estimate without pretending that Phases 7-8 already exist, the last two are proposed planning buckets and require user approval before implementation.

| Phase | Proposed scope | Estimated Codex tokens | Main driver |
|---|---|---:|---|
| 5 — operation foundation | No-op/recording executors, one Assisted adapter at a time, result evidence, timeout/retry/fault tests | 180k-300k | Patch-sensitive execution contracts and failure injection |
| 6 — one end-to-end vertical slice | EO bounded floor band, exploration/combat/resources/transition integration, simulation then Assisted trials | 260k-450k | Missing live DD state and long-horizon controller |
| 7 — dungeon expansion *(proposed)* | Remaining EO, HoH and PotD policy/mechanics/data; optional PT only after approval | 350k-650k | Encounter/resource breadth and replay corpus growth |
| 8 — hardening/recovery *(proposed)* | Long-run soak, reload/disconnect/death recovery, compatibility matrix and release evidence | 300k-600k | Rare failures and live verification dominate |
| **Total planning range** | Multiple usage windows; excludes user-run live play time | **1.09M-2.00M** | Includes implementation, tests, docs and roughly 20% contingency |

These are planning estimates, not an account balance or guarantee. The largest uncertainty is how authoritative room/resource state will be acquired without violating the public-API boundary. Phase 5 should not start until that decision and Phase 4 decision contracts are approved.

## Conclusion

BossMod contains substantial reusable **behavioral knowledge**, but very little is a drop-in library. The safe approach is to reuse the proven small algorithms and data evidence with provenance, while preserving DDCore's immutable snapshots, fail-closed unknown state and execution boundary. The Phase 4 extraction demonstrates that approach without adding movement, combat, target, item or interaction execution.
