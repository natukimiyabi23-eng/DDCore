# Phase 5.6 - Environment observation model

## 1. Design boundary

This is a proposed read-only model, not an implemented contract. It separates raw public observations from DD semantics and from strategy decisions.

```text
Dalamud public observations
  -> normalization and quality gate
  -> EnvironmentSnapshot (no operation capability)
  -> semantic classifiers (still evidence-only)
  -> validated topology gate
  -> RoomPathfinder / strategy input

DisabledExecutionGateway remains unchanged and unavailable.
```

No environment value becomes actionable merely because a generic object was observed. Provenance, quality, freshness, floor epoch, and semantic confidence all have to pass their respective gates.

## 2. Reuse of existing primitives

The model should reuse rather than duplicate:

- `ObservationValueSource` and `ObservationValueQuality` from `WorldSnapshot`;
- nullable `Coordinate2D`/rotation and `ObservationMetadata`;
- `ActorSpatialObservation` and `SpatialUsability` (`Usable`, `Quarantined`, `Missing`);
- `DeepDungeonContext` for DD kind, exact floor, territory/content identity and floor range;
- `RoomConnectionFlags` and `RoomPathfinder` only after topology validation;
- immutable collections and sequence/time validation used by `WorldSnapshot`.

The current `ObservedValue<T>` only distinguishes Known and Unknown. Environment acquisition needs richer metadata, but the strategy boundary may still reduce every non-Confirmed/non-fresh value to Unknown.

## 3. Proposed model

Names are provisional and require implementation review.

```csharp
EnvironmentSnapshot
  SchemaVersion
  SessionId
  FloorEpochId
  Sequence
  CapturedAtUtc
  ValidUntilUtc?
  DeepDungeonContext
  AnonymousPlayerPose
  ObservedCollection<EnvironmentRoom> Rooms
  ObservedCollection<EnvironmentConnection> Connections
  ObservedCollection<EnvironmentObstacle> Obstacles
  ObservedCollection<EnvironmentObject> Objects
  ImmutableArray<EnvironmentDiagnostic> Diagnostics

ObservedValue<T>
  Value: T?
  Source: PublicDalamud | StaticGameData | Replay | Derived | Unknown
  Quality: Confirmed | Candidate | Missing | Conflict | Stale | Invalid | Unsupported
  ObservedAtUtc
  ValidUntilUtc?
  ReasonCode?
  EvidenceIds

EnvironmentRoom
  RoomId: int?
  GridIndex: int?
  Bounds: RoomBounds?
  Exploration: Unknown | Unexplored | Explored
  Metadata

EnvironmentConnection
  FromRoomId?
  ToRoomId?
  Direction?
  Traversal: Unknown | Open | Closed
  CorridorGeometry?
  Metadata

EnvironmentObstacle
  ObstacleId
  Geometry?
  Blocking: Unknown | Blocking | NonBlocking
  Metadata

EnvironmentObject
  EphemeralIdentity
  BaseId
  PublicObjectKind
  Spatial: ActorSpatialObservation
  IsTargetable: bool?
  Semantic: Unknown | PassageCandidate | CofferCandidate | RevealedTrapCandidate | Other
  State: Unknown | Inactive | Active | Opened
  SemanticMetadata
```

### Identity and privacy

- Never record object names, player names, chat text, account identifiers, memory addresses, or persistent player identifiers.
- Runtime GameObjectId/EntityId may be used transiently to deduplicate a floor epoch, but exported identifiers must follow the existing anonymization policy.
- BaseId and public ObjectKind may be recorded because they describe game data, not a player identity.
- `Semantic` remains Unknown until a separately versioned mapping has verified evidence for the current DD and game/API version.

## 4. Coordinate and quality rules

1. Missing coordinates are `null`; `(0,0)` is not converted to missing.
2. Existing B2 qualification remains authoritative:
   - `Usable`: finite, lifecycle-valid, and not otherwise quarantined;
   - `Quarantined`: observed but unsuitable for spatial reasoning;
   - `Missing`: no coordinate observation.
3. Quarantined/Missing coordinates cannot be used for AoE, distance, room membership, corridor, obstacle, or path calculations.
4. Static geometry and live object coordinates must declare their coordinate system and transformation. A transformation is not Confirmed until round-trip fixtures and visible landmarks agree.
5. A point inside a proposed room bound is only a derived room-membership candidate unless the bound and floor epoch are themselves Confirmed.
6. No cluster of enemies or player movement samples may be treated as a room boundary without explicit approval and validation.

## 5. Provenance and semantic confidence

Acquisition and interpretation are separate:

- **Acquisition provenance** answers where the raw field came from: public object table, public game data, replay, or derived calculation.
- **Semantic provenance** answers why a BaseId/ObjectKind is believed to be a passage, coffer, or trap.
- **Topology provenance** answers where room indices and edges came from.

A generic `EventObj` with valid coordinates can be Confirmed as an observed object but only Candidate as a passage. This distinction prevents public field availability from being mistaken for DD meaning.

