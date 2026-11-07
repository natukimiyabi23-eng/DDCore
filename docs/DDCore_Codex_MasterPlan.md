# DDCore — Codex Master Plan

## Purpose and scope

Create a standalone, extensible Dalamud plugin for Final Fantasy XIV Deep Dungeons, based on proven BossMod concepts while separating BossMod-derived code from DDCore-original code.

This plan covers research and phased implementation design. Phase 1 research is complete; Phase 2 foundation work is implemented on `phase2/plugin-foundation` for review and is not merged into `main` yet.

## Fixed technical policy

- Language: C#
- Platform baseline: Dalamud API 15, .NET 10
- Upstream baseline: BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- Default runtime mode: Observer; all game operation disabled
- Architecture: observation, advice/simulation, decision, and execution are separate
- Provenance: BossMod-derived files and DDCore-original files remain identifiable
- Safety: emergency stop and fail-closed behavior are mandatory before any operation mode

## Research baseline and evidence policy

### Verified baseline

- Research date: 2026-11-07 JST.
- BossMod repository: <https://github.com/awgil/ffxiv_bossmod>.
- Inspected BossMod commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0` (`master`, commit message `ok buddy`). A fresh fetch on the research date confirmed the same remote head.
- BossMod version in `Common.targets`: `7.5.7.20`.
- BossMod license: BSD-3-Clause.
- Current stable Dalamud baseline: API 15, .NET 10. The API 16 page explicitly identifies API 16 as not finalized and under development for Patch 8.0, so API 16 is a forward-compatibility risk rather than the implementation baseline.
- Local verification environment: .NET SDK 10.0.302 and XIVLauncher development assemblies.
- Verification build: `dotnet build .\BossMod.Modules\BossMod.Modules.csproj -c Release` completed with 0 errors and 25 warnings in 1:52.88.
- Located DD-specific inventory: 63 source/resource files: 10 global, 20 Palace of the Dead, 10 Heaven-on-High, 10 Eureka Orthos, and 13 Pilgrim's Traverse.

### Evidence rules

- **Verified fact** means observed in the pinned source, successful local build output, or an official source linked here.
- **Assessment** means a DDCore design decision, risk forecast, or implementation proposal; it must not be read as an existing BossMod or Dalamud capability.
- A source file existing or compiling does not prove mechanic accuracy, full floor coverage, or current in-game behavior.
- Native layout offsets and FFXIVClientStructs members are patch-sensitive. They require re-verification whenever the game, Dalamud API, or FFXIVClientStructs changes.
- Future implementation must record provenance at file or algorithm level before copying or adapting BossMod code.

### Primary official sources

- [BossMod source at the pinned commit](https://github.com/awgil/ffxiv_bossmod/tree/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0)
- [BossMod BSD-3-Clause license](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/LICENSE)
- [Dalamud API 15 release information](https://dalamud.dev/versions/v15/)
- [Dalamud API 16 preview; not finalized](https://dalamud.dev/versions/v16/)
- [Dalamud plugin development entry point](https://dalamud.dev/plugin-development/getting-started/)
- [Dalamud technical considerations](https://dalamud.dev/plugin-development/technical-considerations/)
- [Dalamud plugin restrictions](https://dalamud.dev/plugin-publishing/restrictions/)

## Verified BossMod source map

| Concern | Pinned source | Verified role | DDCore implication |
|---|---|---|---|
| DD state model | [`BossMod/Data/DeepDungeonState.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs) | Models dungeon ID, progress, 25 rooms, four party slots, 16 pomander slots, 16 chests and three magicite slots | Recreate as immutable DDCore snapshots; do not expose BossMod mutable state across layers |
| Native synchronization | [`BossMod/Framework/WorldStateGameSync.cs#L941`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941) | Reads `InstanceContentDeepDungeon` and emits state operations | Isolate native reads in `DDCore.World`; validate ranges before publishing snapshots |
| Shared floor logic | [`BossMod.Modules/Global/DeepDungeon/AutoClear.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/AutoClear.cs) | `ZoneModule` integrating actors, hazards, goals, targets, actions and interactions | Extract behavior behind DDCore contracts rather than copy the class wholesale |
| Room graph routing | [`FloorPathfind.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs) | BFS over the 5x5 room graph | Port as a pure deterministic navigation component with graph tests |
| Geometry and walls | [`Rooms.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs), [`LevelData.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/LevelData.cs), `Walls.json` | Reads `planmap.lgb` and `DeepDungeonLayer`; stores room and wall data | Put data extraction/build tooling outside runtime where possible; version generated data |
| Trap data | [`Traps.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Traps.cs), `BadTraps.json` | Tracks visible/revealed traps and optional `Palantir.Traps` IPC | Mark information source and confidence; function without Palantir and never assume hidden traps are known |
| Decision exchange | [`BossMod/BossModule/AIHints.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/BossModule/AIHints.cs) | Carries potential targets, forbidden/goal zones, interactions, queued actions and movement | Replace with immutable `DecisionFrame` and typed intents |
| Action arbitration | [`BossMod/ActionQueue/ActionQueue.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/ActionQueue/ActionQueue.cs) | Prioritizes and validates action candidates | Keep policy selection separate from execution acknowledgement |
| DD combat policy | [`BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs) | Potion selection, transformations, ranged kiting and line-of-sight positioning | Migrate as explicit, testable policy rules after world/mechanic layers exist |
| DD actions | [`BossMod/ActionQueue/ActionDefinition.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/ActionQueue/ActionDefinition.cs), [`BossMod/Framework/ActionManagerEx.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/ActionManagerEx.cs) | Registers pomander/magicite actions and hooks native use functions | Treat hooks as Phase 5, patch-sensitive execution adapters, not domain logic |
| Replay serialization | [`BossMod/Replay/ReplayParserLog.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Replay/ReplayParserLog.cs) | Parses `DDPG`, `DDMP`, `DDPT`, `DDIT`, `DDCT`, and `DDMG` operations | Define a versioned DDCore replay schema and conversion tooling; do not depend permanently on BossMod log internals |
| UI/config | [`ModuleUI.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/ModuleUI.cs), [`Minimap.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Minimap.cs), [`Config.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Config.cs) | Renders DD state and exposes AutoDD configuration | Build a Japanese-first DDCore UI; never inherit an operation-enabled default |

