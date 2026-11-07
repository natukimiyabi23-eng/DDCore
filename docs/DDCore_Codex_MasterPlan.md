# DDCore — Codex Master Plan

## Purpose and scope

Create a standalone, extensible Dalamud plugin for Final Fantasy XIV Deep Dungeons, based on proven BossMod concepts while separating BossMod-derived code from DDCore-original code.

This plan covers research and future implementation design. The current task stops before plugin implementation.

## Fixed technical policy

- Language: C#
- Platform baseline: Dalamud API 15, .NET 10
- Upstream baseline: BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- Default runtime mode: Observer; all game operation disabled
- Architecture: observation, advice/simulation, decision, and execution are separate
- Provenance: BossMod-derived files and DDCore-original files remain identifiable
- Safety: emergency stop and fail-closed behavior are mandatory before any operation mode

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

## Phase 4 — Strategy decision engine

### Implementation target

- Recognize floor, room, exploration, enemies, passage/return, chests and resources.
- Produce routes, target priorities, combat/resource recommendations and state transitions.
- Provide reason codes and rejected-alternative diagnostics.

### Dependencies

- World snapshots, navigation geometry, mechanic hazards and resource inventory.
- Deterministic clock/random abstractions for simulation.

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

## Phase 5 — Operation foundation

### Implementation target

- Implement movement, targeting, action, item, object-interaction and transition adapters.
- Add result verification, timeouts, retry budgets and emergency stop.
- Begin with Assisted mode; Autonomous remains locked.

### Dependencies

- Approved intents from the decision engine.
- Stable Dalamud/FFXIVClientStructs adapters and optional IPC contracts.
- Operation audit log.

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

## Decision pending

The current BossMod source includes Pilgrim's Traverse (`DeepDungeonState.DungeonType.PT` and 13 module/resource files), but the requested Phase 3 named only PotD, HoH and EO. DDCore should not silently expand scope; inclusion of PT needs approval.
