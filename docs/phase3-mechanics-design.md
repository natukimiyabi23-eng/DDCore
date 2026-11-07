# Phase 3 mechanic analysis design

## Status and scope

This document is the implementation-ready design for Phase 3. It does not authorize or implement character movement, targeting, combat actions, item use, object interaction, input injection, hooks that change game state, or any other game operation.

Phase 3 produces deterministic observations only:

1. normalize observed Deep Dungeon state into immutable snapshots;
2. recognize casts, statuses and encounter transitions;
3. derive time-bounded hazard regions and mechanic warnings;
4. replay the same observations without Dalamud or FFXIV;
5. project results to Observer, Advisor and Simulation consumers.

Assisted and Autonomous remain defined modes in the Phase 2 safety model, but Phase 3 must not add an execution path for either mode.

## Evidence baseline

### Verified facts

- BossMod analysis is pinned to commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. The local checkout and public `origin/master` were both at this commit when this design was prepared.
- The pinned tree contains 63 paths under `BossMod.Modules` whose paths include `DeepDungeon`: 59 C# files and 4 JSON resource files.
- `BossMod/Data/DeepDungeonState.cs` models dungeon/floor progress, a 25-room map, four party members, 16 pomanders, 16 chests and three magicite entries. It also defines six replay operations: `DDPG`, `DDMP`, `DDPT`, `DDIT`, `DDCT` and `DDMG`.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs and updates the BossMod state. This is a native, patch-sensitive acquisition boundary.
- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` consumes BossMod actors, events, `AIHints`, `ActionQueue`, pathfinding and services. It is not an independent mechanic library.
- The DD modules use BossMod component and shape infrastructure extensively. In the pinned DD C# inventory, `StandardAOEs` appears 113 times. The most frequent shapes are circle (58), cone (55), rectangle (36), donut (18), cross (4) and donut-sector (2).
- BossMod represents forbidden zones using signed-distance functions plus activation time in `AIHints`; its navigation layer rasterizes those zones. Phase 3 must stop before navigation.
- `D10Gancanagh.cs` is a small, bounded encounter definition: a square arena and three circle-AoE analyzers. This makes it a suitable first validation slice, but choosing it is a DDCore design assessment rather than an upstream requirement.
- BossMod is BSD-3-Clause. Its copyright notice, conditions and disclaimer must be retained in source redistribution, and the notice/conditions/disclaimer must be reproduced with binary distributions.
- Official Dalamud restrictions discourage automatic server interaction and combat interference, allow only narrowly informational combat presentation, and list visible markers for non-telegraphed AoEs as a non-starter. Official-repository acceptance is subjective and requires review.
- The official D17 repository requires a publicly cloneable source repository, clean Release installation/build artifacts, an icon, appropriate Windowing API usage, and disclosure of AI use during submission.

### Design assessments

- Native reads and Dalamud services belong in an adapter assembly; they must never leak their types into the pure analysis model.
- “No hazard observed” is not equivalent to “safe.” Missing, stale or unsupported observations must produce an explicit unknown result.
- A serialized shape discriminated union is safer for replay compatibility than storing arbitrary signed-distance delegates.
- Phase 3 should initially remain informational and local-only. Official repository submission should not be assumed possible until the Plugin Approval Committee reviews the concrete feature set.

## Dependency boundary

```text
Dalamud / Lumina / FFXIVClientStructs
             |
             v
DDCore.Plugin observation adapter
             |
             v
DDCore.Core.World immutable snapshots
             |
             v
DDCore.Core.Mechanics deterministic analyzers
             |
             +----> replay writer / test fixtures
             |
             +----> Observer / Advisor / Simulation projections