## Proposed dependency direction

```text
DDCore.Plugin  -> DDCore.UI, DDCore.World, DDCore.Decision, DDCore.Execution
DDCore.UI      -> read-only view models
DDCore.World   -> Dalamud/Lumina/FFXIVClientStructs adapters
DDCore.Dungeons -> DDCore.Mechanics, DDCore.World abstractions
DDCore.Mechanics -> pure geometry/event contracts
DDCore.Navigation -> DDCore.World + DDCore.Mechanics
DDCore.Combat  -> DDCore.World + DDCore.Mechanics
DDCore.Resources -> DDCore.World
DDCore.Decision -> Navigation + Combat + Resources; emits typed intents
DDCore.Execution -> consumes approved intents; never referenced by pure policy projects
```

The direction above is an **assessment**. The essential rule is that observation and policy never call game-operation APIs directly. `DDCore.Execution` must be replaceable by a no-op recorder for Observer, Advisor and Simulation modes.

## Phase 1 — BossMod source analysis

### Implementation target

- Inventory all DD source/resources and their upstream commit.
- Build the pinned source.
- Trace DD state ingestion, room graph, geometry, traps, AoE, targets, actions and UI.
- Establish license/provenance obligations.

### Dependencies

- BossMod repository and BSD-3-Clause license.
- Dalamud API 15, .NET 10, FFXIVClientStructs, Lumina.
- Current game structures used by `InstanceContentDeepDungeon`.

### Verified basis

- `BossMod.Modules` references the core `BossMod` project through `Module.targets`; DD modules are not an independent package.
- `AutoClear` is split across `AutoClear.cs`, `Rooms.cs`, `Traps.cs`, `ModuleUI.cs` and `FloorPathfind.cs`, demonstrating cross-cutting responsibility rather than a portable unit.
- `WorldStateGameSync.UpdateDeepDungeon` reads native state and emits `DeepDungeonState` operations; replay parsing already preserves the main DD state transitions.
- The pinned source built successfully, but no dedicated `BossMod.Tests` project or DD-specific automated test suite was located. Replay support is evidence infrastructure, not proof of test coverage.

### Implementation approach