Conflicting sources produce `Conflict`, not precedence-based overwriting. Unsupported or stale evidence cannot be promoted by combining multiple weak observations.

## 6. Floor epoch and invalidation

`FloorEpochId` identifies the lifetime of floor-scoped environment evidence. It is not the floor number.

Invalidate the entire environment snapshot when any of the following is observed:

- logout or local-player disappearance beyond a bounded grace period;
- DD exit;
- territory, content, map, or instance identity change;
- a verified exact-floor transition event;
- session change;
- sequence/time regression;
- source exception that makes snapshot consistency unknowable;
- TTL expiration.

On invalidation:

1. rooms, connections, obstacles, passage/coffer/trap semantics, and actor-room membership become Stale/Unknown immediately;
2. old floor objects are not merged into the new epoch;
3. no path is retained;
4. exact floor may remain Unknown until separately re-observed;
5. the strategy engine receives Unknown and returns its existing safe Stop behavior.

Public territory/map/instance events are reset evidence. They are not proof that every same-territory floor transition was observed. If Phase 5.5D notification is absent, topology must not be silently carried forward past its TTL.

## 7. Object semantics

### Passage device

Required before Confirmed:

- generic object observation with Usable position;
- DD-specific identity mapping verified by live evidence;
- floor epoch match;
- independently verified activation-state source.

Object identity/position alone is insufficient. `IsTargetable` is retained as raw evidence but is not defined as passage activation by the public contract.

### Coffer

Required before Confirmed:

- public ObjectKind/BaseId and Usable position;
- live evidence that maps the identity to a DD coffer type;
- lifecycle/visibility validation within the epoch.

Contents and opened state remain Unknown unless a separate public observation is verified.

### Traps

Visible-object/action evidence may produce a revealed-trap Candidate. Hidden-trap completeness remains Unsupported/Unknown. Absence of a trap actor never means a room is trap-free.

## 8. Topology validation and BFS gate

The current `RoomPathfinder` expects exactly 25 nodes and validates reciprocal cardinal edges and grid boundaries. It can be called only if all conditions below pass:

1. the topology belongs to the current session and floor epoch;
2. all 25 room entries are present;
3. every connection flag is Confirmed and uses only North/South/West/East;
4. every connection is reciprocal and within the 5x5 grid;
5. current room and destination room are Confirmed;
6. no source conflict, stale evidence, sequence regression, or coordinate-system mismatch exists;
7. path invalidation is wired to any topology/epoch update.

If any condition fails, the topology adapter returns Unknown and does not call BFS. An empty BFS result means only “no route in the supplied valid graph”; it must not be used to conceal missing or invalid graph data.

Obstacles, enemies, traps, and danger are policy overlays, not connection discovery. Unknown trap/obstacle state prevents calling a route “safe” even if a topological route exists.

## 9. Source-neutral interface proposal

```csharp
public interface IEnvironmentObservationProvider : IDisposable
{
    EnvironmentObservationFrame Capture();
}

public interface IEnvironmentNormalizer
{
    EnvironmentSnapshot Normalize(EnvironmentObservationFrame frame);
}

public interface IValidatedRoomGraphAdapter
{
    TopologyConversion TryConvert(EnvironmentSnapshot snapshot);
}
```

The public-Dalamud provider may emit only generic objects and reset signals. A future static-game-data provider may emit candidate geometry. Neither can reach the strategy engine directly; normalization and topology validation are mandatory.

## 10. Failure behavior

| Failure | Environment result | Strategy effect |
| --- | --- | --- |
| Local player missing | pose Missing; current room Unknown | Stop |
| Object read throws | bounded diagnostic; affected collection Invalid/Unknown | Stop if required input |
| Non-finite or quarantined position | preserve evidence and reason; exclude from spatial calculations | no route/safety claim from object |
| Unknown BaseId/ObjectKind | semantic Unknown | ignore as destination, retain diagnostic evidence |
| Floor transition without new topology | old epoch Stale; topology Unknown | Stop |
| Partial/non-reciprocal graph | topology Invalid | do not call BFS; Stop |
| Static/live source conflict | Conflict | Stop and expose reason code |
| TTL elapsed | Stale | invalidate route; Stop |

## 11. Test design for later implementation

- model validation: missing/null/origin, finite ranges, duplicate identities, invalid timestamps;
- provenance: generic object Confirmed but semantic Candidate/Unknown;
- privacy: no names, chat text, player IDs, account data, or addresses;
- epoch reset: territory/content/map/instance, DD exit, verified floor transition, logout, time/sequence regression;
- stale TTL and partial-frame handling;
- EventObj/Treasure/Aetheryte mock inputs without semantic overpromotion;
- conflicting mappings and unsupported DDs;
- complete versus incomplete/non-reciprocal 25-room topology;
- BFS determinism and refusal before the topology gate;
- schema compatibility and bounded replay size;
- `DisabledExecutionGateway` remains unavailable.

No live observation in this phase establishes the room graph, so topology/BFS tests remain synthetic until a separately approved acquisition source is verified.
