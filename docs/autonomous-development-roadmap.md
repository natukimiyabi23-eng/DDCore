# DDCore Autonomous Deep Dungeon Roadmap

## Goal and evidence policy

The long-term goal is a user-started system that can observe, plan, traverse, fight, interact with required objects, recover across floors, and stop safely. This is a target, not a statement of current capability. `Confirmed` below means supported by the checked repository code or prior recorded evidence; `Unknown` is not promoted to a positive assumption.

## Current baseline

| Area | Status | Evidence / limitation |
|---|---|---|
| DD kind and 10-floor band | Partial, confirmed for mapped content | Public duty/territory data mapping; not exact floor or room topology. |
| Exact floor | Diagnostic only, partial | Phase 5.5D saw EO 1->2 candidates in a live run; exact floor is not part of strategy input. Other DDs/floors unverified. |
| Player pose | Implemented, quality-gated | Public API adapter and nullable anonymous pose; missing/stale values fail closed. |
| Enemy/cast/mechanics | Partial | Public observation and EO Gancanagh AoE analyzer; not complete enemy threat/combat policy. |
| Generic environment objects | Diagnostic only | Stage 5.6A/B records generic object and selected-object correlation. It does not infer chest/passage state or topology. |
| Room graph and current room | Not available from verified public contract | No authoritative randomized floor graph/current-room value identified. Keep Unknown. |
| BFS | Implemented as pure 5x5 room-graph search | `RoomPathfinder` requires a trustworthy complete graph; live observations do not currently provide it. |
| Strategy | Implemented for synthetic complete state | `DeepDungeonStrategyEngine` returns advice; it is not connected to reliable live topology and does not execute. |
| Movement/combat/interactions | Not implemented | `DisabledExecutionGateway` is the only gateway. No movement, attack, target, item, or interaction operation exists. |
| Safety | Implemented foundation | Observer default, emergency stop, fail-closed validation; operation-capable gateway absent. |

References: `README.md`; `docs/phase5-strategy-engine.md`; `docs/phase5.6-environment-research.md`; `docs/phase5.6-environment-model.md`; `docs/phase5.6-implementation-plan.md`; `docs/phase5.6b-object-correlation.md`; `src/DDCore.Core/Navigation/RoomPathfinder.cs`; `src/DDCore.Core/Decision/DeepDungeonStrategyEngine.cs`; `src/DDCore.Core/Execution/IExecutionGateway.cs`; `src/DDCore.Plugin/Observation/DalamudEnvironmentObjectDiagnosticSource.cs`.

## EO B1 map feasibility gate (first priority)

Question: can DDCore construct a verifiable representation of the *current randomized* EO B1 map from permitted, validated sources?

| Required fact | Current finding | Gate treatment |
|---|---|---|
| Current room | No verified public room index or authoritative room geometry-to-player mapping | Unknown; cannot claim room identity. |
| Room-to-room connections | Public API research found no structured DD room graph; BossMod reads native `InstanceContentDeepDungeon` state | Unavailable through the investigated public contract; no inferred edges. |
| Walls/obstacles/traversable area | No public collision/navmesh enumeration found. `ScreenToWorld` is a raycast for a supplied screen ray, not a map query. | Unknown; do not route through guessed geometry. |
| Current randomized map identity/layout | Static `planmap.lgb` candidates may describe room geometry, but do not alone identify randomized current room layout/connection state | Candidate offline investigation only; current layout remains unidentified. |
| Complete input for BFS | BFS needs a validated 25-room connectivity map and current room | Not available; keep BFS disconnected from live observations. |

**Conclusion (evidence-based):** using only the investigated Dalamud API 15 public contracts and present live diagnostics, the five required facts are not all available; therefore an authoritative current map cannot presently be built and the EO B1 live BFS gate fails closed. This does not prove no alternative lawful/public data source exists. BossMod's native reads are evidence of its own input source, not proof of a public API. Static data parsing, visible UI interpretation, or user-supplied mapping are research candidates requiring independent accuracy and patch validation before use.