1. Generate and commit a machine-readable source inventory containing path, upstream commit, classification, license origin and proposed destination.
2. Build a directed dependency graph from project references, `using` directives, base types and service access.
3. Classify every dependency as pure algorithm, data model, Dalamud service, Lumina data, native structure, rendering, optional IPC or execution hook.
4. Preserve the upstream build command and warnings as a reproducible baseline.
5. Create architecture-decision records for every dependency that will be copied, adapted, wrapped or replaced.
6. Reject migration of any member whose source, semantics or current validity cannot be demonstrated.

### Technical risks

- `AutoClear` spans many BossMod subsystems and is not independently portable.
- Unsafe/native structure reads can break after game/API updates.
- Existing DD code has no located dedicated automated test project.
- Palantir trap IPC is optional and may be absent.

### Test method

- Reproduce a clean Release build.
- Record file counts, paths, class names and dependency graph.
- Compile a provenance table with pinned permalinks.
- Compare observed architecture against source, not UI assumptions.

### Completion criteria

- Pinned commit, license, build result and all DD paths are recorded.
- Shared versus dungeon-specific code is classified.
- Unknowns and optional dependencies are explicit.

### Research status

Completed for initial planning. The pinned `BossMod.Modules` Release build succeeded with 0 errors and 25 warnings. Sixty-three DD files/resources were identified.

## Phase 2 — Standalone plugin foundation

### Implementation target

- Create the proposed solution/projects.
- Implement Dalamud lifecycle, Japanese UI, configuration, logging and command surface.
- Implement mode gating and emergency stop.
- Add provenance/license files and CI.

### Dependencies

- `Dalamud.NET.Sdk/15.0.0` and Dalamud Windowing API.
- Minimal selected libraries only; no wholesale BossMod package graph.

### Verified basis

- Official API 15 documentation specifies .NET 10 and recommends `Dalamud.NET.Sdk/15.0.0`.
- API 15 supports `IDalamudPlugin`; `IAsyncDalamudPlugin` exists but is described as experimental even though its interface is stable.
- API 15 requires the packaged manifest inside the plugin archive to be accurate.
- API 16 is not finalized. DDCore should not target its preview namespaces or event signatures yet, but the migration risk must be tracked.

### Implementation approach

1. Create the ten proposed projects with explicit references matching the dependency direction above.
2. Start with `DDCore.Plugin`, `DDCore.World`, `DDCore.UI` and test projects only; add other assemblies when a concrete dependency requires them.
3. Use dependency injection for services and expose immutable read models to UI.
4. Store configuration with a schema version and migration functions. New installs and unknown/corrupt settings resolve to Observer mode.
5. Register Japanese UI strings through a localization catalog rather than embedding mixed-language labels in logic.
6. Implement a global mode controller, emergency stop state and execution capability registry before any execution adapter exists.
7. Add `THIRD-PARTY-NOTICES`, provenance records, deterministic build settings and CI restore/build/test/package jobs.

### Technical risks

- Accidentally recreating BossMod as a hidden monolith.
- Enabling operation through default config or stale persisted state.
- Official distribution incompatibility if automation is included.

### Test method

- Build/package on a clean environment.
- Verify default mode is Observer after install, update, reload and config migration.
- Unit-test mode transitions and emergency stop.
- Verify no execution service is resolved in non-operating modes.

### Completion criteria

- Plugin loads without BossMod installed.
- Japanese UI shows mode, current DD state and safety status.
- No game operation occurs in default configuration.
- License/provenance audit passes.

### Phase 2 implementation status

- Source/build verified: independent API 15 plugin and core assemblies compile with 0 warnings and 0 errors.
- Unit verified: 26 tests pass for startup mode, configuration migration/persistence failures, safety transitions, emergency stop, reauthorization boundaries, initialization rollback and disabled execution.
- Package verified: the generated manifest reports API level 15 and the archive contains DDCore plus its game-independent core assembly.
- Dependency verified: no BossMod assembly/package reference; the core assembly also has no Dalamud reference.
- Static safety verified: no FFXIVClientStructs, interop hook, action, target or input implementation is present.
- Review state: PR #1 is a Draft from `phase2/plugin-foundation` to `main`; no merge has been performed.
- Live Dalamud load, Japanese UI rendering and live reload persistence remain unverified and require user-side in-game verification.

## Phase 3 — DD mechanic migration

### Implementation target

