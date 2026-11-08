# Phase 5.5B B2 actor spatial quality and schema 5 implementation plan

## 1. Decision and scope

B2 should make actor coordinates explicitly nullable, qualified and safe to consume, then migrate replay output to schema 5. It must not attempt to explain BaseId 6388 through a special case, infer rooms from coordinates, or add native reads. The key rule is:

> A coordinate can be preserved as an observed value without being authorized for geometry, hazard, distance, room or route decisions.

B2 remains read-only. It does not add exact floor, topology, movement, combat, targeting, item use, input injection or a non-disabled execution gateway.

## 2. Current source facts

| Source | Confirmed current behavior | B2 implication |
| --- | --- | --- |
| `src/DDCore.Core/World/WorldSnapshot.cs:170-203` | `ActorSnapshot.ObjectId` is a `uint`; `Position` is a mandatory finite `Coordinate2D`; no per-actor spatial metadata exists. | Rename the semantic ID and replace mandatory position with a qualified observation. |
| `src/DDCore.Observation/RawObservation.cs:39-57` | Raw actor data calls the value `BaseId`, but position is mandatory. | Keep `BaseId`; allow absent/invalid spatial observations with reason codes. |
| `src/DDCore.Plugin/Observation/DalamudObservationSource.cs:156-195` | The public adapter writes `gameObject.BaseId` and `gameObject.Position`. | No new API is needed, but acquisition failures and non-finite values must become evidence rather than exceptions/default origin. |
| `src/DDCore.Observation/LiveWorldSnapshotProvider.cs:90-104` | `rawActor.BaseId` is passed into the JSON field named `ObjectId`; position is converted directly. | Correct the in-memory/serialized name in schema 5 and qualify position before constructing actors. |
| `src/DDCore.Core/Mechanics/CastHazardTracker.cs:35-82` | Hazard origin falls back from cast target location to `actor.Position`. | A quarantined/missing actor position must not create a hazard. Missing origin must return uncertainty, not safety. |
| `src/DDCore.Core/Decision/WorldSnapshotStrategyAdapter.cs:44-90` | Zero coordinates are counted as evidence, but spatial quality is not modeled. | Report usable/quarantined/missing counts and never derive strategy geometry from uncertain values. |
| `src/DDCore.Observation/ObservationRecording.cs:472-507` | Privacy filtering removes player/party actors and keeps enemy/event-object values. | Preserve quality metadata while continuing to exclude player/party identifiers. |
| `src/DDCore.Core/Schema/SchemaVersions.cs:3-9` | World/replay schema is 4, with schema 3 accepted as legacy. | Add schema 5 while keeping explicit readers for schemas 3 and 4. |
| `src/DDCore.Core/Replay/ReplayCodec.cs:48-96` | Strict JSON deserialization and header/snapshot schema pairing are enforced. | Version detection and migration must occur before validating the normalized schema-5 model. |

The latest original export remains schema 4 / producer 0.5.1.0. Its 272 exact-origin actor observations all carry JSON `objectId: 6388`, but the field actually contains `BaseId`. This establishes a repeatable suspicious pattern, not its cause. B2 must use generic evidence rules that behave identically for any BaseId.

## 3. Proposed schema 5 model

### Actor identity and BaseId

Replace the semantic `ActorSnapshot.ObjectId` member with `BaseId`. `ActorIdentity` continues to carry the session-scoped entity/generation identity used for cast tracking. Do not persist `GameObjectId` solely to solve spatial quality; it is unnecessary for the normalized replay and increases identifier surface.

Schema 3/4 input migration maps legacy JSON `objectId` to schema-5 `baseId`. Schema-5 output writes only `baseId`; it must not emit both names.

### Spatial observation

Introduce a source-neutral value such as:

```text
ActorSpatialObservation
  Position: Coordinate2D?
  Rotation: double?
  Metadata: ObservationMetadata
  Usability: Usable | Quarantined | Missing
  ReasonCode: string?
```