No edge from Core back to Dalamud, native structs, UI, navigation or execution.
```

The physical project split may remain `DDCore.Core` plus namespaces during the first vertical slice. Separate assemblies should be introduced only when dependency enforcement or build isolation requires them. Regardless of physical layout, architecture tests must reject references from Core to Dalamud, FFXIVClientStructs, Lumina, BossMod or the plugin assembly.

## WorldSnapshot model

`WorldSnapshot` is an immutable, versioned value graph. It represents what was observed, not what DDCore believes should happen.

### Header and observation quality

| Field | Purpose |
|---|---|
| `SchemaVersion` | Replay/model compatibility; unknown major versions fail closed |
| `Sequence` | Strictly increasing within one observation session |
| `CapturedAtUtc` | Timestamp supplied by an injected clock |
| `SessionId` | Random local replay correlation ID; not a character/account identifier |
| `Source` | Live adapter, replay, mock or simulation |
| `Quality` | Complete, partial, stale, unsupported or invalid |
| `Diagnostics` | Stable reason codes for omitted/rejected observations |

Analysis uses `CapturedAtUtc` and never reads the process clock directly. Duplicate or decreasing sequence numbers, future timestamps outside tolerance, invalid numeric values and unknown schema major versions are rejected or downgraded to unknown.

### Context

- territory/content identifiers;
- dungeon kind: None, Palace of the Dead, Heaven-on-High, Eureka Orthos, or an explicitly approved future value;
- floor and tileset, represented as optional validated values;
- local arena/room coordinate frame and bounds when known;
- progress for passage, return, hoard, equipment sync and floor resources;
- room/chest/pomander/magicite observations with stable, DDCore-owned enums.

Pilgrim's Traverse is represented only as an unsupported/unknown dungeon value until scope is explicitly approved. BossMod enum values must not be assumed stable wire values.

### Actors and party

Each `ActorSnapshot` contains only normalized values needed for analysis:

- session-local actor identity and object ID;
- actor kind, position, rotation and hitbox radius;
- HP state, target identity, dead/destroyed/combat flags;
- optional cast snapshot with action ID, target, location, start and finish time;
- immutable status snapshots with source, stack and expiry;
- observation quality for fields that may be absent.

Party slots are immutable records linked by session-local actor identity. Content IDs, account IDs, character names and other unnecessary personal identifiers are excluded.

### Event delta

The adapter may accompany a snapshot with normalized deltas: actor added/removed, cast started/finished/interrupted, event cast, status gained/lost, death and environment-state changes. An analyzer must be able to rebuild equivalent state from a complete snapshot plus ordered deltas. A periodic complete snapshot bounds damage from a missing delta.

## Geometry and time model

### Serializable shape union

Phase 3 supports these DDCore-owned primitives:

- circle;
- cone;
- rectangle/capsule-aligned rectangle;
- donut;
- donut sector;
- cross;
- polygon or explicitly versioned custom geometry when upstream behavior cannot be expressed otherwise.

Each shape exposes a pure signed-distance calculation and validates finite dimensions, angle ranges and orientation. The serialized representation stores numeric parameters and a discriminator, not executable delegates.

### HazardInstance

Each derived hazard includes:

- stable `HazardId` within the session;
- source analyzer/version and provenance ID;
- source actor/action/status evidence;
- shape, origin and rotation;
- `ObservedAt`, optional `ActivatesAt`, and optional `ExpiresAt`;
- severity and confidence;
- target/party applicability when known;
- support state: supported, approximate, unknown or invalid;
- stable reason codes suitable for Japanese UI localization.

Expired hazards are removed deterministically. Unknown activation time is displayed as unknown, not “active now” or “safe.” Simultaneous hazards retain independent IDs so one cancellation does not remove another.

### MechanicFrame

`MechanicFrame` is the only Phase 3 analyzer output. It contains hazards, enemy-action observations, warnings, unsupported observations and the input sequence/time it was derived from. Safe-region output is optional and may only be emitted when arena bounds are known and all hazards needed for that claim are supported. The default result is “safe region not established.”

## Analyzer contracts

```text
IMechanicAnalyzer
  AnalyzerId / AnalyzerVersion / SupportedContext
  Analyze(WorldSnapshot snapshot, MechanicState previous)
    -> AnalysisResult(MechanicState next, MechanicFrame frame)
