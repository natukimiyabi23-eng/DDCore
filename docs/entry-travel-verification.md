# Entry-travel foundation verification

Date: 2026-11-08 JST  
Branch: `feature/eo-entry-travel-foundation`  
Base: `phase5/strategy-engine` at `bc75ea1`

## Scope verified

- official Eureka Orthos access and displayed map-coordinate evidence;
- deterministic high-level planning from unknown, other-area, Revenant's Toll and Eight Sentinels contexts;
- fail-closed access/current-area/teleport handling;
- explicit-start authorization lifetime;
- emergency stop, missing/invalid position, time/step inconsistency and stall detection;
- Japanese preview/status UI with no operation control;
- preservation of `DisabledExecutionGateway` and absence of movement/teleport IPC dependencies.

## Local verification

| Check | Result |
| --- | --- |
| `dotnet restore DDCore.slnx --locked-mode` | Passed |
| `dotnet build DDCore.slnx -c Release --no-restore` | Passed, 0 warnings, 0 errors |
| `dotnet test DDCore.slnx -c Release --no-build --no-restore` | Passed, Core 200/200 and Observation 43/43 (243 total) |
| `dotnet format DDCore.slnx --verify-no-changes --no-restore` | Passed |
| `dotnet list DDCore.slnx package --vulnerable --include-transitive` | No vulnerable package reported for any project |
| Project/dependency audit | Core has no project references; plugin references only Core and Observation; no vnavmesh, Lifestream or FFXIVClientStructs package/project reference |
| New-source operation-token scan | No IPC subscriber, teleport, move, input, hook, scanner or client-struct operation token in the three new Core source files |
| CI trigger coverage | Added `phase5/strategy-engine` as a pull-request base so stacked PR #8 receives the existing Windows build/test job |

The broad text scan found only two expected strings: the new Japanese UI says vnavmesh/Lifestream are not connected, and the pre-existing BossMod diagnostic uses a read-only public IPC subscriber. Neither is a movement dependency or operation call.

## Artifact

- plugin version: `0.5.2.0`;
- package: `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`;
- package SHA-256: `102F4F229401EFC6F1444E8DA681D07409D638A3FA7FAA99B5ABDE9670E481E0`.

No deployment was performed because this checkpoint is not intended for live character operation.

## Not verified

- live Dalamud load and rendering of the new panel;
- territory/sub-area or unlock detection;
- displayed-map-coordinate to world-coordinate conversion;
- aetheryte, NPC or object identifiers;
- Lifestream/vnavmesh presence, compatibility or behavior;
- teleport, navigation, obstacle recovery, area transition or arrival in FF14;
- emergency-stop latency against a real operation adapter.

These items must not be described as working. The branch stops before the operation adapter and requires explicit user approval for the next slice.
