# Phase 5.6 - Staged environment implementation plan

## 1. Recommendation

The next implementation should be a small read-only diagnostics slice that records only public generic environment objects. It should not attempt room inference, portal activation, collision, or path generation.

The required order is:

1. prove what public objects are actually visible;
2. validate DD-specific semantics with live evidence;
3. evaluate static geometry separately;
4. decide whether a trustworthy public topology source exists;
5. connect BFS only after a complete topology gate passes.

Producing a non-Stop strategy result is not a success criterion. Correct Unknown/Stop is required whenever evidence is incomplete or conflicting.

## 2. Stage 5.6A - public object evidence recorder

### Purpose

Observe generic interactable/environment objects using public Dalamud API 15 without assigning DD semantics.

### Scope

- extend the source-neutral observation contract for generic object kind, BaseId, anonymous ephemeral identity, Usable/Quarantined/Missing position, rotation, targetability, timestamp and provenance;
- capture a bounded allowlist of public generic kinds relevant to investigation (`EventObj`, `Treasure`, `Aetheryte`, and only other kinds justified during implementation review);
- initial OFF, explicit opt-in, local-only bounded recording;
- show counts and quality in Japanese diagnostics;
- keep all environment fields disconnected from strategy and execution.

### Public APIs

- `IObjectTable` and `IGameObject`;
- `IFramework.Update` for bounded sampling;
- `IClientState`/`IDutyState`/`ICondition` for reset and DD-context evidence.

### Live verification

- collect opt-in evidence in each supported DD near a coffer and passage device;
- correlate visible game objects with object kind/BaseId/position/targetability without assuming meaning in advance;
- enter, transition floor, exit, and re-enter to validate resets;
- verify no names/chat/account data are present in export.

### Safe failure

Capture exceptions produce a fixed reason code and invalidate the affected collection. Missing or quarantined positions remain unusable. No strategy value changes.

### Tests

- public-object conversion and unsupported kinds;
- object disappearance and ID reuse;
- nullable/origin/non-finite/lifecycle spatial cases;
- session/floor-epoch reset hints;
- bounds, stop/dispose, export failure and privacy;
- schema backward read compatibility;
- prohibition scan and `DisabledExecutionGateway` regression.

### Completion gate

Release build, all tests, format/audit/CI pass; live package remains explicitly unverified until the user supplies evidence. The output contains generic evidence only.

## 3. Stage 5.6B - versioned semantic candidates

### Purpose

Convert independently verified generic observations into Candidate passage/coffer/revealed-trap semantics.

### Scope

- versioned mapping manifest with DD kind/range, game/API version, public ObjectKind, BaseId and evidence reference;
- distinguish object identity/position from state;
- conflict, unknown version, unsupported DD, and missing evidence remain Unknown;
- no passage activation inference from targetability unless separately demonstrated.

### Required evidence

At least two independent captures for each object type and DD/floor band, including spawn/despawn or interaction performed manually by the user. BossMod OIDs may guide comparison but do not replace DDCore live evidence.

### Safe failure

Unknown or conflicting mapping produces semantic Unknown; generic record remains available for diagnostics. Nothing reaches strategy.

### Tests and completion gate

Golden fixtures for known/unknown/conflicting/version-mismatched mappings; deterministic classification; zero PII; unsupported DDs remain Unknown. All validation and CI gates pass.

## 4. Stage 5.6C - static room-geometry feasibility prototype

### Purpose

Determine what static `planmap.lgb`/Excel data can prove without native current-layout state.

### Scope

- offline-only parser/prototype using public `IDataManager` contracts or copied non-personal test fixtures;
- enumerate candidate EventRange room boxes and coordinate transforms;
- compare candidates with anonymous player/object coordinates;
- do not create a live room graph or current-room claim.

### Risks

- static assets may describe multiple layout variants but not identify the active randomized layout;
- priorities/transforms are game-data conventions, not a stable Dalamud topology API;
- patch changes may invalidate paths and assumptions;
- BossMod combines these assets with native `RoomFlags`, so static geometry alone is incomplete.

