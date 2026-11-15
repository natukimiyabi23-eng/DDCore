# Stage 5.6C — static map feasibility (EO B1)

## Conclusion

**Superseded by the follow-up extraction report:** `docs/phase5.6c-lumina-extraction-verification.md` records a successful, read-only Lumina extraction of the EO-associated `planmap.lgb` and 53 room-layer EventRange candidates. The earlier text below records the narrower prior pass only; its statement that extraction was not run is historical and no longer describes the latest Stage 5.6C result. The overall map gate remains **UNKNOWN** because current randomized layout, active A/B selection, current room, open connections, and validated BFS input remain unproven.

The prior M0 result remains **UNKNOWN**. No current room, connection graph, collision map, active random-layout identity, or complete BFS input was demonstrated. No code, fixture, replay access, game interaction, native read, or BFS connection was performed.

## Scope and method

Baseline: M0 commit `8d9d0a94b31f6fefbee70ca62a1de29db510d318`; dedicated branch `research/phase56c-static-map-feasibility`. Rechecked the M0, roadmap, Phase 5.6 research/model/implementation plan and Stage 5.6B live verification documents. Searched the DDCore checkout and the local XIVLauncher/game folders for `planmap.lgb`, `.lgb`, `.exd`, and `Lumina.dll`. Inspected the SqPack directory listing and existing BossMod source references. No game process was queried and no game data was modified.

## Local asset/runtime evidence

| Item | Observation | Meaning / limit |
|---|---|---|
| Game data | `C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack\ffxiv` exists and contains `*.win32.index`, `*.win32.index2`, and `*.win32.datN` archive files. | Game assets are locally archived. The directory listing does not identify the location/version of a particular `planmap.lgb`. |
| Loose `planmap.lgb` | No matching loose file found in the searched game SqPack tree, XIVLauncher tree, or DDCore checkout. | Does not mean the virtual asset is absent from SqPack archives. |
| Lumina | `Lumina.dll` exists under the local Dalamud API 15 hook directory (15.0.4.0-80-gbd3a49a6c-estell.56) and plugin directories. | Library presence only; no Lumina extraction was run, no API compatibility was tested, and no asset bytes were read. This folder version is not asserted to be DDCore's target build contract. |
| EO B1 asset identity/version | Not identified. | No asset hash, virtual path, map row, or patch/game-data version was verified. |
| Extracted fixture | None. | No fixture or generated data was committed. |

## What static geometry could and could not prove

The fixed BossMod source reports use of `planmap.lgb` / `DeepDungeonLayer` `EventRange` geometry and wall resources in `Rooms.cs`. This is a concrete candidate for static room-boundary/geometry data, not a DDCore extraction result. Static geometry alone cannot prove which randomized arrangement is active, which rooms are currently connected/revealed, current room membership, or dynamic obstacles. BossMod's current topology comes separately from `InstanceContentDeepDungeon` through `WorldStateGameSync.UpdateDeepDungeon`; that native source is patch-sensitive and is not a public Dalamud contract.

References at fixed BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`:

- [`Rooms.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L14-L163) — static room geometry processing.
- [`WorldStateGameSync.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Framework/WorldStateGameSync.cs#L941-L990) — native DD map/floor state ingestion.
- [`FloorPathfind.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/FloorPathfind.cs#L14-L60) — 25-room connectivity input to BFS.

BossMod is BSD-3-Clause licensed. Any future reuse of its code/data must preserve required notices/provenance; this stage copied nothing and adds no BossMod runtime dependency.

## Required facts and outcome

| Item | Outcome | Classification |
|---|---|---|
| Room candidate geometry | BossMod source indicates static `EventRange` use; not extracted locally. | **Unverified** |
| Room centers/bounds/coordinate transform | No EO B1 values or transform reproduced. | **Unverified** |
| Walls/corridors | BossMod source references wall resources; local wall data and semantics not extracted. | **Unverified** |
| Current player-to-room match | No labelled B1 map/room fixture used. | **Unverified** |
| Multiple random layouts | No layout corpus or active-layout key tested. | **Unverified** |
| Room connectivity | Static shape is not dynamic edge/reveal state; no allowed source established here. | **Unavailable in this pass** |
| Complete BFS input | `RoomPathfinder` requires a complete validated 25-node reciprocal graph and confirmed current room. Neither was produced. | **Unavailable** |

No required test was claimed successful. Static archive inspection is not a test of parsing, extraction, coordinate conversion, room classification, randomized-layout selection, or route correctness.

## Next evidence needed

1. Identify the exact virtual SqPack path and game-data version for EO `planmap.lgb` and related wall assets using a documented Lumina reader compatible with the local archive/API version. Record hashes and keep original assets out of Git.
2. Extract only an offline, sanitized fixture. Verify parsing deterministically, validate malformed/missing records are rejected, and independently reproduce coordinate transforms.
3. Obtain multiple user-labelled EO B1 layout/map samples with a matching coordinate frame. Test whether static candidates distinguish active layouts and classify current room without false-safe matches.
4. Separately identify an allowed source for dynamic room connections/reveal state. Static geometry alone is insufficient for BFS edges.
5. Only after all inputs are proven, test complete graph construction and expected BFS paths using held-out layouts. Until then, keep BFS disconnected from live map evidence.

If a documented reader or matching ground truth cannot be obtained, report the specific missing prerequisite and ask the user whether to provide sanitized map samples or stop this path. Do not download game assets from unapproved sources, read process memory, infer active layout from nearby objects, or mark the map gate PASS.