```

Requirements:

- pure and deterministic for equal snapshot, prior state and configuration;
- no service locator, process clock, network, file, UI or game-operation access;
- bounded work per actor/event and cancellation support for replay batches;
- explicit unsupported results for unknown actions or invalid context;
- analyzer state is immutable or replaced atomically;
- exceptions are contained per analyzer, logged outside Core and converted to an invalid/unknown frame.

The registry selects analyzers by dungeon, floor/encounter and stable data identifiers. Encounter analyzers must not mutate shared floor state. Common components (cast AoE, persistent voidzone, gaze, knockback, spread, bait, line-of-sight) are composable analysis helpers, not BossMod-derived inheritance chains.

## First vertical slice

The recommended first slice is Eureka Orthos floor 10, Gancanagh.

### Verified source behavior

- square arena centered at `(-300, -300)` with half-size `19.5`;
- action `31478`: circle radius 10 with 5-second cast;
- action `32700`: circle radius 10 with 8-second cast, source component limits displayed instances to six;
- action `31479`: circle radius 18.5 with 5-second cast;
- the upstream file activates three standard AoE components and contains no movement or execution logic.

### Acceptance fixture

A synthetic fixture must cover cast start, overlapping mines, ordering, cast interruption, actor removal, activation/expiry, unknown action and stale snapshot. Expected output is compared by shape parameters, origin, activation window, evidence and support state. This validates the platform only; it is not evidence that all live encounter behavior is correct.

## Replay and mock test design

### Replay envelope

- versioned header with schema, DDCore version, source kind, analyzed BossMod provenance commit when applicable, and start time;
- normalized complete snapshots plus ordered deltas;
- optional expected `MechanicFrame` records for golden tests;
- no player names, account IDs, content IDs, chat or unrelated plugin state;
- deterministic JSON during schema development; a compact format can be considered only after compatibility tests exist.

Replay input is untrusted. Readers enforce length/count limits, finite numeric values, monotonic sequence constraints and a maximum supported schema version.

### Test layers

1. Geometry property tests: inside/outside/boundary, rotation, translation and invalid parameters.
2. Model tests: immutability, validation, unknown/stale propagation and serialization round trips.
3. Lifecycle tests: cast interruption, duplicate events, actor reuse/removal, status replacement and time expiry.
4. Golden encounter tests: snapshot/event sequence to exact `MechanicFrame` output.
5. Differential tests: for curated fixtures, compare DDCore results with pinned BossMod results and document intentional differences.
6. Malformed-input tests: truncation, unknown discriminators, excessive arrays, NaN/infinity and out-of-order events.
7. Architecture tests: Core has no forbidden package/assembly references and analyzers expose no execution API.
8. Performance tests: bounded allocations and frame time against a documented actor/hazard ceiling; thresholds are set only after baseline measurement.

Live Dalamud validation is a separate user-run gate. Passing replay tests must never be reported as successful live-game verification.

## Migration and provenance

No BossMod implementation is copied during this design task. Before any derived code or data is added, record for each item:

- upstream repository and pinned commit;
- exact source path and permanent URL;
- upstream copyright/license;
- destination path;
- whether copied, modified, reimplemented from behavior, or independently authored;
- transformation notes and source/destination hashes where applicable.

Keep BossMod-derived files mechanically separable from original DDCore code. Update `THIRD-PARTY-NOTICES.md`, retain applicable BSD-3-Clause notices in source, and include the license notice/conditions/disclaimer with binary distributions. Contributor metadata in module attributes is not a substitute for the license notice and should be preserved as provenance where appropriate.

## Official distribution constraints

The following are verified against current official Dalamud publishing pages on 2026-11-07:

- plugins should not automatically interact with game servers or interfere with combat except for narrowly informational presentation of information otherwise available;
- visible markers for non-telegraphed AoEs are listed as not allowed;
- combat submissions should be discussed with the approval team before submission, and review is subjective;
- regular windows should use the Dalamud Windowing API;
- official D17 submission requires a publicly cloneable repository, Release build artifacts, installer images/icon requirements, and AI-use disclosure.

Consequences for Phase 3:

- no in-world overlays or claims about hidden/non-telegraphed hazards are enabled by default;
- Observer/Advisor output must identify evidence and confidence and remain informational;
- replay/simulation tooling is local-only and contains no personal identifiers;
- official repository eligibility is unresolved until the concrete UI and data sources receive approval-team feedback;
- custom distribution is not official approval and does not remove Square Enix terms/account risk.

## Implementation order and gates

| Step | Deliverable | Gate to continue |
|---|---|---|
| P3.0 | provenance manifest, schema ADR and supported-scope matrix | license and privacy review pass |
| P3.1 | pure vectors, angles, shape union and signed-distance tests | geometry/property tests pass |
| P3.2 | immutable `WorldSnapshot`, validation and mock builder | serialization/unknown-state tests pass |
| P3.3 | normalized deltas, replay codec and hostile-input limits | replay round-trip/malformed tests pass |
| P3.4 | analyzer registry, state lifecycle and `MechanicFrame` | architecture/determinism tests pass |
| P3.5 | Gancanagh vertical slice | all synthetic/golden cases pass; live remains unverified |
| P3.6 | remaining Eureka Orthos bosses and selected floor enemies | per-module support matrix and fixtures pass |
| P3.7 | Heaven-on-High, then Palace of the Dead | same per-module gate; unsupported remains explicit |
| P3.8 | Pilgrim's Traverse | separate user scope approval required first |

Every step is a review boundary. A failing or incomplete analyzer remains disabled and marked unsupported rather than falling back to guessed geometry.

## Risk register

| Risk | Impact | Mitigation / verification |
|---|---|---|
| Native layout or API drift | wrong/incomplete snapshots | isolate adapter, version checks, invalid quality, live user gate |
| Missing/out-of-order events | stale hazards | sequence checks, periodic full snapshots, lifecycle tests |
| Actor ID reuse | hazard attached to wrong entity | session-local generation identity and removal tests |
| Coordinate/rotation mismatch | incorrect regions | canonical radians/axes, transform property tests, replay fixtures |
| Upstream behavior uncertainty | false advice | provenance, support state, differential tests, no guessed safe region |
| Custom/LOS/knockback mechanics | insufficient shape model | explicit unsupported output, add primitives only with fixtures |
| Replay privacy/size | sensitive or unbounded files | allowlist fields, no personal IDs, count/size limits |
| BossMod license omission | redistribution violation | provenance gate, notices, package audit |
| Official repository rejection | distribution blocked | pre-consult approval team; keep local analysis distinct from automation |
| Performance regression | game frame impact | immutable batches, profiling baseline, bounded analyzer work |

## Unresolved decisions before implementation

1. User approval of Gancanagh as the first vertical slice.
2. Whether Phase 3 live acquisition uses only supported Dalamud services or also an isolated, version-checked native adapter.
3. Replay storage location, retention and an explicit user control for capture/export.
4. Numeric tolerances and maximum accepted observation age, based on measured live data rather than guesses.
5. Exact Advisor presentation that the Dalamud approval team considers acceptable.
6. Scope and evidence requirements for enemy floor actions beyond boss encounters.
7. Whether safe-region calculation belongs at the end of Phase 3 or is deferred to Phase 4; default is deferred unless a bounded informational use case is approved.
8. Whether and when Pilgrim's Traverse enters scope.

Phase 3 implementation must not begin until these decisions are reviewed. None of them should be silently resolved by implementation convenience.

## Completion conditions

Phase 3 is complete only when:

- the three requested Deep Dungeons have an audited support matrix;
- every “supported” analyzer has deterministic unit and replay fixtures;
- derived BossMod material has complete BSD-3-Clause provenance and package notices;
- unknown, stale and unsupported inputs visibly fail closed;
- Core remains free of Dalamud/native/execution dependencies;
- Release build, tests, format and dependency audit pass;
- live behavior is reported separately and only after user-run Dalamud verification;
- no Phase 3 code performs or requests game operation.

## Primary sources

- BossMod pinned tree: <https://github.com/awgil/ffxiv_bossmod/tree/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0>
- BossMod license: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/LICENSE>
- Deep Dungeon state: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs>
- Live DD synchronization: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs>
- Shared AutoClear: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/AutoClear.cs>
- Gancanagh source: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Endwalker/DeepDungeon/EurekaOrthos/D10Gancanagh.cs>
- Dalamud plugin restrictions: <https://dalamud.dev/plugin-publishing/restrictions/>
- Dalamud technical considerations: <https://dalamud.dev/plugin-development/technical-considerations/>
- Official D17 repository criteria: <https://github.com/goatcorp/DalamudPluginsD17/blob/main/README.md>
