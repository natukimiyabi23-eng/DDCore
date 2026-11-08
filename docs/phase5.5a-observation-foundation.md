# Phase 5.5A observation foundation

## Scope

Phase 5.5A implements only the approved public-API observation foundation:

- reusable source, quality and missing-reason metadata;
- an anonymous player pose with nullable position and rotation;
- public Dalamud API 15 acquisition through `IObjectTable.LocalPlayer`;
- replay schema 4 output with schema 3 read compatibility;
- privacy-preserving local recording;
- fail-closed Phase 5 evidence conversion.

It does not implement DD kind/floor acquisition, room topology, passage, traps, resources, native structures, BossMod runtime integration, execution or Phase 6.

## Model

`ObservationMetadata` records:

- `Source`: `PublicDalamud`, `Replay`, `Mock`, `Derived` or `Unknown`;
- `Quality`: `Direct`, `Derived`, `Missing`, `Unsupported`, `Stale` or `Invalid`;
- `MissingReason`: absent for direct/derived data and required for every non-current value.

`AnonymousPlayerPose` contains only `Coordinate2D? Position`, `double? Rotation` and metadata. It has no entity ID, object ID, name, account or character field. A direct `(0,0)` is a valid coordinate. Absence is represented only as `Position=null` and `Rotation=null` with a reason.

The current slice treats position and rotation as one atomic public observation because Dalamud exposes both from the same `LocalPlayer` object. Future fields may reuse `ObservationMetadata`, but this phase does not migrate unrelated actor/DD observations.

## Public API adapter

`DalamudObservationSource` reads `IObjectTable.LocalPlayer` once per frame. A present object yields position and rotation without copying its identity into the pose. A missing object, getter exception or non-finite value produces an explicit missing/invalid observation and diagnostic rather than `(0,0)`.

The transient local entity ID remains in `RawObservationFrame` only for classifying the in-memory actor list. `SnapshotPrivacyFilter` still removes Player and PartyMember actors and their target/status references before persistence. The anonymous pose survives sanitization because it has no identifier.

Logged-out frames contain a missing pose. Leaving a duty can retain a valid public pose, but `DeepDungeonContext` becomes `None`; a pose alone never authorizes a DD strategy.

## Replay compatibility

| Data | Read | Write |
|---|---|---|
| Replay/world schema 3 | Supported; missing `playerPose` becomes `null`/Unknown | Not produced by current code |
| Replay/world schema 4 | Supported | Current output |
| Other schema versions | Rejected | Not produced |

Replay document and snapshot versions must match. Schema 3 snapshots cannot carry the Phase 5.5A field. JSON still rejects unknown properties and preserves the existing size, collection and ordering limits.

The two original local exports were read with the Phase 5.5A codec without modification:

- schema 3, 288 snapshots, SHA-256 `D2A6DBAD273EF46285759C9FCE43A630F014294B19D169F12937D20B4BD938E6`;
- schema 3, 124 snapshots, SHA-256 `ED9809FF3066874C10A79150CBE75541F301C95674AC335DF1FE080057D0683D`.

Both correctly yield no player pose because the old exports never contained one.

## Strategy conversion

`WorldSnapshotStrategyAdapter` exposes the anonymous position, rotation and metadata only as `StrategyObservationEvidence`. It does not infer a room from the coordinate and does not mark current room, topology, exit or resources as known. A missing player pose adds `playerPosition` to unavailable evidence. A direct origin does not.

The existing `DeepDungeonStrategyEngine` therefore remains fail-closed. Missing pose, content exit or the still-unavailable room/resource fields cannot produce an actionable decision. `DisabledExecutionGateway` is unchanged.

## Tests

Automated coverage includes:

- direct origin versus missing coordinates;
- invalid metadata and non-finite coordinates;
- public API object absent/read failure;
- player not generated, logout and content exit;
- schema 3 replay without the new property;
- schema 4 metadata round-trip;
- Unknown/content-exit strategy stop;
- removal of player/party identifiers while retaining anonymous pose.

## Live verification procedure

The user performs these steps; this implementation did not operate FF14 or Dalamud.

1. Load the package as a development plugin and leave recording OFF.
2. Log in outside a duty and confirm diagnostics remain stable; do not infer DD support from pose availability.
3. Enter a DD, start a short explicit recording, move through a position near but not necessarily at the origin, then stop and preview it.
4. Inspect the JSON locally: `playerPose.position`, `rotation`, source `publicDalamud` and quality `direct` should exist; no Player/Party actor identity or name should exist.
5. Log out or return to character selection and confirm a subsequent observation reports a missing pose without substituting `(0,0)`.
6. Leave the duty and confirm the DD strategy remains stopped even while a public player pose is available.
7. On any exception, stop recording, unload the development plugin and retain the local file for offline diagnosis. No game setting or original export needs replacement.

## Remaining work

- No live validation has been performed.
- DD kind/floor and floor epochs remain Phase 5.5B candidates and require separate approval.
- Room graph/current room/passage/resource acquisition is unchanged and Unknown.
- The public API adapter cannot prove that a coordinate is semantically usable for room mapping until live coordinate/geometry fixtures exist.
- Phase 6 is not ready.
