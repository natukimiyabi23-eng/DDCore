# Phase 5.5A verification

## Result

Phase 5.5A adds only the approved read-only observation foundation. It does not add native game-structure access, BossMod runtime integration, gameplay input, Phase 5.5B or Phase 6 behavior.

The verified local package is DDCore `0.5.1.0` for Dalamud API 15. Live FF14/Dalamud behavior has not been verified.

## Implemented boundary

- `ObservationMetadata` records value source, quality and a required reason for missing, unsupported, stale or invalid values.
- `AnonymousPlayerPose` stores nullable position and rotation without a player name, account, entity ID or object ID.
- `DalamudObservationSource` reads `IObjectTable.LocalPlayer.Position` and `Rotation` through public API 15 surfaces only.
- `LiveWorldSnapshotProvider` preserves a valid origin `(0,0)` and represents absence as `null` with a reason.
- replay/world schema 4 writes the new field; schema 3 remains readable without fabricating a pose.
- `SnapshotPrivacyFilter` removes Player/PartyMember actor identities and references while retaining the identifier-free pose.
- `WorldSnapshotStrategyAdapter` exposes the pose as evidence only. It does not infer rooms or make an incomplete state actionable.
- `DisabledExecutionGateway` remains unchanged.

## Mandatory test coverage

| Requirement | Automated evidence |
|---|---|
| Missing coordinate versus origin | `WorldSnapshotTests`, `LiveWorldSnapshotProviderTests`, `WorldSnapshotStrategyAdapterTests` |
| Public API acquisition failure | `LiveWorldSnapshotProviderTests` covers missing/read-failed/non-finite raw observations |
| Player not created / content exit | provider and strategy-adapter tests preserve missing/None and stop safely |
| Old JSON loading | `ReplayTests` reads schema 3 without `playerPose` |
| Unknown stops safely | `WorldSnapshotStrategyAdapterTests` keeps required fields unavailable |
| No personal identity persisted | `ObservationRecordingTests` removes Player/PartyMember actor IDs and keeps only anonymous pose |

Final local result:

- Core tests: 180 passed, 0 failed, 0 skipped.
- Observation tests: 43 passed, 0 failed, 0 skipped.
- Total: 223 passed, 0 failed, 0 skipped.

## Legacy export verification

The Phase 5.5A codec decoded both original local schema 3 exports in read-only mode. Neither file was modified.

| Snapshots | Player pose values | SHA-256 |
|---:|---:|---|
| 288 | 0 | `D2A6DBAD273EF46285759C9FCE43A630F014294B19D169F12937D20B4BD938E6` |
| 124 | 0 | `ED9809FF3066874C10A79150CBE75541F301C95674AC335DF1FE080057D0683D` |

The absence of a pose is expected because schema 3 did not contain that property. This result proves decode compatibility, not live observation correctness.

## Build and audits

- locked restore: passed;
- Release build: passed with 0 warnings and 0 errors;
- format verification: passed;
- NuGet direct/transitive vulnerability audit: no known vulnerabilities reported in all five projects;
- project-reference audit: Core has no project references, Observation references Core only, Plugin references Core and Observation;
- prohibited-operation diff audit: no new `FFXIVClientStructs`, targeting, action, teleport, navmesh, hook, key-state or input-injection use;
- BossMod runtime dependency: none.

Package:

- path: `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`;
- manifest: `AssemblyVersion=0.5.1.0`, `DalamudApiLevel=15`;
- SHA-256: `1A9EE37D336C91F031809FA277375C7A23EE48E8D0EC10DE2CACCC79218C76C8`;
- contents include Core, Observation, Plugin, manifest/deps, `THIRD-PARTY-NOTICES.md` and both provenance manifests.

## Live verification still required

The user must perform the steps in [Phase 5.5A observation foundation](phase5.5a-observation-foundation.md#live-verification-procedure). In particular, live verification must establish that API 15 supplies the expected pose in login, DD entry, duty exit and logout transitions. No live result is claimed here.

## Residual limits

- DD kind, floor and floor epoch acquisition are not implemented.
- Current room, room graph, passage, traps and resources remain Unknown.
- A pose cannot yet be mapped to a room without separately verified floor geometry.
- Phase 5.5B and Phase 6 are not ready and require explicit approval.
