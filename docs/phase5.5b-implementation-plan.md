# Phase 5.5B implementation plan

## 1. Scope and conclusion

Phase 5.5B should first make public duty identity and actor spatial quality trustworthy. It should **not** add native Deep Dungeon reads in the same change. The latest live replay proves that public `TerritoryType` and `ContentFinderCondition` identifiers are available and that the anonymous player pose works, but the current adapter does not interpret the duty row and the actor schema cannot distinguish a semantically usable position from a suspicious zero value.

Recommended slices, each separately reviewable:

1. **5.5B-Public**: data-driven DD kind and ten-floor band classification, separate duty/deep-dungeon evidence, and unknown-ID diagnostics.
2. **5.5B-Spatial**: actor `BaseId` naming correction plus nullable/quality-qualified positions that quarantine uncertain coordinates.
3. **5.5B-Native research prototype**: exact floor only, behind a separately approved, optional, version-gated, read-only interface. Do not combine room/resources with the first native read.
4. **5.5B-Topology**: only after exact-floor evidence is validated, add the complete 25-room map/current-room/passage snapshot and floor-epoch invalidation.

No slice may make `DeepDungeonStrategyEngine` actionable until all inputs required by the chosen decision are authoritative. `DisabledExecutionGateway` remains unchanged.

## 2. Sources and versions

### DDCore source at Draft PR #7

Base commit: `bc75ea1b88f7642130ce020b497632d95129e2c5` (`phase5/strategy-engine`).

| Evidence | Current behavior |
| --- | --- |
| `src/DDCore.Plugin/Observation/DalamudObservationSource.cs:100-102` | Stores `IDutyState.ContentFinderCondition.RowId` as `contentId`, only while `IsDutyStarted` is true. |
| `DalamudObservationSource.cs:112` | Stores `IClientState.TerritoryType` as `territoryId`. |
| `DalamudObservationSource.cs:139-154` | Reads a character position and stores `gameObject.BaseId` in `RawActorObservation`. |
| `src/DDCore.Observation/LiveWorldSnapshotProvider.cs:90-103` | Writes `rawActor.BaseId` into `ActorSnapshot.ObjectId`; therefore the JSON field called `objectId` is a BaseId, not `GameObjectId`. |
| `LiveWorldSnapshotProvider.cs:274-309` | Identifies only EO floor 10 from two encounter BaseIds. Every other started duty becomes `DeepDungeonKind.Unsupported` / `duty-unidentified`. |
| `src/DDCore.Core/World/WorldSnapshot.cs:170-203` | Actor position is a mandatory `Coordinate2D` without per-value source/quality/absence metadata. |
| `WorldSnapshot.cs:301` | `IsDeepDungeon` is currently `Dungeon.Kind != None`; an unidentified ordinary duty represented as `Unsupported` would also return true. It is not independent proof from `ConditionFlag.InDeepDungeon`. |
| `src/DDCore.Core/Decision/WorldSnapshotStrategyAdapter.cs:83` | Counts zero actor positions as evidence but does not assign room membership. |
| `src/DDCore.Core/Replay/ReplayCodec.cs` | Enforces schema and limits; Phase 5.5A accepts legacy schema 3 and current schema 4. |

The existing fail-closed behavior is correct: public actor data is not fabricated into room/topology/exit state, and Phase 5 remains stopped.

### Dalamud API 15

