# Phase 3.6 live observation adapter

## Scope

Phase 3.6 adds a read-only Dalamud API 15 adapter and connects its normalized output to the existing Phase 3 analyzer registry. It does not move the player, change targets, execute actions, use items, inject input, install hooks, or read private/native client structures.

The implementation was compiled and tested against the locally installed `Dalamud.dll` version `15.0.3.9` and its XML documentation. The public API surface was also checked against the current official API 15 documentation. No live FFXIV or Gancanagh encounter test was performed.

## Dependency boundary

```text
Dalamud public services
  -> DDCore.Plugin/Observation/DalamudObservationSource
  -> DDCore.Observation/RawObservationFrame
  -> LiveWorldSnapshotProvider
  -> DDCore.Core/WorldSnapshot
  -> AnalyzerRegistry -> GancanaghAnalyzer
  -> read-only Japanese diagnostics
```

`DDCore.Core` and `DDCore.Observation` have no Dalamud reference. Only `DDCore.Plugin` references `Dalamud.NET.Sdk/15.0.0`. The normalized observation assembly is independently unit-testable.

## Verified public APIs

| Service/type | Fields used | Classification | Official evidence |
|---|---|---|---|
| `IFramework` | `Update`, `LastUpdateUTC` | directly observed; throttled to at most 10 Hz | <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IFramework/> |
| `IClientState` | `IsLoggedIn`, `TerritoryType` | directly observed | <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/> |
| `IDutyState` | `IsDutyStarted`, `ContentFinderCondition.RowId` | directly observed when a current row exists | <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDutyState/> |
| `IObjectTable` | local player and object enumeration | directly observed | <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IObjectTable/> |
| `IGameObject` | identity, base ID, table index, kind, position, rotation, hitbox, death, target and validity | directly observed | <https://dalamud.dev/api/Dalamud.Game.ClientState.Objects.Types/Interfaces/IGameObject/> |
| `ICharacter` | current/max HP and combat status flags | directly observed | <https://dalamud.dev/api/Dalamud.Game.ClientState.Objects.Types/> |
| `IBattleChara` | status list, cast state, action ID, target object ID, current/total cast time | directly observed | <https://dalamud.dev/api/Dalamud.Game.ClientState.Objects.Types/Interfaces/IBattleChara/> |
| `IStatus` / `StatusList` | status ID, source ID and remaining time | directly observed | <https://dalamud.dev/api/Dalamud.Game.ClientState.Statuses/Classes/StatusList/> |

The implementation does not call `IGameObject.Address`, `IObjectTable.Address`, `GetObjectAddress`, `IGameInteropProvider`, `ISigScanner`, FFXIVClientStructs, or any target/input service.

## Direct, derived and unavailable values

### Directly available

- Player presence and position.
- Battle-NPC entity/game-object IDs, BossMod-compatible `BaseId`, table index, position, actor rotation, hitbox, HP, death/validity, combat flag and current target.
- Active cast action ID, target object ID, current cast progress and total cast time.
- Status ID, source entity ID and remaining duration.
- Login, territory, duty-start state and current ContentFinderCondition row when supplied by Dalamud.

### Derived or estimated

- Core X/Y uses game world X/Z. Actor and cast rotation converts from FFXIV clockwise-from-positive-Z to Core counter-clockwise-from-positive-X.
- Cast start and finish are derived from capture time minus `CurrentCastTime`, plus `TotalCastTime`. A continuing cast retains its first derived timestamps to prevent frame-to-frame jitter.
- A disappeared active cast is classified `Completed` only at its predicted finish (100 ms tolerance); an earlier stop is classified `Interrupted`.
- Cast target location is derived from the observed target actor position only when `CastTargetObjectId` resolves. Otherwise it remains null.
- Cast-specific rotation is not public, so actor rotation is used as an explicitly estimated value.
- Eureka Orthos floor 10 is inferred only while Gancanagh or its mine (`BaseId` `0x3D54`/`0x3D55`) is observed. The snapshot is marked `Partial`, its content key says `estimated`, and diagnostics record the inference.
- Generation starts at zero and increments when an entity ID returns after absence or its game-object fingerprint changes. Territory/content/login transitions create a new session and reset generation/cast state.

### Not available through the verified public API

- Authoritative Deep Dungeon kind and current floor.
- Cast ground-target location when no target object resolves.
- Cast-specific facing/rotation distinct from actor rotation.
- An explicit status stack-count property; `IStatus.Param` is not treated as a stack count.
- A public event that distinguishes server-confirmed cast completion from interruption. DDCore derives this from observed progress and time and labels the observation partial.

No zero/default placeholder is inserted for unavailable territory, content, floor, target, target location, status source, status stack count or expiry. Nullable values and reason codes are used instead.

## Fail-closed analysis

Partial snapshots are passed to analyzers so the UI can show known hazard candidates, but registry output is forced to `UnknownOrUnsupported`. Therefore a predicted Gancanagh circle can be displayed diagnostically without asserting the unobserved space is safe. Stale, unsupported or invalid snapshots are not analyzed. Exceptions become invalid diagnostics and do not stop the plugin.

The existing `DisabledExecutionGateway` remains the only execution gateway. No Phase 2 safety control was relaxed.

## Lifecycle and reset behavior

- Capture sequence increases within one session.
- Logout/login, territory change or current content-row change starts a new session at sequence zero.
- Capture-time regression yields a stale snapshot with no actor payload.
- Missing actors are absent rather than represented by zero-filled records.
- Reused entity IDs receive a new generation.
- Framework subscription is removed and its tick source is disposed with the plugin.

## Diagnostics UI

The Japanese main window shows observation state, DD kind/floor, enemy and active-cast counts, detected AoE count, fail-closed safety state, last observation time, and bounded reason-code details. Unsupported content is never labeled supported.

## Verification coverage

The mock tests cover conversion, X/Z and rotation conversion, target-derived cast location, status expiry/source, cast start/interruption/completion and same-action restart, generation reuse, actor disappearance, territory/session reset, missing player data, logout, stale time, unidentified duty, analyzer integration, exception containment and disposal/unsubscription.

Live loading, public wrapper behavior in a running game, actual EO territory/content identifiers, floor inference accuracy, cast location/rotation accuracy and recorded Gancanagh comparisons remain unverified.