### Gate pass criteria

1. Identify a permitted source for current-floor layout and establish its version/provenance.
2. On EO B1, compare source-derived room IDs, boundaries and edges against a user-verified map for multiple distinct randomized layouts.
3. Show player-to-room classification, including boundaries and missing/invalid observations, with no false-safe classification.
4. Produce a complete validated 25-room graph; unknown cells/edges must block route output.
5. Replay the same captured inputs deterministically and compare expected room/edge labels. No game movement is involved.

Until all five pass, no live route or movement integration is permitted.

## Dependency order and shortest verifiable sequence

Phase numbering is retained as in the existing master plan; names clarify the current repository's user-defined numbering. The order is organized around the B1 proof, not around expanding diagnostics for their own sake.

### M0 — EO B1 map source feasibility (Phase 5.6 continuation; research/design)

- **Purpose:** decide whether a validated current-map source exists within approved public/supported inputs.
- **Inputs:** API 15 public contracts, existing object/player observations, static game-data candidates, user-labelled B1 map samples.
- **Work:** inspect data schemas and coordinate transforms offline; compare candidate sources; document licensing/ToS and patch sensitivity. No in-game automation or memory access.
- **Reuse:** Phase 5.6 acquisition matrix; Stage 5.6A/B generic objects and quality model.
- **Blockers:** current randomized-layout key, room graph/current room, collision data source.
- **Verification/completion:** source and limitations cited; multiple map fixtures labeled; explicit pass/fail of all EO B1 gates above.
- **Advance when:** a source passes the gate, or user approves a different bounded evidence approach. If not, stop and re-scope rather than fabricate topology.

### M1 — Read-only map reconstruction and route preview (Phase 5.6 implementation after approval)

- **Purpose:** create a trusted model from validated inputs and calculate routes without controlling the character.
- **Inputs:** validated floor graph, current room, destination, observation freshness/provenance.
- **Work:** source adapter, versioned graph model, conservative room classifier, live route preview only.
- **Reuse:** `RoomPathfinder`, immutable `WorldSnapshot`, coordinate-quality model.
- **Blockers:** M0 pass; reliable room/edge truth and reset detection.
- **Verification/completion:** replay/mock fixtures across layouts; route matches labeled shortest paths; unknown/stale/contradictory input yields no route; manual in-game route overlay comparison is user-run and read-only.
- **Advance when:** repeated B1 live comparisons pass without unsafe route claims and the user approves a movement-feasibility study.

### M2 — Movement feasibility and safety design (original Phase 5 operation foundation; design first)

- **Purpose:** establish whether authorized, stable movement control and obstacle-aware progress monitoring can be safely supported.
- **Inputs:** route waypoints, current pose, movement capability evidence, emergency-stop state, zone/session state.
- **Work:** compare only documented/supported movement interfaces; prototype in simulation; define immediate pre-side-effect safety revalidation, stop latency, stuck detection and recovery. No game input in this roadmap task.
- **Reuse:** execution contracts and `DisabledExecutionGateway`; emergency stop; map route model.
- **Blockers:** official plugin restrictions, ToS/anti-cheat risk, movement API availability/permissions, nav geometry.
- **Verification/completion:** selected method has a documented permitted interface and failure analysis; simulated hazards/stale pose/blocked route stop correctly; actual operation remains disabled pending explicit separate approval.
- **Advance when:** technical/legal review and user approval both exist; otherwise remain read-only.

### M3 — Live combat decision validation (Phase 3 mechanics + Phase 5 decision)

- **Purpose:** validate threat and mechanic decisions before considering any combat action.
- **Inputs:** fresh enemy identity/position/cast/status, verified mechanic definitions, player pose, safe geometry, job/action capability (if later approved).
- **Work:** grow coverage only from sourced mechanics; replay labeled observations; expose advice and unknown reasons. No combat actions.
- **Reuse:** mechanics registry, Gancanagh analyzer, hazard tracker, `DeepDungeonStrategyEngine`.
- **Blockers:** encounter coverage, target/actor lifecycle uncertainty, true collision/line-of-sight and health/status gaps.
- **Verification/completion:** labeled live/replay cases for each decision; deterministic expected danger and stop outcomes; no safe decision from missing data.
- **Advance when:** user accepts read-only decision quality and a separate policy review is approved.

