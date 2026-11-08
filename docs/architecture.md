# Architecture

## Design goals

- Separate observation, reasoning, simulation, and execution.
- Keep BossMod-derived code identifiable and attributable.
- Make every decision explainable from an immutable state snapshot.
- Default to no game operation.
- Permit deterministic replay tests without FFXIV running.
- Contain unsafe/native-memory access behind one adapter.

## Proposed projects

| Project | Responsibility | May operate the game |
|---|---|---|
| `DDCore.Plugin` | Dalamud entry point, lifecycle, commands, DI composition | No direct operation |
| `DDCore.World` | Immutable DD snapshots, observations, event normalization | Read-only |
| `DDCore.Mechanics` | AoE, cast, status, trap and boss mechanic models | No |
| `DDCore.Dungeons` | PotD, HoH, EO and later PT data/modules | No |
| `DDCore.Navigation` | Room graph, geometry, route and risk scoring | No |
| `DDCore.Combat` | Target/threat/engagement recommendations | No |
| `DDCore.Resources` | Pomander, magicite/demiclone, chest and consumable policy | No |
| `DDCore.Decision` | State machine and intent generation | No |
| `DDCore.Execution` | Movement, target, action, item and interaction adapters | Yes, only when enabled |
| `DDCore.UI` | Japanese UI, status, reasons, emergency stop | Only commands the core |

## Phase 2 physical layout

Phase 3.6 uses three production assemblies rather than creating ten mostly empty assemblies:

- `DDCore.Core`: game-independent modes, safety, configuration and the World/Mechanics/Navigation/Combat/Resources/Decision/Execution contracts.
- `DDCore.Observation`: normalized public-observation DTOs, session/entity/cast lifecycle tracking and `WorldSnapshot` conversion without a Dalamud reference.
- `DDCore` (`DDCore.Plugin` project): Dalamud lifecycle, Japanese UI, command registration, configuration persistence and logging adapters.

The live dependency direction is `DDCore.Plugin -> DDCore.Observation -> DDCore.Core`. Dalamud types stop at `DalamudObservationSource`; neither downstream assembly references Dalamud. Partial observations may produce hazard candidates, but their aggregate safety remains unknown.

Phase 3.7 keeps normal diagnostics and detailed recording separate. `ObservationDiagnosticLog` is a bounded in-memory state/error history, while `ObservationRecordingService` receives privacy-filtered immutable snapshots only after an explicit user start. It produces the existing replay schema locally, with bounded sampling and atomic interval flushes. Neither path depends on an execution adapter or a network client.

The logical boundaries are separate namespaces and interfaces. A responsibility should move to its own assembly when it gains concrete behavior or dependencies that require an enforceable compile-time boundary. This keeps the Phase 2 package small without discarding the target architecture.

## Dependency direction

```text
Plugin -> UI -> Decision -> {Navigation, Combat, Resources, Mechanics}
Plugin -> World
Dungeons -> {World, Mechanics}
Execution -> Decision intents + Dalamud/FFXIV adapters
Simulation -> recorded World snapshots + Decision (never Execution)
```

`World`, `Mechanics`, `Navigation`, `Combat`, `Resources`, and `Decision` must not depend on Dalamud UI or concrete input execution. `Execution` is the only layer allowed to translate an approved intent into a game-side operation.

## Runtime modes

| Mode | Observation | Advice | Simulated decisions | Game operation |
|---|---:|---:|---:|---:|
| Observer | Yes | No | No | No |
| Advisor | Yes | Yes | No | No |
| Simulation | Replay only | Yes | Yes | No |
| Assisted | Yes | Yes | Yes | Explicit user-triggered steps only |
| Autonomous | Yes | Yes | Yes | Yes, guarded and opt-in |

The initial configuration is `Observer`. Moving to Assisted or Autonomous requires an explicit per-session start action. Reload, error, territory mismatch, death, stale state, or adapter loss returns the system to a non-operating state.

## Principal interfaces

```csharp
public interface IDDObservationSource { DDObservation Capture(); }
public interface IDDDecisionEngine { DecisionFrame Evaluate(DDState state); }
public interface IDDExecutor { ExecutionResult Execute(ApprovedIntent intent); }
public interface IDDReplayStore { IEnumerable<DDObservation> Read(string replayId); }
```

Phase 2 implements the first versions of these boundaries under `DDCore.Core`, with more specific names such as `IWorldSnapshotProvider`, `IMechanicsAnalyzer`, `INavigationAdvisor`, `ICombatAdvisor`, `IResourceAdvisor`, `IDecisionEngine`, and `IExecutionGateway`. The execution implementation is deliberately limited to `DisabledExecutionGateway`.

## State machine proposal

`Inactive -> Observing -> Planning -> Navigating -> Engaging -> Fighting -> Recovering -> Interacting -> FloorTransition`

Any active state can transition to `Paused`, `EmergencyStopped`, or `Faulted`. Each transition records source state, triggering observation, selected intent, rejected alternatives, safety gate result, and outcome.

## BossMod reuse boundary

### Candidate concepts to port

- `DeepDungeonState` data concepts and replay operations.
- 5x5 room graph and `FloorPathfind` BFS behavior.
- Room geometry extraction and wall resources.
- AoE shapes, forbidden zones, goal zones, target hints, and boss modules.
- DD-specific enemy behaviors, transformations, potion and resource rules.

### Do not copy wholesale

`AutoClear` is tightly coupled to `ZoneModule`, `WorldState`, `Service`, `AIHints`, `ActionQueue`, camera rendering, configuration, Lumina, FFXIVClientStructs, and Palantir IPC. Copying it directly would recreate BossMod internally. The migration should first extract pure models and algorithms, then replace infrastructure dependencies with DDCore interfaces.