- [`IDutyState.ContentFinderCondition`](https://dalamud.dev/api/api16/Dalamud.Plugin.Services/Interfaces/IDutyState/) is a `RowRef<ContentFinderCondition>` for the current duty; `IsDutyStarted` is a separate state.
- [`IDutyState` duty events](https://dalamud.dev/api/api16/Dalamud.Plugin.Services/Interfaces/IDutyState/) provide lifecycle signals but are not exact DD-floor events.
- [`IObjectTable`](https://dalamud.dev/api/api16/Dalamud.Plugin.Services/Interfaces/IObjectTable/) supplies spawned objects and `LocalPlayer`.
- API 15 / .NET 10 is the current project baseline. Public services reviewed expose no typed exact DD floor, 25-room map, passage, pomander or DD timer contract.

### Game-data mapping evidence

The community-maintained game-data dump was checked at commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`. It is reproducible source evidence, not an official Square Enix API, so live/runtime cross-checks remain mandatory:

- [`ContentFinderCondition.csv`](https://github.com/xivapi/ffxiv-datamining/blob/44948e4cda5846eab0baa9872122a888fcc4f1b1/csv/en/ContentFinderCondition.csv) row `897` is `Eureka Orthos (Floors 1-10)` and references territory `1099`.
- EO rows `897-906` map ten-floor bands 1-10 through 91-100 to territories `1099-1108`.
- Heaven-on-High rows `540-549` and Palace of the Dead rows `174-178, 204-218` likewise identify ten-floor bands, but their territory rows are not uniformly sequential. Code must use a verified manifest rather than arithmetic assumptions.
- [`BNpcName.csv`](https://github.com/xivapi/ffxiv-datamining/blob/44948e4cda5846eab0baa9872122a888fcc4f1b1/csv/en/BNpcName.csv) row `6388` is labelled `dreaming brahmin` in English (`テンパード・ブラフミン` in the Japanese dump). This proves only the sheet row label for BaseId 6388 at that data revision; it does not explain why eight such battle-character entries exist at the origin inside EO.

Runtime classification should use numeric rows plus cross-checks, not localized names. A checked manifest must record the data revision, CFC row, expected territory row, content row/short code and floor band. At startup, missing or mismatched sheet data becomes Unknown and a diagnostic; it must not silently retain an old classification.

### BossMod and native evidence

BossMod fixed commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.

- [`BossMod/Framework/WorldStateGameSync.cs:941-1003`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941-L1003) calls `EventFramework.Instance()->GetInstanceContentDeepDungeon()` and reads DD ID, exact floor, tileset, gear/hoard, return/passage progress, 25 `MapData` entries, party room, pomanders, chests and magicite.
- [`BossMod/Data/DeepDungeonState.cs:7-16,25-41`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs#L7-L41) normalizes dungeon types and state. `PassageActive` uses progress `>= 11`; DDCore must not treat that threshold as verified for all current live data without a fixture.
- [`BossMod.Modules/Global/DeepDungeon/Rooms.cs:97-112,142-148`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L97-L148) derives room geometry from planmap event ranges and maps positions into room boxes.
- [`BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs:96-125,138-178`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs#L96-L178) combines room flags, revealed/passage state and geometry for decisions.

The pinned BossMod commit pins FFXIVClientStructs commit `fb26505058f33791e31c029e3678dbe70378b972`. Its [`InstanceContentDeepDungeon.cs:12-38,49,65,69`](https://github.com/aers/FFXIVClientStructs/blob/fb26505058f33791e31c029e3678dbe70378b972/FFXIVClientStructs/FFXIV/Client/Game/InstanceContent/InstanceContentDeepDungeon.cs#L12-L69) declares offsets for exact floor, return/passage progress, DD ID, 25 map flags and room indices. These are reverse-engineered internal structures, not a stable Dalamud service. Their presence is evidence for feasibility, not authorization to implement them.

## 3. Live replay facts

The fixed export folder was searched read-only according to repository `AGENTS.md`. Four valid `*.replay.json` files were found. The latest valid file and the user-described Phase 5.5A capture are the same:

- file: `ddcore-export-20261108-114807-8e655d1ad023455796a6a0beeba0421d.replay.json`;
- filesystem modified UTC: `2026-11-08T11:48:07.2517505Z`;
- `createdAtUtc`: `2026-11-08T20:47:47.5714353+09:00`;
- schema / producer: `4` / `0.5.1.0`;
- snapshots: `34`;
- SHA-256: `7890DDB45657AF6BAD9EDD2A9CEBFD4F336B8CB7D9EE68B8E38CBE3D361B39D2`;
- territory / CFC row: `1099` / `897` in every relevant snapshot;
- recorded actor snapshots: `479`;
- actor positions exactly `(0,0)`: `272`, all with JSON `objectId: 6388`;
- those 272 records represent 8 distinct actor identities repeated in every snapshot (8 minimum and maximum per snapshot), with full HP, no combat, not dead, and not destroyed;
- all 272 BaseId-6388 observations are zero; none has a non-zero recorded position;
- anonymous player position and rotation are present;
- dungeon remains `Unsupported`, exact floor remains null, and diagnostics contain `observation.dungeon.unidentified`.

The original file was not modified, copied or committed. The other schema-4 export has 30 snapshots outside this duty (`territoryId: 156`, no duty CFC row), and is useful as a negative classification fixture only after sanitization.

### Fact versus label

- **JSON fact:** IDs 1099/897, the counts above, unsupported/null classification and diagnostics.
- **External game-data evidence:** CFC row 897 maps to EO floors 1-10 and territory 1099 at the checked data revision.
- **User-supplied truth label:** the capture was made inside Eureka Orthos.
- **Not established:** exact floor within 1-10, why the eight BaseId-6388 objects are at zero, whether zero is an engine sentinel, or whether those objects can ever become spatially relevant.

## 4. Acquisition options and risks

| Information | Public/data-driven option | Other option | Recommendation and risk |
| --- | --- | --- | --- |
| DD kind | CFC RowId + territory cross-check + explicit `InDeepDungeon` condition | Native `DeepDungeonId` | Implement public classification first. It can identify kind/band without native access. Unknown/mismatch stays Unknown. |
| Floor band | CFC manifest says 1-10, 11-20, etc. | None needed | Record as `ObservedFloorRange`; never convert the band to an exact floor. |
| Exact floor | No verified public API | Visible UI extraction or native `InstanceContentDeepDungeon.Floor` | Keep Unknown initially. UI is locale/lifecycle fragile. Native field is precise-looking but patch-sensitive and needs separate approval/version gate/live transition fixtures. |
| Actor spatial data | Public object position plus validity/activity/targetable evidence and validated room geometry | Native object data | Add per-coordinate provenance/quality/usability. Never special-case BaseId 6388. Exact zero remains a value but is unusable for safety/pathing until corroborated. |
| Room topology | No typed public API | Native 25-entry `MapData`; planmap geometry | High-risk separate slice. Require full 25-room snapshot, reciprocal-edge validation and floor epoch. Geometry cannot prove revealed connections alone. |
| Current room | Self pose + validated geometry can cross-check | Native party room index | Native negative values must remain Unknown, never normalized to room 0. Require both sources to agree when both exist. |
| Passage/exit | Public actor/object evidence may locate a cairn but not prove activation | Native passage progress plus room flag | Separate position from activation. Validate progress threshold and actor state live before use. |
| Obstacles/traps | Public objects/AoEs cover only visible evidence | Planmap collision; optional trap source | Unknown unless observed. Absence of a visible object is not a safe floor. |

## 5. Recommended architecture

### Source-neutral evidence

Add source-neutral values without importing Dalamud/native types into Core:

- `ObservedDutyIdentity`: CFC row, territory row, deep-dungeon condition, source, timestamp and quality;
- `DeepDungeonClassification`: kind plus optional `FloorRange`, mapping revision and validation result;
- `ObservedSpatialValue`: nullable coordinate, syntactic quality, semantic usability and missing/suspicion reason;
- `FloorEpoch`: session + confirmed exact-floor change sequence; a floor band alone does not create an exact-floor epoch;
- `DeepDungeonTopologyObservation`: optional complete 25-room flags, current room and passage evidence, all scoped to a floor epoch.

### Adapters

1. `PublicDutyIdentitySource`: uses public `IDutyState`, `ICondition`, `IClientState` and checked game-data rows.
2. `PublicActorSpatialSource`: retains public actor evidence and marks spatial usability separately.
3. `DeepDungeonObservationFusion`: resolves conflicts, applies floor-epoch invalidation and emits Unknown on disagreement.
4. Future `INativeDeepDungeonMetadataSource`: optional and read-only, with no implementation until explicit approval.
5. Existing `WorldSnapshotStrategyAdapter`: consumes only fused authoritative values and cannot access a raw adapter.

No adapter may reference `IExecutionGateway`. Observation exceptions become Unknown diagnostics rather than plugin-wide failure.

### Replay compatibility

- Continue reading schemas 3 and 4 unchanged.
- In a future schema 5, rename the semantic actor field to `baseId`; keep a legacy JSON alias/converter for schema-4 `objectId`.
- Schema-4 actor coordinates become `LegacyUnqualified`; exact zero is preserved but spatially unusable unless independent evidence validates it.
- New recordings write nullable position plus quality/usability/reason. Missing must never be serialized as `(0,0)`.
- Do not rewrite old exports in place.

## 6. Implementation order

### B1: public classification and band (recommended first approval)

1. Record CFC RowId independently from `IsDutyStarted`; record explicit deep-dungeon condition separately.
2. Add a versioned manifest for all supported DD CFC/territory pairs and ten-floor bands.
3. Cross-check CFC row, referenced territory, observed territory and short-code/content evidence.
4. Output kind and floor range only on full match; otherwise Unknown with a stable reason code.
5. Remove the semantic shortcut where any `Unsupported` duty implies `IsDeepDungeon=true`.

### B2: actor spatial quality and schema 5

1. Correct `ObjectId`/`BaseId` naming while preserving schema-4 reads.
2. Add nullable/quality-qualified actor positions and public evidence needed to assess usability.
3. Mark the eight repeated zero entries as suspicious by generic evidence rules, not BaseId.
4. Exclude Unknown/Suspicious positions from hazards, room assignment, distance and path decisions.

### B3: exact floor prototype (separate high-risk approval)

Read only exact DD kind/floor through an optional native adapter. Gate on API/game/struct compatibility, pointer availability, `InDeepDungeon`, public classification agreement and monotonic fixture checks. Any mismatch disables the adapter for the session. Do not read map/resources in this slice.

### B4: topology vertical slice (later approval)

Add complete map/current-room/passage observations, validate against planmap geometry and self pose, and invalidate all floor-scoped state on confirmed floor change. Only after this can real-data Phase 5 decisions be reconsidered.

## 7. Test plan and acceptance criteria

### Public classification

- table-driven tests for every supported PotD, HoH and EO CFC/territory pair;
- non-sequential PotD/HoH territory mapping tests;
- unknown CFC, unknown territory, mismatch, no duty, not-in-DD and reconnect cases all return Unknown;
- row 897 + territory 1099 + DD condition returns EO with range 1-10, never an exact floor;
- mapping revision mismatch fails closed.

### Spatial/replay

- missing coordinate and valid direct origin remain distinct;
- schema 3/4 replay compatibility and schema-5 round trip;
- schema-4 zero positions become `LegacyUnqualified`, not automatically missing or safe;
- repeated zero/full-HP/inactive actors are spatially quarantined without a BaseId-specific rule;
- non-finite, stale, destroyed and out-of-validated-geometry coordinates are unusable;
- uncertain positions cannot create hazards, room membership or a route.

### Floor/topology (only after approval)

- fixtures for pointer unavailable, wrong version, outside DD and public/native mismatch;
- independently labelled transitions across at least two floors of each supported DD;
- floor epoch invalidates rooms, enemies, passage, traps and resources immediately;
- 25-room count, reciprocal edges, room-index range and geometry containment;
- negative/invalid native room stays Unknown; valid room zero remains room zero;
- passage closed/open comparisons before and after activation.

### Global acceptance

- `DisabledExecutionGateway` is unchanged and remains the only execution gateway;
- the existing 223 tests on Draft PR #7 plus new tests pass; Release build, format, vulnerability and prohibited-operation audits pass;
- original replay hashes are unchanged and no raw replay is committed;
- same normalized input produces the same fused observation and strategy result;
- incomplete/conflicting data produces `Stop` / Unknown, never a fabricated safe result;
- live behavior not exercised is reported as unverified.

## 8. Explicit approvals required

The following must each be approved before implementation:

1. **B1 public DD classification/floor-band implementation** and the supported mapping manifest.
2. **B2 schema 5 actor spatial-quality migration**, including the `objectId` to `baseId` semantic correction.
3. **Any FFXIVClientStructs/native read**, beginning with exact floor only. Approval for floor does not authorize rooms, passage, resources or writes.
4. **Planmap/geometry parsing** and any packaged game-data cache or generated manifest.
5. **UI extraction** if chosen as an alternative exact-floor source.

None of these approvals authorizes movement, combat, target changes, item use, input injection or a non-disabled execution gateway.

## 9. Remaining live verification

- Capture EO at two independently labelled exact floors within the same 10-floor CFC band to prove that CFC cannot identify the exact floor.
- Capture one floor band from PotD and HoH to validate the manifest and public condition behavior.
- Record the eight BaseId-6388 entries with additional public targetable/object-index/activity evidence and observe whether they ever leave the origin.
- Compare self pose, visible room and any future native room value, including a legitimate room zero.
- Record before/after passage activation and a confirmed floor transition.

Phase 5.5B implementation is feasible as bounded slices. The recommended immediate approval is **B1 only**, followed by B2. Native exact-floor work remains a separate high-risk decision.