- Port common AoE shapes, forbidden/goal-zone representation and boss state logic.
- Support Palace of the Dead, Heaven-on-High and Eureka Orthos as requested.
- Treat Pilgrim's Traverse as a separately approved scope addition.
- Port room walls, trap representation and boss modules incrementally.

### Dependencies

- Stable `DDCore.World` snapshots and replay format.
- Mechanic contracts independent of Dalamud rendering.
- Annotated replay fixtures.

### Verified basis

- PotD has 20 located DD files/resources and floor-zone registrations through floor 200.
- HoH and EO each have 10 located files/resources and floor-zone registrations through floor 100.
- Current BossMod also models Pilgrim's Traverse with 13 files/resources, `DungeonType.PT`, floor modules through 100 and separate floor-99 resources.
- Dungeon floor modules inherit shared `AutoClear`; boss modules depend on BossMod components and state-machine infrastructure.
- `Rooms.cs` and the wall resources provide navigation geometry, while boss modules provide encounter-local hazards. These are different data lifecycles and should not share one mutable service.

### Implementation approach

1. Define pure mechanic primitives: circles, donuts, cones, rectangles, knockbacks, line-of-sight blockers, activation windows and confidence/source metadata.
2. Build a BossMod-to-DDCore compatibility layer only inside migration tooling; runtime dungeon modules target DDCore primitives directly.
3. Port one vertical slice at a time: state fixture, encounter events, expected hazards, expected safe regions, UI projection and replay assertions.
4. Version room/wall/trap data independently from code and retain generation provenance.
5. Maintain a support matrix with states `not inventoried`, `inventoried`, `ported`, `replay tested`, and `in-game verified`.
6. Treat optional hidden-trap IPC as an enrichment source. Missing IPC must reduce confidence, never block basic operation or fabricate certainty.
7. Require explicit approval before adding Pilgrim's Traverse to the first release scope.

### Technical risks

- Boss modules rely on BossMod actor/events/components/state-machine infrastructure.
- Hidden or inferred traps can be incomplete without Palantir data.
- Some mechanic knowledge may be outdated or incomplete despite compilation.
- Visible advice must not claim certainty when its source is incomplete.

### Test method

- Golden tests for generated AoEs, safe/unsafe zones and activation timing.
- Replay tests for each boss file and selected non-boss enemies.
- Compare Advisor output with source BossMod at the pinned commit.
- Track unsupported mechanics explicitly.

### Completion criteria

- Requested three DDs have an audited support matrix.
- Every supported boss has passing replay fixtures.
- Unknown/unsupported mechanics display a non-safe warning.
- Observer/Advisor operation remains input-free.

### Detailed Phase 3 design status

The implementation-ready, non-operating design is recorded in [phase3-mechanics-design.md](phase3-mechanics-design.md). P3.0 through P3.5 are implemented on `phase3/mechanics-foundation`: immutable `WorldSnapshot`, six serializable geometry concepts, bounded JSON replay/mock support, deterministic analyzer contracts, fail-closed hazard output and the Eureka Orthos floor 10 Gancanagh vertical slice. Phase 3.6 adds a public-API-only, read-only observation adapter on `phase3/live-observation-adapter`; its direct, derived and unavailable fields are documented in [phase3-live-observation.md](phase3-live-observation.md). Live Dalamud and live encounter behavior remain unverified. See [phase3-verification.md](phase3-verification.md).

## Phase 4 — Strategy decision engine

### Implementation target

- Recognize floor, room, exploration, enemies, passage/return, chests and resources.
- Produce routes, target priorities, combat/resource recommendations and state transitions.
- Provide reason codes and rejected-alternative diagnostics.

### Dependencies

- World snapshots, navigation geometry, mechanic hazards and resource inventory.
- Deterministic clock/random abstractions for simulation.

### Verified basis

- BossMod's `AIHints` combines targets, forbidden zones, goal zones, interactions and queued actions in one exchange object.
- `FloorPathfind` is a deterministic BFS over a small 5x5 room graph.
- `DeepDungeonAI` contains DD-specific potion, transformation, kiting and line-of-sight policy, but it operates inside BossMod's autorotation framework.
- BossMod supplies useful policy evidence but not DDCore's proposed explainable, mode-gated state machine.
- Phase 4 measurement found 67 scoped files / 28,609 physical lines and strong coupling to BossMod world, decision and geometry types. Of the eight named components, only the 5x5 room BFS was portable after dependency removal; see `phase4-reuse-analysis.md`.