### Phase 4 proven extraction boundary

Phase 4 extracted only the deterministic 5x5 room-graph BFS into `DDCore.Core.Navigation.RoomPathfinder`. The Core type accepts a DDCore-owned `RoomConnectionFlags` array and has no BossMod, Dalamud, Lumina or FFXIVClientStructs dependency. Search state is per invocation and malformed graph inputs fail explicitly.

The current live observation boundary does not expose authoritative room flags, room geometry, passage/resource state or traps. Therefore this algorithm is an offline/testable building block only. No observation adapter feeds it and no decision or execution component consumes it yet.

### Phase 4.5 optional BossMod diagnostic

`DDCore.Observation` owns the BossMod-neutral `IBossModIpcInvoker` contract and fail-closed diagnostic result. `DDCore.Plugin` alone adapts public Dalamud IPC. The probe is optional, one-shot and read-only. It does not feed `WorldSnapshot`, `AnalyzerRegistry`, decisions or execution because the fixed BossMod IPC surface exposes no versioned DD strategy DTO.

Reachability and DD-data capability are independent fields. No future code may infer hazards, safe locations or strategy availability from `ReachableUnversioned`.

### User-defined Phase 5 strategy layer

The user-defined Phase 5 completes a bounded portion of the original master plan's Phase 4 decision-engine scope. It does not implement the original Phase 5 operation foundation. `DeepDungeonStrategyState` is an immutable, game-independent input model for dungeon kind, floor, the complete 25-room graph, exploration/danger state, exit, enemies, traps, pomanders, aggregate resource readiness and remaining time. Every observation can remain `Unknown`; unknown required data produces a stop decision rather than a default value.

`DeepDungeonStrategyEngine` is pure and deterministic. Its priority is stale/invalid input rejection, current danger avoidance, engaged-combat continuity, resource conservation, current-room hostile advice, active-exit routing and safe unexplored-room routing. Results expose action, reason, preconditions, risk, blocking reasons, target room and route. Time thresholds are supplied through an explicit policy and are not presented as BossMod facts.

The engine reuses `RoomPathfinder`, attributed to BossMod's fixed-commit `FloorPathfind.cs:5-60`, but adds no BossMod runtime dependency. Live public Dalamud observations still lack authoritative room connections, exit, traps, pomanders and floor state, so the engine is not connected to live operation and cannot authorize execution. `DisabledExecutionGateway` remains the only execution implementation.

### Phase 5.5 observation boundary

The Phase 5.5 research confirms that public Dalamud API 15 supplies actors, self pose, casts, statuses, territory/content identifiers and a boolean `InDeepDungeon` condition, but no verified typed floor, room-map, passage, trap, pomander or DD-timer contract. BossMod obtains most of those fields from `InstanceContentDeepDungeon` through FFXIVClientStructs, which is a high-risk internal source rather than a stable public service.

The next observation model must therefore add source, availability, timestamp, sequence and floor-epoch provenance. Missing pose must be distinct from a valid `(0,0)` coordinate. A confirmed floor-epoch change invalidates every room-, exit-, trap- and enemy-membership observation before strategy evaluation. Public and any future optional native sources terminate at a source-neutral fusion boundary; no native type or observation provider may access execution.

The full acquisition matrix, evidence and approval gates are in `phase5.5-observation-acquisition.md`. No Phase 5.5 production implementation is included in this branch.

## Safety gates

- Execution disabled by default and after every load.
- One visible master stop control and one command-based emergency stop.
- Reject execution if state age exceeds a configured threshold.
- Reject execution outside recognized DD territory/content state.
- Reject execution when actor, room, destination, or target identity is ambiguous.
- Rate-limit repeated interaction/action attempts.
- Record every operation and its observed result.
- Do not silently fall back from advice to operation.

### Mandatory execution-time revalidation

Every future operation-capable `IExecutionGateway` implementation must call the supplied
`IExecutionSafetyGate.RevalidateImmediatelyBeforeExecution` inside `TryExecute`, immediately
before its first external side effect. A safety decision made by the caller, decision engine,
or an earlier frame is stale and is not authorization.

The final validation must reject at least: an expired intent, an emergency-stop latch, an
unarmed session, a mode that does not permit execution, a missing execution adapter, and a
world snapshot sequence that differs from the intent. Validation and the first side effect
must remain in the same gateway call so no queue or asynchronous handoff can bypass the gate.
The raw operation adapter must not be exposed to UI or decision code. Phase 2's
`DisabledExecutionGateway` may return `Disabled` without invoking the gate because it cannot
produce a side effect.

### Configuration failure policy

- A load exception or incompatible stored object creates a new Observer configuration with
  the emergency-stop latch enabled. DDCore logs the exception and attempts one replacement save.
- If any save fails, the exception is logged and the current in-memory state is retained. A
  failed emergency-stop save therefore remains latched for the running session; every restart
  still starts in Observer even if persistence remains unavailable.
- Persistence failure never enables an operating mode and never falls back to an older saved
  mode. The UI status reports that changes may be session-only.
- Live recovery from malformed JSON and storage failures remains an in-game verification item.

## Test architecture

- Unit tests for room graph, route scores, resource policy and state transitions.
- Golden snapshot tests for AoE/goal generation.
- Replay tests for every boss and representative non-boss floors.
- Adapter contract tests with fakes.
- In-game tests begin in Observer mode, then Advisor, Assisted, and only finally Autonomous.
