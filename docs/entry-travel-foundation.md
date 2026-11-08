# Deep Dungeon entry-travel foundation

## Scope and safety boundary

This checkpoint designs and tests travel planning without moving the character. It adds no vnavmesh or Lifestream subscriber, no input injection, no teleport call, no NPC interaction, and no operation-capable `IExecutionGateway`. `DisabledExecutionGateway` remains the only execution implementation. The UI exposes status and verified display coordinates but intentionally has no start button.

An operation-capable adapter requires separate user approval. It must still pass the existing execution-time safety gate immediately before each external side effect.

## Verified Eureka Orthos access facts

The official FFXIV play guide states:

- unlock quest contact: Koh Rabntah in Mor Dhona at displayed map position `X:21.8 Y:8.1`;
- requirements: level 81 combat job, completion of the Endwalker main scenario, and Palace of the Dead floor 50;
- entry/save-data NPC: Khatun in Mor Dhona at displayed map position `X:34.8 Y:19.2`;
- after unlocking the quest, Burnel at `X:21.8 Y:8.1` provides convenient access to the entrance;
- when entering as a party, all members must be in Mor Dhona.

Source: [official Eureka Orthos play guide](https://na.finalfantasyxiv.com/lodestone/playguide/contentsguide/deepdungeon3/), checked 2026-11-08.

These are **displayed map coordinates**, not raw world-space coordinates. DDCore does not feed them into a pathfinder. Territory IDs, aetheryte IDs, object data IDs, raw world positions and the map-to-world transform remain unverified and are deliberately absent from the executable model.

## Movement approaches investigated

| Approach | Confirmed surface | Assessment for DDCore |
| --- | --- | --- |
| Dalamud API 15 | `IObjectTable.LocalPlayer` exposes the current player object; DDCore already reads its position/rotation | Suitable for observation. No documented public character-navigation service was found in the reviewed API 15 service surface. This is not treated as proof that no future API can exist. |
| vnavmesh IPC | Fixed source commit `38d2a24c3306f24640e64efe13915a78d301e878`, `vnavmesh/IPCProvider.cs:17,21,41-43,53` registers readiness, pathfind, move, stop, running state and simple move endpoints | Best candidate for optional local pathing. Third-party runtime dependency, string IPC contract and plugin/game update compatibility require explicit capability/version checks and live tests. Not connected. |
| Lifestream IPC | Fixed source commit `b36320170faafc8315f7585edb45cfbcd34e7046`, `Lifestream/IPC/IPCProvider.cs:65,241` exposes busy state and a teleport operation | Candidate for optional inter-area teleport only. Destination/aetheryte identifiers and unlock state must be observed rather than guessed. Not connected. |
| Direct input/native movement | No stable public contract established | Rejected for this checkpoint. It has a high patch/safety risk and would bypass the intended execution boundary. |

Primary references:

- [Dalamud API 15 `IObjectTable`](https://dalamud.dev/api/api16/Dalamud.Plugin.Services/Interfaces/IObjectTable/)
- [Dalamud v15 migration and SDK baseline](https://dalamud.dev/versions/v15/)
- [vnavmesh IPC provider at the investigated commit](https://github.com/awgil/ffxiv_navmesh/blob/38d2a24c3306f24640e64efe13915a78d301e878/vnavmesh/IPCProvider.cs)
- [Lifestream IPC provider at the investigated commit](https://github.com/NightmareXIV/Lifestream/blob/b36320170faafc8315f7585edb45cfbcd34e7046/Lifestream/IPC/IPCProvider.cs)

## Implemented non-operating model

`EntryTravelPlanner` produces a deterministic high-level preview:

1. fail closed when current area, unlock state or required teleport availability is unknown;
2. from another area, require a known teleport route to Revenant's Toll;
3. navigate to Burnel;
4. use the unlocked shortcut to the Eight Sentinels area;
5. navigate to Khatun and verify arrival.

The preview is always `IsExecutable = false`. Palace of the Dead and Heaven-on-High exist only as future destination enum values and return an explicit unsupported result.

`EntryTravelProgressTracker` is pure C# state logic for a future adapter. It requires a fresh timestamped user authorization, stops immediately on emergency stop, blocks on missing position or an external failure, detects time regression and step disagreement, and stops when position progress remains below a configured threshold for the configured timeout. It performs no side effect itself.

## Required adapter sequence after separate approval

1. Add a public-observation resolver for territory, sub-area, unlock evidence and raw landmark positions. Unknown remains blocking.
2. Add a version/capability-checked optional teleport adapter. Never guess an aetheryte identifier.
3. Add a version/capability-checked optional path adapter. Every move must be stopped when the safety latch, observation freshness or path state becomes invalid.
4. Route every side effect through an operation-capable `IExecutionGateway` that revalidates safety immediately before the call.
5. Begin with a live no-op/capability diagnostic, then a short user-started local path in a controlled area. Only after that may the full entrance route be considered.

## Remaining technical risks

- The official map coordinates are not pathfinder coordinates.
- Mor Dhona sub-area detection and the Burnel transition outcome are not represented by current public observations.
- Unlock, teleport availability and party readiness do not have verified DDCore adapters.
- Third-party IPC contracts may change without a DDCore protocol handshake.
- Teleport, loading, navmesh build, mount/fly state, obstacles and user interruption need live timeout/recovery evidence.
- The current tests validate deterministic planning and progress-state behavior only; they do not validate game movement.

## Approval gate

This checkpoint stops before any operation-capable adapter. User approval, a fresh token/safety preflight and a separate implementation slice are required before DDCore may call teleport, path, movement, interaction or input APIs.
