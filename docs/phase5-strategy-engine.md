# Phase 5 DD state and strategy engine

## Scope and numbering

This document describes the user-defined Phase 5 implemented on `phase5/strategy-engine`. The original master plan calls decision logic Phase 4 and operation infrastructure Phase 5. The current explicit instruction takes precedence for naming: this phase completes a bounded, read-only strategy layer and does not implement the operation foundation.

No movement, combat action, target change, item use, object interaction, input injection, NavMesh operation or native hook is present. `DisabledExecutionGateway` remains the only execution gateway.

## State contract

`DeepDungeonStrategyState` is immutable and independent of Dalamud. It carries:

| Field | Representation | Fail-closed rule |
|---|---|---|
| Session, sequence, timestamp | Required header | Invalid or regressing observations stop |
| Dungeon kind and floor | `ObservedValue<T>` | Unknown or unsupported content stops |
| Current room and room graph | Complete 25-room immutable array | Unknown connections or malformed graph stop |
| Exploration and danger | Per-room enum with `Unknown` | Unknown exploration/danger is never treated as safe |
| Exit | Unknown / not found / found plus activation | Unknown discovery or activation stops |
| Enemies | Immutable observations with disposition and threat | Unknown current enemy state stops; unsafe rooms are excluded from routes |
| Traps | Immutable observations with danger | Unknown current trap stops; unsafe rooms are excluded from routes |
| Pomanders | Key, observed count and usability | Unknown item detail stops |
| Aggregate resource readiness | Unknown / sufficient / low / critical | Unknown stops; low/critical prevents new work |
| Remaining time | Observed duration | Unknown stops; caller-supplied minimum is enforced |

Known arrays reject defaults and duplicate room, enemy, trap or pomander identities. Counts, room indices, floors and time ranges are validated before evaluation.

## Strategy result

Every `StrategyDecision` returns:

- recommended action;
- human-readable reason;
- required preconditions;
- risk level;
- blocking or missing-information reasons;
- optional target room;
- immutable route.

Actions are `Stop`, `AvoidDanger`, `EngageEnemy`, `PreserveResources`, `MoveToExit`, `ExploreRoom` and `SearchForExit`. They are advice values only and are not execution intents.

## Deterministic priority

The engine evaluates the same immutable input in this order:

1. Reject same-session sequence/time regression or duplicate sequence.
2. Reject missing required observations, unsupported content or insufficient remaining time.
3. Validate the 25-room graph.
4. Escape a dangerous current room or trap through rooms confirmed safe.
5. Continue an already engaged enemy response.
6. Preserve low/critical resources before starting new combat or exploration.
7. Recommend a current-room hostile enemy when resources are sufficient.
8. Route to a confirmed active exit through rooms confirmed safe.
9. Route to the nearest safe unexplored room, breaking equal lengths by room index.
10. Recheck the current room if all known rooms are explored and the exit is absent/inactive.

The engine contains no wall clock, randomness, mutable search state or external service call. A minimum remaining-time threshold is an explicit `DeepDungeonStrategyPolicy` input; DDCore does not invent a BossMod threshold.

## BossMod reuse and provenance

The only BossMod-derived algorithm used here is the already extracted `RoomPathfinder`:

- repository: `https://github.com/awgil/ffxiv_bossmod`;
- fixed commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`;
- source: `BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs:5-60`;
- license: BSD-3-Clause;
- destination: `src/DDCore.Core/Navigation/RoomPathfinder.cs`;
- detailed provenance: `provenance/bossmod-phase4.json` and `THIRD-PARTY-NOTICES.md`.

The Phase 5 state and priority policy are DDCore-owned reimplementations. BossMod `DeepDungeonState.cs:5-60` and `FloorPathfind.cs:63-182` were used as evidence for required concepts, not copied as implementation. BossMod remains absent at runtime.

## Live-observation boundary

The verified public Dalamud API 15 adapter does not expose authoritative DD floor, room connections, passage/exit state, trap state or pomander inventory. Therefore Phase 5 is exercised with synthetic inputs only and is not wired to the live observation runtime. Missing live fields must remain Unknown until a separately verified adapter exists.

## Test matrix

Synthetic tests cover:

- missing observations and unknown item details;
- sequence/time regression and session changes;
- explicit remaining-time policy;
- dangerous room/trap retreat and no-route failure;
- engaged and hostile enemies;
- low and critical resource conservation;
- active, inactive, unknown and disconnected exits;
- safe-route detours around danger;
- nearest unexplored-room selection and deterministic tie breaking;
- unknown exploration/connections;
- duplicate identities and malformed state;
- repeated identical-input decisions.

Live DD behavior, route quality against real maps, policy tuning and long-run simulation remain unverified.