Required invariants:

- `Usable` requires finite position and rotation, `Direct` or `Derived` quality, and no reason code.
- `Quarantined` may preserve finite coordinates, including `(0,0)`, but requires a stable reason code.
- `Missing` stores neither position nor rotation and requires a missing reason.
- NaN/Infinity never enters `Coordinate2D`; it becomes `Invalid` + `Missing`.
- position and rotation must be qualified together. Partial pairs are invalid.
- an exact origin is not converted to null. Without independent corroboration, public actor origin is preserved as `(0,0)` but quarantined.

Initial generic rules should be deliberately conservative:

1. acquisition failure or unavailable object: `Missing`;
2. non-finite position/rotation: `Missing` with `Invalid` quality;
3. finite values on invalid/dead/destroyed actors: preserve them as `Quarantined` with a lifecycle reason;
4. exact origin from the public object table: `Quarantined` with `actor.spatial.origin-unverified`;
5. finite non-origin public observation on a valid live actor: `Usable`;
6. stale snapshot: retain the value only as `Quarantined` / `Stale`;
7. future independent evidence may validate an origin, but B2 must not invent that evidence.

The origin rule applies to every BaseId. BaseId 6388 must not appear in production classification logic.

## 4. Versioned replay migration

### Supported inputs and output

- schema 3 input: readable; no player pose; legacy actor `objectId` becomes `BaseId`; legacy position becomes `LegacyUnqualified` and quarantined;
- schema 4 input: readable; player pose/B1 optional fields retained; legacy actor ID/position migration is the same;
- schema 5 input/output: strict new actor/spatial representation;
- serialization: schema 5 only after B2; no in-place rewrite of original exports.

Avoid optional-constructor aliases that make one CLR model silently accept contradictory `objectId` and `baseId`. Instead, inspect the root replay schema first, deserialize a bounded version-specific DTO, reject unexpected/duplicate/conflicting members, and map it into the current immutable model. The normalized model then runs existing validation and collection limits.

`SchemaVersions` should retain named constants for both legacy versions and add current world/replay schema 5. The codec must enforce valid pairs: replay 3/world 3, replay 4/world 4, replay 5/world 5. Mixed pairs fail.

## 5. Consumer safety changes

### Mechanics and hazards

`CastHazardTracker` must request a usable origin. A schema-5 cast target location must either carry equivalent spatial qualification or be absent; a bare coordinate cannot bypass the actor gate. The tracker may fall back only to a usable actor position. If neither exists, it emits an unsupported/unknown diagnostic and no hazard. The aggregate mechanic safety result remains `UnknownOrUnsupported`; absence of a hazard is never treated as a safe area.

Cast target-location derivation in `LiveWorldSnapshotProvider` must use the target actor's qualified position. A quarantined target cannot create a derived cast location.

### Strategy and navigation

`WorldSnapshotStrategyAdapter` should expose counts for usable, quarantined and missing enemy positions plus stable reason-code aggregates. It must not map any B2 coordinate to rooms, routes or exits. `RoomPathfinder` remains disconnected from live spatial data. Unknown/quarantined evidence keeps Phase 5 stopped whenever authoritative inputs are absent.

### Diagnostics and recording

The Japanese diagnostics UI should show actor spatial totals and the latest quarantine reason without listing entity IDs or BaseIds. Recording persists only sanitized enemy/event-object spatial observations. Player/party actor removal and anonymous self-pose behavior remain unchanged.

## 6. Implementation slices

### B2.1: model and codec migration

1. Add schema-5 DTO/model and invariants.
2. Rename normalized `ObjectId` to `BaseId`.
3. Add explicit schema-3/4 readers and deterministic schema-5 writer.
4. Preserve all replay size/count/depth/unmapped-member limits.

Gate: schema fixtures and model tests pass before live-adapter changes.