### M4 — Object interaction and floor transition model (Phase 5.6 + 5.5 observation)

- **Purpose:** validate chest/device/passage semantics and invalidate stale floor state after transitions.
- **Inputs:** generic object identity, selected-target match, targetability/transitions, exact-floor diagnostics, user labels.
- **Work:** build versioned, provenance-backed semantic candidates; verify activation/open/transition semantics manually; preserve Unknown on conflicts.
- **Reuse:** Stage 5.6A/B schema 2 diagnostics; 5.5D chat-floor diagnostic (not strategy input yet).
- **Blockers:** only limited EO B1 examples; no causal interaction event; cross-floor identity/reset not demonstrated.
- **Verification/completion:** multiple independently labeled captures per object/state and transition; schema compatibility; floor change clears prior map/objects; no inference from disappearance alone.
- **Advance when:** required passage state and floor reset are authoritative for B1.

### M5 — EO B1-to-B2 end-to-end proof (integration of original Phases 3-5 and operation foundation)

- **Purpose:** only after gates pass, prove one user-started floor end-to-end with conservative stop behavior.
- **Inputs:** verified map/current room, route, enemy/mechanic state, passage condition, floor transition evidence, approved movement/combat/interaction adapters.
- **Work:** staged integration with operation adapters separately enabled and explicit user consent; begin in supervised/assisted scope if legally and technically permitted.
- **Reuse:** validated M1-M4 outputs, strategy engine, emergency stop, immediate safety gate.
- **Blockers:** every preceding evidence and policy gate plus distribution/ToS review.
- **Verification/completion:** repeatable EO B1 start-to-B2 transition, correct state reset, no uncommanded action, emergency stop demonstrated, fault injection stops on every missing prerequisite. Autonomous claims require multiple repeatable trials and explicit acceptance criteria.
- **Advance when:** this first milestone is accepted; only then propose more floors/DDs.

## System dependencies

```text
validated current map source + pose quality
  -> current-room/topology model
  -> deterministic BFS route preview
  -> movement feasibility/safety gate

enemy/cast/status + verified mechanics + geometry
  -> danger/threat decision
  -> (future, separately approved) action selection/execution

object semantics + exact-floor candidate + session reset
  -> floor objective and transition confirmation
  -> invalidate old floor graph/state

all validated observations + strategy + safety gate
  -> supervised B1 integration
```

Each edge is a dependency, not an implemented connection. The current environment diagnostics are not connected to BFS or strategy; the current strategy runs on complete synthetic state; execution is disabled.

## Compliance, distribution, timing

The repository's research cites Dalamud's plugin restrictions as disallowing automated combat/gameplay for official distribution. Full autonomous gameplay therefore cannot be assumed eligible for the official repository. FFXIV terms/policies and anti-cheat consequences require current official review before any operational prototype; this document is not legal advice and authorizes no bypass, memory access, injection, or operation. BossMod BSD-3-Clause source/data reuse needs provenance and notice compliance; no BossMod runtime dependency is proposed.

Calendar estimates are intentionally omitted: M0 may fail or reveal a new permitted source, while movement/combat authorization and technical dependencies are unresolved. Earliest useful milestone is a successful M0 gate, not a promised completion date.

## Immediate next work

Create an approved, offline-only EO B1 feasibility study: inventory candidate public static map assets and documented APIs, define coordinate/provenance fixtures, and compare them against user-labeled layouts if available. Do not parse private process memory, operate the game, connect observations to BFS, or implement movement. If no authoritative current-layout source emerges, report the B1 autonomous path blocked and present alternatives with their evidence and policy risks.
