# Phase 5.5 Deep Dungeon observation acquisition research

## Conclusion

Phase 5 strategy decisions cannot be made actionable from the current public-API replay contract alone. Dalamud API 15 publicly exposes login/territory/duty state, the local player and battle actors, positions, rotations, casts and statuses. It does not expose a verified typed contract for Deep Dungeon kind, floor, room graph, passage, traps, pomanders or remaining time.

BossMod obtains most missing DD state through `FFXIVClientStructs` in `WorldStateGameSync.UpdateDeepDungeon`, not through a public Dalamud DD service or the 37-function BossMod IPC surface. That implementation is valuable evidence for an independently owned adapter, but it is not a stable public API. This phase therefore recommends no new live access until the model can preserve provenance, quality and absence separately and each internal field has been validated on a disposable read-only prototype.

This document is research and design only. No production observation code, native access, gameplay operation or Phase 6 work was added.

## Evidence baseline

- DDCore branch: `phase5/strategy-engine`, head `327152c3543e854c2bda30e15371932d5e19ac63` before this document-only update. The Phase 5 real-data implementation is commit `0c0f04c` in its history.
- BossMod: fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.
- BossMod FFXIVClientStructs submodule at that commit: `fb26505058f33791e31c029e3678dbe70378b972`.
- Local Dalamud assembly: `Dalamud.dll` 15.0.3.9 with its XML API documentation.
- Official public API references: `IClientState`, `ICondition`, `IObjectTable`, `IDutyState` and `IGameGui` for Dalamud API 15.
- Original replay exports: two unmodified schema-3 documents, 412 snapshots total. The candidate `ddcore-export-20261107-180939-e7bd7997efe34218aadad2a978beb81e.replay.json` has 124 snapshots and SHA-256 `ED9809FF3066874C10A79150CBE75541F301C95674AC335DF1FE080057D0683D`.
- The candidate's floor 3 to floor 4 attribution is a user-provided truth label. Neither floor number nor a transition event exists in the JSON.

## Acquisition matrix

`Confirmed` means the cited API or source directly provides the value. `Candidate` means a concrete source exists but live meaning, stability or mapping still needs verification. `Unknown` means no verified acquisition method was found.