### Safe failure and completion gate

If a layout cannot be selected from public evidence, report Unavailable and stop. Completion means an evidence report and deterministic fixture tests, not room recognition.

## 5. Stage 5.6D - topology-source decision gate

### Purpose

Decide whether a complete current-floor graph can be built within approved public-only constraints.

### Required inputs

- current floor epoch;
- 25 room entries or an independently proven equivalent;
- reciprocal cardinal connection flags;
- current room and destination room;
- source/version/freshness evidence.

### Decision

- If a public source is proven, implement a read-only normalizer plus captured-fixture validation.
- If no public source is proven, retain Unknown and report the limitation. Do not approximate edges from actor clusters, minimap pixels, or movement traces.
- Any internal/native-structure proposal is a separate approval boundary and is not authorized by this plan.

### Completion gate

A source is accepted only after multiple-floor live comparison, transition reset, valid room zero, missing data, and patch/version mismatch tests. Otherwise this stage closes as blocked-by-public-data with safe Stop behavior intact.

## 6. Stage 5.6E - validated BFS adapter

### Purpose

Connect an independently verified environment graph to the existing pure `RoomPathfinder`, still without game movement.

### Scope

- enforce the topology gate in `phase5.6-environment-model.md`;
- convert only Confirmed, fresh, complete graph/current/destination data;
- return explicit Unknown/Invalid/NoRoute distinctions;
- invalidate cached results on any epoch or topology change;
- expose the proposed route in diagnostics/simulation only.

### Tests

- complete 25-node graphs, disconnected routes, reciprocal/boundary failures;
- stale/conflicting/partial graphs rejected before BFS;
- deterministic tie/order behavior;
- route invalidation after every reset source;
- strategy remains Stop when safety overlays such as trap/obstacle state are Unknown.

### Completion gate

The route is computed only in tests or read-only diagnostics. `DisabledExecutionGateway` remains unchanged, and no movement/UI/chat/game operation is introduced.

## 7. Live verification checklist

For future approved diagnostics, the user performs all game-side actions.

1. Record plugin version, API version, DD kind, territory/content, floor range and session/floor epoch.
2. Start opt-in recording before approaching a visible passage/coffer.
3. Record public ObjectKind, BaseId, spatial quality, targetability and timestamps only.
4. Capture object appearance/disappearance and manual interaction outcome without chat text or names.
5. Transition to the next floor and confirm old objects are invalidated.
6. Exit and re-enter; confirm a new session/epoch and no carry-over.
7. Export bounded anonymous diagnostics and review contents before sharing.
8. Repeat on Palace of the Dead, Heaven-on-High, and Eureka Orthos; untested DDs remain unsupported.

No existing schema-5 replay can substitute for this test because those exports contain Enemy actors only.

## 8. CI and audit plan

Every implementation stage must run:

```powershell
dotnet restore DDCore.slnx --locked-mode
dotnet build DDCore.slnx -c Release --no-restore
dotnet test DDCore.slnx -c Release --no-build --no-restore
dotnet format DDCore.slnx --verify-no-changes --no-restore
dotnet list DDCore.slnx package --vulnerable --include-transitive
```

Also audit project/package references for BossMod runtime dependencies and source for game input, movement, targeting, action/item execution, signature scanning, and new native/internal-structure access.

## 9. Approval boundaries and unresolved risks

User approval is required before each implementation stage. Additional explicit approval is required before any native/internal structure access, even as a read-only prototype.

Unresolved risks:

- no public room graph/current-room source is confirmed;
- passage activation semantics are unverified;
- no public collision/navmesh enumeration is confirmed;
- hidden traps remain unavailable;
- static layout variants may not be selectable without native current-layout state;
- same-territory floor transitions can be missed outside the separately verified chat diagnostic path;
- all non-EO DDs and most floor bands remain unverified for environment-object semantics.

## 10. Phase 5.6 completion statement

Phase 5.6 research/design is complete when the three Phase 5.6 documents are reviewed, CI for the documentation-only branch passes, and the Draft PR remains unmerged. No next implementation stage starts automatically.
