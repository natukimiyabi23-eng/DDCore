# Phase 4.5 BossMod integration proof

## Scope and conclusion

BossMod evidence is pinned to commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. DDCore does not reference a BossMod assembly and this phase adds no game operation.

Two questions were tested separately:

1. **Can DDCore call BossMod?** Yes, when BossMod is loaded and its unversioned `BossMod.HasModuleByDataId(uint) -> bool` IPC function is registered.
2. **Can DDCore obtain the DD strategy data it needs?** No. None of the 37 fixed-commit IPC functions returns hazards, movement guidance, decisions, room/resource state or target priorities.

Therefore the prototype is a reachability diagnostic, not a DD data adapter. A successful call must never be interpreted as useful DD integration.

## Verified IPC surface

`BossMod/Services/IpcService.cs:15-23` defines the internal hosted service. Lines 29-258 register 37 functions. Lines 267-285 (and following overloads) prefix each name with `BossMod.` and register it through Dalamud `GetIpcProvider`.

| Group | Count | Representative pinned lines | DD strategy payload |
|---|---:|---|---|
| Module lookup | 1 | `IpcService.cs:31` | No; returns only whether an OID has a module |
| Configuration | 3 | `IpcService.cs:33-53` | No; includes mutating endpoints and is not used by DDCore |
| Rotation/action status | 6 | `IpcService.cs:55-68` | No; only booleans/module names, not queue entries or advice |
| AI status | 2 | `IpcService.cs:70-71` | No; only enablement booleans |
| Presets | 20 | `IpcService.cs:73-252` | No; mostly CRUD/activation and not used by DDCore |
| Obstacle maps | 5 | `IpcService.cs:254-258` | No DD state; several mutate/generate state and are not used |
| **Total** | **37** | | **0 useful DD strategy-data functions** |

Dalamud API 15 officially exposes `IDalamudPluginInterface.GetIpcSubscriber<T1, TRet>(string)`. Its call gate executes a registered function on the caller's thread and reports an unavailable publisher with `IpcNotReadyError`. DDCore uses only that public IPC mechanism. It does not inspect BossMod memory or private types.

Official reference: <https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IDalamudPluginInterface/>

## Required DD information

| Required information | Exists internally in pinned BossMod | Existing IPC result | Classification |
|---|---|---|---|
| AoE / danger regions | `AIHints` and module components; `AutoClear.cs:252-280` clears and adds several hazard families | Not exported | **BossMod change required** |
| Enemy mechanics / casts | World actors and boss components | Module-exists boolean only | **BossMod change required** |
| Safe position / recommended movement | `AIHints` goal/forbidden zones; `DeepDungeon.cs:73-77` adds movement goals | Not exported | **BossMod change required** |
| Recommended combat action | `ActionQueue`; `DeepDungeon.cs:91-95` pushes potion actions | Only queue-has-entries boolean | **BossMod change required** |
| DD floor / rooms / exit | `DeepDungeonState.cs:16-41` models progress, 25 rooms, party rooms and passage/return | Not exported | **BossMod change required** |
| Pomanders / resources | `DeepDungeonState.cs:18-37` models pomanders, chests and magicite | Not exported | **BossMod change required** |
| Enemy priority | `AIHints.cs:8-69` models priority and combat attributes | Not exported | **BossMod change required** |

Directly reflecting these internal objects would be a private implementation dependency and is rejected. Access through process memory or hooks would also violate this phase's stable-public-method condition.

## Prototype

The optional prototype has three layers:

- `IBossModIpcInvoker` contains no Dalamud or BossMod type.
- `DalamudBossModIpcInvoker` obtains `BossMod.HasModuleByDataId` as `uint -> bool` through public Dalamud IPC.
- `BossModIntegrationProbe` calls it once for Gancanagh OID `0x3D54`, verified in `D10Gancanagh.cs:3-7`.

Results are fail-closed:

- absent function: `Unavailable`;
- successful invocation, regardless of true/false return: `ReachableUnversioned`;
- type mismatch or invocation exception: `IncompatibleOrFaulted`;
- protocol version: always unknown because BossMod exposes no handshake/version function;
- DD strategy-data capability: always `Unavailable` because the fixed IPC contract has no such payload.

BossMod is optional. An unavailable endpoint does not introduce a BossMod assembly dependency; DDCore otherwise continues normally. The live invocation has not been performed in this phase.

## Compatibility evidence

The fixed IPC contract has no protocol identifier, capability negotiation, DTO schema version or compatibility endpoint. A later local BossMod descendant (`ede93f39b8a9c7ff14fc99f9676a96b19748b9b4`) moves/renames the provider to `BossMod/Framework/IPCProvider.cs` and retains 27 of the 37 names; 10 fixed-commit endpoints are absent. This is source evidence of change, not a claim about every released BossMod version.

DDCore can detect only a known endpoint being absent, present/callable, or incompatible/faulted. It **cannot** prove BossMod version compatibility. The UI deliberately says “version contract unavailable.”

## Changes BossMod would need for useful integration

A maintainable read-only contract would require coordinated BossMod work:

1. `BossMod.DD.ProtocolInfo() -> { protocolVersion, producerVersion, capabilities }`.
2. `BossMod.DD.Snapshot() -> versioned immutable DTO` with timestamp, sequence and explicit unknown/quality fields.
3. Normalized hazard DTOs rather than BossMod `AOEShape`, `Actor` or delegate instances.
4. Separate DTO sections for target priorities, movement/goal guidance and authoritative DD progress/rooms/passage/resources.
5. Stable identity/lifecycle rules and bounded payload size.
6. Cross-repository contract fixtures and compatibility tests before either side releases.

This creates dual-repository release coordination, schema evolution and backward-compatibility work. The cost is real but is not converted into an invented percentage or hour estimate.

## Integration versus independent migration

| Criterion | Integration with current fixed IPC | Integration after BossMod changes | Independent migration |
|---|---|---|---|
| Initial DDCore implementation | Small probe only | Smaller consumer, but requires producer API work | Larger DDCore model/adapter work |
| DD information available | None of the required seven categories | Potentially complete if the new contract is implemented | Limited by DDCore observation/import adapters |
| External dependency | Optional but functionally insufficient | Hard dependency on BossMod contract and release compatibility | No BossMod runtime dependency |
| Update risk | Endpoint is unversioned | Manageable only with versioning and fixtures | BossMod behavioral changes must be reviewed/migrated explicitly |
| Offline tests | Probe logic only | DTO contract fixtures permit good tests | Strong; Core stays pure C# |
| Maintenance ownership | Low but no value | Shared across two repositories | Single DDCore repository plus provenance tracking |

## Architecture recommendation

**Recommendation:** keep the independent migration architecture selected in Phase 4. The current BossMod IPC proves only that plugin-to-plugin calls are technically possible; it supplies none of the required DD strategy data. Retain the optional diagnostic probe solely to make this limitation visible.

Reconsider a BossMod integration architecture only after BossMod accepts and versions a read-only DD snapshot contract. Until then, using internal BossMod objects would trade short-term code avoidance for an untestable, release-fragile dependency.

## Unverified items

- No FF14/Dalamud/BossMod live invocation was performed.
- No statement is made about third-party BossMod forks or undocumented endpoints.
- No BossMod-side contract change was implemented or proposed upstream.
- Endpoint reachability does not prove that a specific encounter module works live.