| Priority | Observation | Public Dalamud API 15 | BossMod evidence | Current DDCore | Recommended status and method |
|---|---|---|---|---|
| P0 | DD kind | `ICondition[InDeepDungeon]` confirms only that the player is in a DD; territory/content identifiers are also public | `UpdateDeepDungeon` reads native `DeepDungeonId` | Only EO10 is estimated from two encounter actor BaseIds; otherwise `Unsupported` | **Candidate.** First use verified content/territory mapping plus `InDeepDungeon`; retain Unknown until live fixtures prove every DD. Do not treat encounter inference as kind truth. |
| P0 | Current floor | No verified public property | Native `InstanceContentDeepDungeon.Floor` | Nullable and normally unavailable; user-labelled transition is not encoded | **Candidate, high risk.** A separate read-only native adapter could expose it only after version/signature and live transition validation. Public-only fallback remains Unknown. |
| P0 | Player position/heading | `IObjectTable.LocalPlayer`, then `IGameObject.Position` and `Rotation` | BossMod also has party actor state | Captured live, but the privacy filter removes Player/Party actors from exports | **Confirmed live, absent from current export.** Add an anonymized self-pose field rather than persisting an actor identifier. |
| P0 | Current room | No verified public property | Native `Party.RoomIndex`; `AutoClear` can also map position into `Rooms` geometry | Not represented; Phase 5 adapter sets Unknown | **Candidate.** Prefer validated room geometry plus self pose as a cross-check. Never convert a negative/native unknown room to room 0. |
| P0 | Room structure/connections | No verified public property | Native 25-entry `MapData`; `FloorPathfind` interprets connection flags; `Rooms` reads planmap geometry | Strategy requires a complete 25-room graph; none is observed | **Candidate, high risk.** Normalize a full immutable floor epoch from native map data only after flag/coordinate validation. Geometry parsing is an independent fallback, not proof of revealed connections. |
| P1 | Passage location/open state | ObjectTable may expose a cairn actor, but no verified semantic ID/state contract was found | Native `PassageProgress`; `DeepDungeonState` treats progress >= 11 as open; `AutoClear` obtains cairn actor position | `ExitObservation.Unknown()` | **Candidate.** Combine independently verified object identity/location with native progress. A position alone must not imply open. |
| P1 | Enemy position/room | Battle actor identity, position, rotation, HP, validity and casts are public | `FloorPathfind`/`AutoClear` map actors to room rectangles | Position/HP/death/casts recorded; room membership absent | **Partly confirmed.** Keep public actor data; derive room only when a validated floor geometry contains the coordinate. Otherwise room stays Unknown. |
| P1 | Explored map | No verified typed public property | `FloorPathfind.Revealed` derives it from native room map data | Not represented by the live adapter | **Candidate.** Add per-room observation with source and floor epoch; do not infer exploration merely from actor visibility. |
| P1 | Enemy casts/hazards | `IBattleChara` exposes cast action, target, progress and duration; it does not expose a verified cast ground position or cast-specific rotation | BossMod mechanic modules and `AIHints` provide action-specific AoEs internally | Casts are observed; Gancanagh-only analyzer supports three actions; unknown casts fail closed | **Confirmed casts, partial hazards.** Continue public casts plus independently attributed analyzers. Unsupported actions remain Unknown, never an empty-safe hazard set. |
| P2 | Traps | No verified public hidden-trap API | BossMod uses optional `Palantir.Traps` IPC; visible/triggered trap actors/actions provide only partial evidence | Unknown | **Unknown for complete trap state.** Visible/triggered evidence may be recorded as partial. Hidden traps remain Unknown unless an optional, separately versioned provider is explicitly chosen. |
| P2 | Pomanders/demiclones | No verified public typed contract | Native `Items` count/flags and `Magicite` arrays | Unknown | **Candidate, high risk.** Isolate native reads and preserve count, usability and active state separately. Do not infer from UI text without a locale/version contract. |
| P2 | Remaining time | No verified DD-specific public property found | No verified production DD timer was found in `DeepDungeonState` or `UpdateDeepDungeon` | Unknown | **Unknown.** Keep Unknown. A generic content timer or visible UI countdown must not be claimed equivalent until verified. |
| P2 | Floor transition | Territory/map/instance and duty events are public but may not change between DD floors | Native floor value changes; BossMod resets DD state when leaving DD | Session key uses login/territory/content only, so same-territory floor changes are missed | **Candidate.** A confirmed floor change creates a new floor epoch and invalidates rooms, enemies, exit and resources. Public events are reset hints, not floor truth. |

## Source-specific findings

### Dalamud public API 15

- `ICondition` exposes `ConditionFlag.InDeepDungeon`, but the flag carries no DD kind or floor.
- `IClientState` exposes territory, map and instance identifiers plus zone/map/instance/login events.
- `IObjectTable.LocalPlayer` and `IGameObject.Position`/`Rotation` provide live self pose. `IGameObject.Position` is non-nullable, so the API value alone cannot distinguish a legitimate origin from a source that substituted zero.
- `IObjectTable` and `IBattleChara` provide actor identity/base ID, position, rotation, dead state, status list and cast action/target/progress/duration.
- `IDutyState` exposes content finder condition and duty-start state.
- `IGameGui.GetAddonByName` and `GetAgentById` expose native pointers, not a typed, stable DD data contract. Their existence is not evidence that floor/resources can safely be parsed from UI.

