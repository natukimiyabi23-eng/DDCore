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

Phase 2 uses two production assemblies rather than creating ten mostly empty assemblies:

- `DDCore.Core`: game-independent modes, safety, configuration and the World/Mechanics/Navigation/Combat/Resources/Decision/Execution contracts.
- `DDCore` (`DDCore.Plugin` project): Dalamud lifecycle, Japanese UI, command registration, configuration persistence and logging adapters.

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

## Safety gates

- Execution disabled by default and after every load.
- One visible master stop control and one command-based emergency stop.
- Reject execution if state age exceeds a configured threshold.
- Reject execution outside recognized DD territory/content state.
- Reject execution when actor, room, destination, or target identity is ambiguous.
- Rate-limit repeated interaction/action attempts.
- Record every operation and its observed result.
- Do not silently fall back from advice to operation.

## Test architecture

- Unit tests for room graph, route scores, resource policy and state transitions.
- Golden snapshot tests for AoE/goal generation.
- Replay tests for every boss and representative non-boss floors.
- Adapter contract tests with fakes.
- In-game tests begin in Observer mode, then Advisor, Assisted, and only finally Autonomous.
