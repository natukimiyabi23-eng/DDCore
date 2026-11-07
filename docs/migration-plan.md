# Migration plan

## Principle

Migrate behavior by dependency slice, not by copying directories. Every slice first becomes observable and replay-testable, then advisory, and only later executable.

## Recommended order

1. **Plugin shell and provenance**: API 15 project, Japanese UI, config, logging, mode gate, attribution files.
2. **World snapshot**: DD identity, floor, tileset, rooms, party, chests, pomanders, magicite/demiclones, passage/return progress.
3. **Replay format and simulator**: record normalized observations; run decision code without FFXIV.
4. **Pure room navigation**: room graph, geometry, walls and route recommendations without movement.
5. **Mechanics and bosses**: AoE shapes and boss modules, one dungeon at a time.
6. **Decision engine**: explicit state machine, target/resource/risk policy, explanation output.
7. **Assisted adapters**: one user-triggered interaction at a time with guards.
8. **Autonomous integration**: only after prior modes meet exit criteria and distribution/policy decision is explicit.

## Source-to-target mapping

| BossMod source | DDCore target | Migration action |
|---|---|---|
| `Data/DeepDungeonState.cs` | `DDCore.World` | Re-model state; retain replayable operations |
| `Framework/WorldStateGameSync.cs` DD section | `DDCore.World.Dalamud` | Rewrite as isolated observation adapter |
| `Global/DeepDungeon/FloorPathfind.cs` | `DDCore.Navigation` | Port BFS with unit tests |
| `Rooms.cs`, `Walls.json` | `DDCore.Navigation` / resources | Separate extraction from runtime lookup |
| `Traps.cs`, `BadTraps.json` | `DDCore.Mechanics` | Make Palantir optional and provenance explicit |
| `AutoClear.cs` | Multiple projects | Decompose; do not port as one class |
| `*FloorModule.cs` | `DDCore.Dungeons` | Port DD-specific rules after shared contracts stabilize |
| Boss files | `DDCore.Dungeons.*.Bosses` | Port incrementally with replay fixtures |
| `DeepDungeonAI.cs` | `Combat`, `Resources`, `Decision` | Split potion/transformation/kiting policies |
| `AIHints` / pathfinding / action queue | Contracts plus adapters | Recreate minimum required semantics |

## Dungeon sequence

Recommended: Eureka Orthos first, then Heaven-on-High, Palace of the Dead, then Pilgrim's Traverse.

This is a judgment, not a verified requirement. EO has a bounded 100-floor structure and current-style mechanics, while PotD has the broadest historical range and more legacy variation. PT exists in the current BossMod source but was not in the original requested Phase 3 list, so its inclusion requires user approval.

## Compatibility strategy

- Pin an upstream BossMod commit for every migration batch.
- Record original file, upstream commit, local destination, and transformation type.
- Compare upstream DD paths periodically, but never auto-merge into execution code.
- Keep raw/native offsets out of domain projects.
- Fail closed when current game/API versions are unrecognized.

## Verification gates

### Gate A: Observer

- Recorded state agrees with visible in-game DD state for representative floors.
- No input/action APIs are reachable.
- Entry/exit and floor transitions do not retain stale state.

### Gate B: Advisor

- Route, target and hazard advice is explainable and replay-reproducible.
- False-safe advice is tracked as a release blocker.
- Boss AoE outputs match annotated replays.

### Gate C: Assisted

- Every action requires a user trigger.
- Target/action/item/object adapters have timeouts, rate limits and result verification.
- Emergency stop is tested during each operation type.

### Gate D: Autonomous

- Long-run tests cover disconnects, death, wipes, floor transition, missing IPC, stale native state, inventory/resource exhaustion and plugin reload.
- No operation occurs after any safety gate fails.
- Rollback to Advisor mode is immediate and persistent.

## Rollback

Each phase remains selectable independently. A failing dungeon module can be disabled without disabling Observer mode. Execution adapters are feature-flagged individually, and the master mode always overrides child flags.