### B2.2: public observation and qualification

1. Make raw actor position/rotation nullable with acquisition reason.
2. Add a pure `ActorSpatialQualifier` with the generic rules above.
3. Apply qualification in `LiveWorldSnapshotProvider` without BaseId-specific logic.
4. Reset any tracker state on existing session boundaries.

Gate: public origin, missing, invalid, stale and non-origin fixtures produce the expected quality/usability deterministically.

### B2.3: consumers and fail-closed propagation

1. Update analyzers/hazard tracking to require usable origins.
2. Update strategy evidence and diagnostics.
3. Update privacy filtering and bounded recording.
4. Audit every `.Position` consumer so no compatibility helper bypasses the gate.

Gate: uncertain coordinates cannot create hazards, room membership, distance, targets or routes.

### B2.4: verification and package

1. Re-run original schema-3/4 exports read-only and compare aggregate counts.
2. Create sanitized schema-5 fixtures; do not commit original exports.
3. Run Release, all tests, format, vulnerability, dependency and prohibited-operation audits.
4. Document live test steps and package/version/hash; keep the PR Draft.

## 7. Required tests

### Model and qualification

- missing coordinate versus preserved exact origin;
- exact origin is quarantined, not missing and not usable;
- the same origin result for BaseId 6388 and unrelated BaseIds;
- finite non-origin public observation is usable;
- NaN/Infinity, partial pairs, invalid actor, destroyed actor and stale frame;
- deterministic results for identical inputs and reset behavior across sessions.

### Replay compatibility

- schema 3 and 4 fixtures map legacy `objectId` to `BaseId`;
- schema 3/4 positions become `LegacyUnqualified`, including non-zero values;
- schema 5 deterministic round trip;
- mixed schema pairs, conflicting names, unknown members, oversized documents and malformed spatial metadata fail;
- original exports remain byte-for-byte unchanged and readable;
- privacy filter does not reintroduce player/party identifiers.

### Safety propagation

- quarantined/missing actor origin cannot create a hazard;
- quarantined cast target cannot create a target location;
- missing hazard origin returns unknown/unsupported rather than safe;
- strategy conversion never creates rooms/routes from B2 coordinates;
- `DisabledExecutionGateway` and emergency-stop tests remain unchanged and pass.

## 8. Acceptance criteria

- schema 3, 4 and 5 inputs are handled according to the explicit matrix; new output is schema 5;
- exact `(0,0)` remains distinguishable from missing data;
- no BaseId-specific coordinate rule exists;
- only `Usable` spatial observations reach geometry consumers;
- incomplete/conflicting data remains Unknown and Phase 5 stays fail-closed;
- no original replay is modified or committed;
- no Dalamud/native type enters Core/Observation contracts;
- no FFXIVClientStructs/native access, BossMod runtime dependency or gameplay operation is added;
- Release, all tests, format, vulnerability/dependency/prohibited-operation audits and CI pass;
- live behavior not exercised is documented as unverified.

## 9. Live verification after implementation

User-run verification should capture a new producer/schema-5 replay in EO 1-10 and inspect aggregate-only results:

1. confirm DD classification still displays EO 1-10 without an exact floor;
2. confirm the repeated origin actors are present as quarantined rather than missing/usable;
3. confirm visible non-origin enemies are usable;
4. confirm uncertain actors do not create hazards or strategy routes;
5. repeat one capture in PotD and HoH only when those environments are available;
6. retain all bands other than the observed case as unverified.

The fixed replay folder rule in repository `AGENTS.md` continues to apply. The user performs plugin enable/reload and live verification.

## 10. Approval boundary

This plan does not authorize implementation. B2 must wait for explicit approval. Approval of B2 authorizes only the schema/model/public-spatial/consumer-safety work above. It does not authorize exact-floor native reads, room topology, B3/B4, movement, combat, targeting, items, input injection, deployment, plugin activation or any PR merge.