### Implementation approach

1. Publish an immutable `WorldSnapshot` with timestamp, territory, dungeon/floor identity, player/party, rooms, actors, hazards, resources and observation quality.
2. Convert observations into candidate intents such as `ExploreRoom`, `ApproachTarget`, `EngageTarget`, `OpenChest`, `UseResource`, `ActivatePassage`, `Wait` and `EmergencyStop`.
3. Score candidates with hard safety constraints first, then survival, objective progress, resource economy and travel cost.
4. Emit a `DecisionFrame` containing the selected intent, alternatives, rejected reasons, source timestamps and confidence.
5. Keep the state machine deterministic under a supplied clock and random seed.
6. Add stall detection using repeated state/intent fingerprints and elapsed progress thresholds.
7. Define party and solo policy profiles separately; do not infer party authority or coordination from the solo design.

### Technical risks

- Partial observability: the safest decision may require information not available.
- Route scoring can conflict with combat safety and resource conservation.
- Solo and party policies differ materially.
- Local optima can cause loops or resource exhaustion over many floors.

### Test method

- Scenario table tests for room graphs and competing goals.
- Property tests: no route through hard obstacles; no invalid resource count.
- Deterministic replay and counterfactual simulation.
- Loop/stall detection tests.

### Completion criteria

- Every decision has a reason and confidence/source status.
- State machine has no unhandled transition in the scenario corpus.
- Simulation can complete representative floor sets without invoking execution.
- Unsafe/ambiguous states produce pause, not a guessed action.

### Phase 4 extraction checkpoint

`RoomPathfinder` is the first pure strategy building block. It preserves the pinned BossMod traversal order and route contract, is compared against a source-faithful test oracle, and remains disconnected from live observation because authoritative room flags are not available. This checkpoint is not completion of the decision engine and does not authorize Phase 5.

## Phase 5 — Operation foundation

### Implementation target

- Implement movement, targeting, action, item, object-interaction and transition adapters.
- Add result verification, timeouts, retry budgets and emergency stop.
- Begin with Assisted mode; Autonomous remains locked.

### Dependencies

- Approved intents from the decision engine.
- Stable Dalamud/FFXIVClientStructs adapters and optional IPC contracts.
- Operation audit log.

### Verified basis

- BossMod registers pomander and magicite action IDs in `ActionDefinition.cs`.
- `ActionManagerEx.cs` hooks native `InstanceContentDeepDungeon.UsePomander` and `UseStone` functions, which confirms the upstream implementation uses patch-sensitive native execution paths.
- Official Dalamud restrictions reject automatic server interaction and automated gameplay for official repository submissions. They also list automated activities and skipped dialogs as common non-starters.
- Most ready-to-use Dalamud APIs are read-only; the presence of local technical capability does not imply official distribution approval.

### Implementation approach

1. Define narrow adapters: `IMovementExecutor`, `ITargetExecutor`, `IActionExecutor`, `IItemExecutor`, `IObjectExecutor` and `ITransitionExecutor`.
2. Require each request to contain snapshot version, expiry, idempotency key, expected preconditions and success evidence.
3. Route requests through a single execution coordinator that checks mode, emergency stop, focus/territory/duty identity, stale state and retry budget.
4. Acknowledge outcomes as `Succeeded`, `Rejected`, `TimedOut`, `Interrupted`, `Stale` or `Unknown`; never treat a dispatched request as success.
5. Implement no-op and recording executors first, then one Assisted adapter at a time.
6. Keep native hooks and optional IPC in isolated packages with health checks and version gates.
7. Log intent and result without account identifiers, secrets or unnecessary player information.
8. Produce a separate distribution decision before any automated adapter is packaged.

### Technical risks

- Game patch/API changes, latency, animation locks, UI state and stale targets.
- Repeated or unverified interactions can produce harmful loops.
- Movement can desynchronize from room and hazard models.
- This phase conflicts with official Dalamud repository automation restrictions.

### Test method

- Adapter fakes and contract tests first.
- In-game Assisted tests for one operation at a time.
- Inject timeout, missing target, rejected action, stale state and IPC loss.
- Verify emergency stop during every adapter category.