The local API 15 XML evidence is version-specific and was checked at these lines: `ConditionFlag.InDeepDungeon` 4558, `ICondition[ConditionFlag]` 61103, `IClientState.TerritoryType/MapId/Instance` 60979/60984/60989, `IObjectTable.LocalPlayer` 62969, `IGameObject.Position/Rotation` 8372/8377, `IBattleChara.StatusList/CastActionId` 7979/7999 and `IDutyState.ContentFinderCondition` 61486. These line numbers identify the local `Dalamud.xml` 15.0.3.9 evidence; the linked official pages below are the portable reference.

### BossMod fixed commit

At `BossMod/Framework/WorldStateGameSync.cs:941-1003`, `UpdateDeepDungeon` obtains `InstanceContentDeepDungeon` and publishes DD ID, floor, gear, hoard, return/passage progress, 25 room entries, party room, items, chests and magicite. This file imports FFXIVClientStructs and therefore demonstrates native-structure access rather than a public Dalamud service.

Important limitations:

- The tileset conversion around lines 950-956 contains a TODO and must not be treated as authoritative DDCore mapping.
- BossMod normalizes a negative party room to zero near line 1003. DDCore must preserve it as Unknown because room zero is valid.
- BossMod's hidden-trap knowledge is optional Palantir IPC (`BossMod.Modules/Global/DeepDungeon/Traps.cs:18-38`), so BossMod alone does not prove complete trap acquisition.
- No verified DD remaining-time source was found in `DeepDungeonState` or `UpdateDeepDungeon`.
- The fixed BossMod IPC service exposes no DD snapshot, room, passage, resource, hazard or strategy DTO. Reachability cannot substitute for DD data capability.

## Method comparison

| Method | Accuracy potential | API/update risk | Testability | Recommendation |
|---|---|---|---|---|
| Public Dalamud API | High for actors, pose and casts; insufficient for DD topology/resources | Lowest of the compared live methods | Good with source-neutral adapter mocks | Default source for shared observations. |
| Independent implementation informed by BossMod | High if every native field is independently verified | High; layout and semantics can change with game/client-struct updates | Requires captured native fixtures, version gates and live transition tests | Consider only in an isolated read-only adapter after model changes. Do not copy infrastructure wholesale. |
| New read-only game-internal access | Potentially exposes floor/map/resources | Highest safety and maintenance risk; not a public contract | Poor until a reproducible fixture boundary exists | Research candidate only in Phase 5.5; no implementation authorization. |
| UI extraction | Potentially exposes visible floor/time/resources | Locale, layout, addon lifetime and patch dependent; `IGameGui` is pointer-level | Requires multilingual, patch-specific fixtures | Last-resort candidate. Do not use OCR/text as authoritative state without explicit validation. |
| Preserve Unknown | No fabricated availability | No compatibility risk | Excellent fail-closed behavior | Required fallback for every field and every failed source. |

## Proposed observation architecture

### 1. Source-neutral envelope

Every strategy-relevant value should carry:

- `Availability`: Known, Unknown, Unsupported, Stale or Invalid.
- `Source`: PublicDalamud, NativeDeepDungeon, DerivedGeometry, OptionalProvider, UserLabel or Replay.
- `ObservedAt`, `Sequence` and `FloorEpoch`.
- `Confidence`: Direct or Derived; not a numeric probability.
- optional evidence code suitable for diagnostics without storing names or account data.

This replaces the current binary Known/Unknown limitation without weakening the strategy engine's fail-closed behavior.

### 2. Nullable pose contract

Actor/self positions require an availability marker plus an optional coordinate. `(0,0)` remains a valid coordinate when explicitly observed; missing position is represented by absence, never by a zero vector. Persist the local player's anonymized pose separately so privacy filtering need not remove it with player actors.

### 3. Floor-scoped epoch

Create a floor epoch only from an authoritative floor observation or an explicit user-labelled boundary. On a confirmed epoch change:

