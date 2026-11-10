# Phase 5.6 - Deep Dungeon environment research

## 1. Scope and evidence baseline

This document is a read-only research result. It does not add room discovery, navigation, game input, internal-memory access, signature scanning, or execution behavior.

Evidence was checked against:

- DDCore branch base `c1ef53cf51fcc21f52ea4edfffb6f3153b854269`;
- installed Dalamud API 15, assembly version `15.0.3.9`, product commit `17cd86dfacbc1016c6e5aac0d49833b774ce037c`;
- BossMod fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`;
- the original replay files in the configured local export folder, read without modification.

Classification used below:

- **Confirmed**: the public contract, current implementation, or original live evidence directly contains the value;
- **Candidate**: a concrete public observation exists, but DD-specific meaning or live behavior is not verified;
- **Unavailable**: no acquisition contract was found in the investigated public API surface;
- **Unknown**: evidence is insufficient to classify the value.

`Candidate` is not usable strategy evidence. A candidate remains Unknown at the environment/strategy boundary until independently validated.

## 2. Conclusion

### Confirmed facts

Dalamud API 15 publicly exposes the local player and currently spawned game objects, including generic object kind, BaseId, targetability, position, and rotation. It also exposes territory, map and instance identifiers, duty state, framework updates, and read-only game data access. DDCore already records anonymous player pose and enemy observations with `Usable`, `Quarantined`, and `Missing` spatial quality.

The investigated public contracts do **not** expose a typed Deep Dungeon room graph, room index, passage progress, wall/collision mesh, traversable navmesh, hidden traps, or structured randomized floor map. `MapId` identifies the current map; it is not a room graph. `IGameGui.ScreenToWorld` performs a raycast for a supplied screen point; it does not enumerate collision or traversable space.

BossMod obtains the missing DD state from sources outside the permitted Phase 5.6 boundary: `InstanceContentDeepDungeon` native data, static Lumina level assets, bundled wall data, and optional Palantir IPC. BossMod therefore proves that the algorithms need those inputs, but does not prove that DDCore can obtain them through public Dalamud API 15.

### Design judgment

The next implementation must begin with a bounded, opt-in recording of public generic interactable observations. It must not feed the existing BFS or strategy engine. Room topology and portal state must remain Unknown until a separately approved source is demonstrated and validated.

## 3. Acquisition matrix

| Target | Status | Public evidence | Current DDCore/replay evidence | Safe interpretation |
| --- | --- | --- | --- | --- |
| Player position and heading | **Confirmed** | `IObjectTable.LocalPlayer`, `IGameObject.Position`, `Rotation` | Anonymous pose exists in all 869 schema-5 snapshots | Usable only when finite and metadata quality is Direct |
| Enemy position and movement samples | **Confirmed**, quality-gated | spawned object table and character properties | enemy-only actor observations; 8,214 Usable and 2,011 Quarantined | Only Usable coordinates may enter distance/AoE reasoning |
| Generic object kind/BaseId/position/targetability | **Confirmed** as generic fields | `IGameObject.ObjectKind`, `BaseId`, `Position`, `Rotation`, `IsTargetable` | current live adapter skips non-character objects | May be recorded as unclassified public evidence; not as a DD semantic |
| Treasure/coffer identity and position | **Candidate** | generic `ObjectKind.Treasure`/BaseId fields exist in installed API | no Treasure actor is present in schema-5 exports | Requires live mapping per DD and patch; BaseId alone is not sufficient proof |
| Passage device identity and position | **Candidate** | generic EventObj/Aetheryte observations are possible | current adapter does not capture them; replay has none | Record generic object first; do not infer activation/open state |
| Passage active/open state | **Unavailable** | no public DD passage-progress contract found | absent | `IsTargetable` may be recorded but must not be equated to open without live proof |
| Room position, shape, identifier | **Unavailable** through investigated public contracts | no room API; public game data can return static files only | absent | Do not cluster actors or fabricate rooms |
| Room adjacency/passages | **Unavailable** | no public 25-room map/connection flags | absent | Existing BFS must remain disconnected |
| Current room / actor room membership | **Unavailable** | no public room index | absent | Cannot derive until trusted room geometry exists |
| Walls/obstacles/traversable area | **Unavailable** | no public collision/navmesh enumeration | absent | `ScreenToWorld` is insufficient; do not build paths from it |
| Floor map structure/explored area | **Unavailable** | `MapId` only; no structured DD floor-map API | absent | Keep Unknown |
| Revealed trap actor | **Candidate** | generic object/action observation may expose a visible object | current adapter/replay contains no verified trap semantic | Requires live evidence and stable mapping |
| Hidden traps | **Unavailable** | no public hidden-trap API found | absent | Keep Unknown; never infer safety |
| DD kind and ten-floor range | **Confirmed** for supported mappings | duty/CFC/territory public data | EO 1-10 verified in prior live evidence | Not equivalent to exact floor or topology |
| Exact floor | **Confirmed only in separate Phase 5.5D diagnostic for EO 1->2** | chat/log diagnostic path | not present in schema-5 replay/WorldSnapshot | Do not treat as environment topology input yet |

## 4. Dalamud API 15 public contracts

The links below are pinned to the installed API product commit.

| Contract | Confirmed surface | Update/reset behavior | Missing/invalid risks | Constraint |
| --- | --- | --- | --- | --- |
| [`IObjectTable`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IObjectTable.cs#L8-L93) | current spawned objects, `LocalPlayer`, character/event partitions | sampled on framework updates; contents change as objects spawn/despawn | local player can be null; entries can disappear or become invalid | provides generic objects, not DD semantics/topology |
| [`IGameObject`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Game/ClientState/Objects/Types/GameObject.cs#L18-L138) | GameObjectId, EntityId, BaseId, ObjectKind, targetable, position, rotation, validity | values are live views and must be copied during capture | stale references, non-finite/uninitialized positions, reused IDs | no portal/chest/open-state meaning is guaranteed |
| [`IClientState`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IClientState.cs#L33-L111) | territory/map/instance identifiers and change events, login state | events on zone/map/instance changes | same-territory floor changes need not fire | reset hints only; not floor truth or room topology |
| [`IFramework`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IFramework.cs#L28-L54) | update event, update time/delta | every game framework update | high frequency; callbacks must be bounded | scheduling only, not environment data |
| [`IDataManager`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IDataManager.cs#L14-L105) | Excel sheets and game files | static data access | row/file may be absent; modified data is separately reported | static assets do not establish the current randomized layout |
| [`IDutyState`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IDutyState.cs#L8-L67) | current CFC row, duty started/wiped/completed | duty lifecycle events | not every DD floor transition is a duty lifecycle event | content identity only |
| [`ICondition`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/ICondition.cs) | condition flags including DD presence | flag changes | does not expose room/floor topology | presence signal only |
| [`IGameGui`](https://github.com/goatcorp/Dalamud/blob/17cd86dfacbc1016c6e5aac0d49833b774ce037c/Dalamud/Plugin/Services/IGameGui.cs#L51-L144) | map opening, world/screen conversion, screen raycast, low-level addon accessors | caller-driven | raycast only tests a supplied screen ray; addon APIs return pointers | pointer/UI-memory paths are outside this phase; no structured map getter |

The installed API 15 `ObjectKind` enum contains generic kinds including `Treasure`, `Aetheryte`, and `EventObj`. This was confirmed from the installed runtime assembly. It does not by itself identify a DD coffer, passage, or activation state.

## 5. Current DDCore capability and gaps

- `src/DDCore.Plugin/Observation/DalamudObservationSource.cs:39-68` reads anonymous local-player pose.
- The same source at lines 74-93 accepts only `ICharacter`, maps combatant `BattleNpc` to Enemy, and skips `RawActorKind.Unknown`. EventObj, Treasure, Aetheryte, and other interactables are therefore not captured.
- `src/DDCore.Observation/ActorSpatialQualifier.cs:8-65` implements Missing/Quarantined/Usable handling, including origin and lifecycle quarantine.
- `src/DDCore.Observation/ObservationRecording.cs:474-506` permits sanitized Enemy/EventObject records, but the live source currently produces no EventObject records.
- `src/DDCore.Core/Decision/WorldSnapshotStrategyAdapter.cs:27-43` deliberately marks current room, rooms, exit, enemy room membership, traps, pomanders, resource readiness, and time as unavailable.
- `src/DDCore.Core/Navigation/RoomPathfinder.cs:20-158` validates a complete reciprocal 5x5 graph before BFS. It does not discover rooms or edges.
- `src/DDCore.Core/Execution/IExecutionGateway.cs:49-54` remains a permanently unavailable `DisabledExecutionGateway`.

These gaps are intentional safety boundaries, not implementation failures.

## 6. BossMod comparison at the fixed commit

BossMod is BSD-3-Clause licensed. Algorithm reuse must retain attribution; native acquisition and runtime architecture are not copied into Phase 5.6.

| Area | BossMod input/source | Evidence | Equivalent public DDCore input? | Reuse judgment |
| --- | --- | --- | --- | --- |
| `DeepDungeonState` | native `InstanceContentDeepDungeon` room flags and progress | [`DeepDungeonState.cs:1-40`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs#L1-L40) | No | state concepts can inform a source-neutral model; acquisition cannot be claimed public |
| `WorldStateGameSync.UpdateDeepDungeon` | `GetInstanceContentDeepDungeon()`, Floor, MapData, party room, chests, passage progress | [`WorldStateGameSync.cs:941-990`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941-L990) | No | internal/native, patch-sensitive, prohibited in this phase |
| `FloorPathfind` | complete 25-entry native `RoomFlags` map | [`FloorPathfind.cs:14-60`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs#L14-L60) | Algorithm yes; live inputs no | DDCore already has attributed pure BFS; keep gated |
| `Rooms` | room flags plus `planmap.lgb` EventRange boxes and static wall resources | [`Rooms.cs:14-53`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L14-L53), [`69-163`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L69-L163) | Static data is publicly readable, but current layout/room flags are not | parsing approach is Candidate research; it cannot prove the current graph alone |
| `AutoClear` | actor OIDs/targetability plus native passage state and room membership | [`AutoClear.cs:400-446`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/AutoClear.cs#L400-L446) | generic objects only | IDs are reference evidence, not live DDCore truth; validate independently |
| `DeepDungeonAI` | actor positions, combat state, DD state, AIHints/action queue | [`DeepDungeon.cs:46-151`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Autorotation/Standard/xan/AI/DeepDungeon.cs#L46-L151) | only a subset of read-only observations | operation/action code is out of scope; no direct environment acquisition to reuse |
| `Traps` | optional `Palantir.Traps` IPC plus room geometry and revealed actors/actions | [`Traps.cs:15-38`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Traps.cs#L15-L38) | No hidden-trap source | hidden trap completeness remains Unavailable |

## 7. Original schema-5 replay re-evaluation

The configured folder contained 12 replay files. Six were schema 5 / producer `0.5.3.0` and were used for this environment review:

- `ddcore-export-20261108-131759-af866dd10519417da0c05aac56debf0d.replay.json`
- `ddcore-export-20261108-132017-3ea02b34168b4947a60a43e834ba8bf5.replay.json`
- `ddcore-export-20261108-132137-c6b0e482d1724f7e8d7b069e89083e00.replay.json`
- `ddcore-export-20261108-134526-44921e4d021b41fd854283678c50c8cb.replay.json`
- `ddcore-export-20261108-135219-042a32a4ea474e1582fc987c68bd286e.replay.json`
- `ddcore-export-20261108-143027-aa5cbe505b0848bfb5872ce9c0cf8566.replay.json`

Read-only aggregate facts:

| Measure | Result |
| --- | ---: |
| Snapshots | 869 |
| Actor observations | 10,225 |
| Spatial Usable | 8,214 |
| Spatial Quarantined | 2,011 |
| Spatial Missing | 0 |
| Actor kinds | Enemy only |
| Anonymous player poses | 869 |
| DD classification | Eureka Orthos, 1-10 range |
| Exact floor | absent in every snapshot |
| Room/passage/wall/exit fields | absent |
| EventObject/Treasure records | absent |

The files therefore confirm player/enemy pose recording and B2 quality handling. They do not support identifying rooms, corridors, a passage, a coffer, collision, or explored map state. The originals were not modified, moved, or committed.

## 8. Remaining research questions

1. Which generic public object kinds/BaseIds are observed for passage devices and each coffer type in each DD?
2. Is targetability correlated with passage activation, and is that relationship stable enough to use? No current evidence establishes it.
3. Can static `planmap.lgb` room EventRanges be parsed without native current-layout state, and if so, what can they prove versus merely enumerate?
4. What explicit public event can invalidate a same-territory floor environment epoch when a chat event is missed?
5. Are visible traps consistently represented as generic objects/actions in all three DDs?

Until answered, each corresponding field remains Unknown and strategy output remains fail-closed.