### Completion criteria

- No adapter executes without a valid, current, approved intent.
- Every operation has an observed success/failure result.
- Retry budgets terminate safely.
- Assisted mode passes the defined failure-injection suite.

## Phase 6 — Full clear integration

### Implementation target

- Integrate exploration, combat, resources, floor transition and recovery.
- Build the long-running clear loop, telemetry/replay analysis and recovery policies.
- Enable Autonomous mode only as an explicit experimental opt-in.

### Dependencies

- All previous phase gates.
- Dungeon-specific support matrix and regression replay corpus.
- Explicit distribution/policy decision.

### Verified basis

- Existing BossMod source provides many building blocks but no evidence located in this investigation demonstrates unattended, recovery-tested completion of all requested DDs as an independent plugin.
- Compilation proves source compatibility only. It does not prove route quality, mechanic completeness, recovery correctness or long-duration reliability.
- Full automation crosses the official Dalamud repository restrictions identified in Phase 5.

### Implementation approach

1. Build a hierarchical controller: session lifecycle, floor lifecycle, room objective, combat/resource task and atomic execution request.
2. Persist only minimal recovery checkpoints that can be validated against a new live snapshot; discard stale actor/object references after reload or zone change.
3. Define explicit recovery policies for death, wipe, disconnect, plugin reload, lost IPC, invalid floor state, repeated execution failure and passage transition timeout.
4. Run long Simulation campaigns from recorded fixtures before Assisted in-game trials.
5. Promote capabilities individually using feature flags and a compatibility matrix by dungeon, floor band, job and party size.
6. Keep Autonomous mode build-time and runtime gated until user approval and evidence review.
7. Generate a post-run report containing decisions, stalls, retries, resource usage, unsupported states and recovery outcomes.

### Technical risks

- Long-tail failures across hours and many floors.
- Death/wipe, disconnect, plugin reload and floor-transition recovery.
- Compounding imperfect decisions and consumable scarcity.
- Account and distribution risk from full automation.

### Test method

- Soak tests in Simulation, then Advisor, then Assisted.
- Fault injection for disconnects, death, wipe, reload, missing IPC and stale memory.
- Long-run metrics: stalls, unsafe intents, retries, resource use and recovery success.
- Human review of every new autonomous capability before activation.

### Completion criteria

- A documented dungeon/floor support target completes the full regression suite.
- No unresolved critical safety defect or unbounded retry remains.
- Recovery paths are tested, not inferred.
- User explicitly approves Autonomous activation after reviewing evidence.

## Cross-phase quality gates

- Facts, inferences and proposals are labeled.
- Every upstream-derived behavior has source provenance.
- CI builds deterministically and audits dependencies.
- Replays/logs exclude secrets and unnecessary player identifiers.
- No phase starts implementation before the prior phase completion evidence is reviewed.
- The user approves movement to the next implementation phase.

## Cross-phase risk register

| Risk | Evidence/status | Impact | Required mitigation and gate |
|---|---|---|---|
| BossMod core coupling | Verified in `AutoClear`, `AIHints`, `ActionQueue`, `ZoneModule` and service access | Direct extraction expands into a hidden BossMod fork | Dependency inventory and DDCore contract boundary accepted before porting |
| Native layout/API drift | Verified native reads in `WorldStateGameSync` and hooks in `ActionManagerEx`; API 16 is not finalized | Crashes, corrupt state or invalid actions after patches | Version gate, range validation, fail-closed health state, patch requalification |
| Incomplete mechanic knowledge | Compilation is verified; full replay/in-game coverage is not | Unsafe advice or execution | Support matrix, confidence labels, unsupported-state pause |
| Hidden trap uncertainty | Palantir IPC is optional | Route may assume unavailable information | Source/confidence metadata; never infer hidden traps without evidence |
| Replay schema lock-in | BossMod has DD replay opcodes but they are internal to its format | DDCore tests become coupled to upstream serialization | Versioned DDCore schema plus import/conversion tools |
| Decision loops | Long-horizon behavior is not proven | Stalls and resource exhaustion | Deterministic scenarios, state fingerprints, progress deadlines, retry budgets |
| Execution without acknowledgement | Native or IPC requests can fail or become stale | Repeated/harmful operations | Preconditions, expiry, result evidence, idempotency, global emergency stop |
| Official distribution rejection | Explicit official automation restrictions | Rejected submission or ecosystem risk | Separate Observer/Advisor compliance review; private-only assumption for Autonomous |
| License/provenance loss | BossMod is BSD-3-Clause | Redistribution noncompliance | Source-level provenance ledger and third-party notices |
| Sensitive telemetry | Long-run logs can contain identifiers | Privacy exposure | Data minimization, redaction tests, local retention controls |
| Upstream dependency advisories | Local build reported NU1902/NU1903 for transitive ImageSharp 3.1.12 | Supply-chain exposure; DDCore impact not yet established | Avoid unnecessary dependency inheritance and run package audit in CI |