1. increment/reset the floor epoch;
2. invalidate room graph, current room, exploration, passage, traps and floor-scoped enemy membership immediately;
3. retain only observations explicitly scoped outside the floor;
4. prevent strategy evaluation until required new-epoch data is present.

Territory, map, instance, duty and session events can force invalidation, but they cannot assert a new floor number by themselves.

### 4. Adapter separation

- `PublicDalamudObservationSource`: existing public actor/duty/condition data.
- future `IDeepDungeonMetadataSource`: normalized DD kind/floor/topology/resource observations; no native types cross its boundary.
- `ObservationFusion`: combines values only when epoch/source rules agree; conflicts become Invalid/Unknown.
- `WorldSnapshotStrategyAdapter`: consumes normalized data and continues to leave missing strategy fields Unknown.
- `DeepDungeonStrategyEngine`: remains pure, deterministic and execution-independent.

Any future native adapter must be optional, version-gated, read-only, disposable, and unable to reach `IExecutionGateway`. `DisabledExecutionGateway` remains mandatory.

## Minimal implementation order after approval

1. **Provenance/availability and anonymized player pose.** Add quality-qualified optional coordinates and floor epoch to the source-neutral model and replay schema. This removes `(0,0)` ambiguity and restores self pose without persisting identity.
2. **DD kind/floor acquisition.** Validate `InDeepDungeon` plus content mapping first. If public data cannot establish floor, prototype the single native floor field behind an optional interface with captured fixtures and hard version failure. Do not add room/resource reads at the same time.
3. **Room/current-room/passage vertical slice.** Normalize the 25-room map, validate reciprocal connections, cross-check current room against self pose/geometry, and separate passage position from open state. Only then can Phase 5 evaluate a safe non-Stop route on real data.

Traps, pomanders/demiclones and time follow only after the P0/P1 topology slice is stable. Producing a non-Stop decision is not a success criterion; correct Unknown/Stop remains the required result for incomplete or conflicting data.

## Required live verification

- Capture the same character across at least one confirmed floor transition while independently noting DD kind and floor.
- Verify whether territory/map/instance/content IDs change across that transition.
- Compare native floor/room/map values with the visible map and self position on multiple rooms, including valid room zero and any negative room value.
- Verify passage progress threshold and cairn actor position before and after activation.
- Test logout, duty exit, wipe/re-entry and zone changes for epoch reset and stale-data rejection.
- Record a legitimate near-origin coordinate if available to prove zero is not a missing sentinel.
- Verify pomander, demiclone and chest field semantics through controlled inventory changes.
- Treat traps and remaining time as Unknown until separately validated.

No item above was tested in game during this phase.

## Unresolved items and implementation gate

- A stable public source for floor, room map, passage, resources and remaining time is not confirmed.
- The exact patch/version compatibility boundary for `InstanceContentDeepDungeon` is unmeasured.
- DD kind mapping from public territory/content identifiers is not yet validated across all three DDs.
- UI addon names/layouts and visible timer equivalence are unverified.
- Complete hidden-trap acquisition would require an optional third-party provider or another unverified source.

The bounded model/provenance and public self-pose slice is implemented in Phase 5.5A and documented in `phase5.5a-observation-foundation.md`. Native floor/topology access remains unimplemented and requires separate explicit approval. Phase 6 is not ready.

## Primary sources

- [Dalamud `IClientState`](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/)
- [Dalamud `ICondition`](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/ICondition/)
- [Dalamud `IObjectTable`](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IObjectTable/)
- [Dalamud `IDutyState`](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDutyState/)
- [Dalamud `IGameGui`](https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IGameGui/)
- [BossMod `WorldStateGameSync.cs` at the fixed commit](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941-L1003)
- [BossMod `DeepDungeonState.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs)
- [BossMod `FloorPathfind.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs)
- [BossMod `Rooms.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs)
- [BossMod `Traps.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Traps.cs)
- [BossMod BSD-3-Clause license](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/LICENSE)