## Validation strategy by layer

| Layer | Offline verification | In-game verification | Promotion requirement |
|---|---|---|---|
| World | Snapshot parser unit tests, invalid-range/fuzz cases, replay import | Compare snapshot with visible floor/party/resource state | No stale or out-of-range state escapes the adapter |
| Mechanics | Golden geometry/timing tests, replay event tests | Observer overlays compared with recorded encounters | Unsupported mechanics are explicit; no false safe classification in corpus |
| Navigation | Graph fixtures, property tests and blocked-edge cases | Advisor route comparison only | Routes obey walls, hazards and exploration constraints |
| Resources | Inventory transition and conservation scenarios | Advisor recommendations checked against observed resources | Counts remain consistent and illegal use is rejected |
| Decision | Deterministic scenario/counterfactual tests | Advisor reason output reviewed | Every intent is explainable and ambiguous states pause |
| Execution | Fake adapters, timeout/retry/fault injection | Single Assisted capability per trial | Current snapshot, acknowledgement and emergency stop verified |
| Integration | Replay campaigns and soak simulation | Incremental Assisted sessions | No critical defect, unbounded retry or unsupported autonomous transition |

## Deliverables and approval gates

| Phase | Required deliverables before approval |
|---|---|
| 1 | Source inventory, dependency/provenance graph, reproducible upstream build, risk baseline |
| 2 | Independent solution, Observer-only load, Japanese UI/config, tests, CI and notices |
| 3 | Dungeon support matrix, replay fixtures, mechanic test results and known-unknown register |
| 4 | Decision/state-machine specification, scenario corpus, explainability and stall-test results |
| 5 | Adapter contracts, audit schema, failure-injection evidence and distribution decision |
| 6 | Long-run metrics, recovery evidence, compatibility matrix and explicit Autonomous approval |

## Research conclusions

### Verified facts

- The pinned BossMod source builds and contains substantial DD state capture, room routing, resource handling, combat policy, replay and boss-mechanic code.
- The DD code is coupled to BossMod core infrastructure and cannot be called an independent library in its current form.
- Official Dalamud API 15 is the stable target at the research date; API 16 remains explicitly unfinished.
- Official Dalamud publication rules are incompatible with the requested fully automated gameplay design.

### Assessments

- An independent Observer/Advisor DDCore is feasible with controlled migration and replay-led testing.
- Autonomous operation is technically plausible but should be treated as a new, high-risk system rather than a packaging exercise.
- The recommended implementation sequence is: Phase 2 safety shell; world snapshots/replay; navigation; one EO vertical slice; remaining EO; HoH; PotD; optional PT; decision simulation; Assisted adapters; Autonomous integration last.
- EO is recommended as the first vertical slice because the located module set is smaller than PotD. This is a planning judgment, not proof that EO is mechanically easier.

## Unresolved decisions before implementation

1. Include or exclude Pilgrim's Traverse from the first supported release.
2. Require absolute independence from BossMod at runtime, or permit an optional read-only compatibility/import provider.
3. Select the first solo job, dungeon and floor band for the end-to-end acceptance target.
4. Decide which optional navigation/action IPC providers may be supported and how absence/version mismatch is surfaced.
5. Confirm whether Autonomous artifacts are strictly private/local and whether Observer/Advisor will ever target official publication.
6. Define the required replay data source and redaction policy before collecting fixtures.

## Decision pending

The current BossMod source includes Pilgrim's Traverse (`DeepDungeonState.DungeonType.PT` and 13 module/resource files), but the requested Phase 3 named only PotD, HoH and EO. DDCore should not silently expand scope; inclusion of PT needs approval.
