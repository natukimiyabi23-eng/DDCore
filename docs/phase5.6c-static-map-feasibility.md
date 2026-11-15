# Stage 5.6C — static map feasibility (EO B1)

## Conclusion

**Superseded by the follow-up extraction report:** `docs/phase5.6c-lumina-extraction-verification.md` records a successful, read-only Lumina extraction of the EO-associated `planmap.lgb` and 53 room-layer EventRange candidates. The earlier text below records the narrower prior pass only; its statement that extraction was not run is historical and no longer describes the latest Stage 5.6C result. The overall map gate remains **UNKNOWN** because current randomized layout, active A/B selection, current room, open connections, and validated BFS input remain unproven.

The 2026-11-16 SGB follow-up in that report also parsed the one SGB referenced by 42 shared-group placements and found 8 `CollisionBox` records. Their geometry and metadata were recorded, but their semantic relationship to walls, passages, or navigable space is unproven; the result does not change the gate.

The prior M0 result remains **UNKNOWN**. The no-extraction/no-replay statements below describe that earlier pass only and are superseded where noted by `docs/phase5.6c-lumina-extraction-verification.md`. Neither pass demonstrated a current room, connection graph, collision map, active random-layout identity, or complete BFS input. No live game interaction, native read, or BFS connection was performed.

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
| Room candidate geometry | Follow-up Lumina report extracted 26 `LVD_Room_A` and 27 `LVD_Room_B` `EventRange` candidates from local EO-associated `planmap.lgb`. | **Real game asset parsed; candidate semantics only** |
| Room centers/bounds/coordinate transform | Finite LGB transforms are available; bounds use the pinned BossMod `Translation +/- abs(Scale)` convention, not independently calibrated in-game. | **Raw transforms verified; world-boundary interpretation unverified** |
| Walls/corridors | No verified topology or walkable geometry was extracted from the room layers. | **Unverified** |
| Current player-to-room match | One latest local replay produced 50 unique box containments, 3 ambiguous, 13 unmatched; no ground-truth room labels. | **Exploratory only; current room remains Unknown** |
| Multiple random layouts | No layout corpus or active-layout key tested. | **Unverified** |
| Room connectivity | Static shape is not dynamic edge/reveal state; no allowed source established here. | **Unavailable in this pass** |
| Complete BFS input | `RoomPathfinder` requires a complete validated 25-node reciprocal graph and confirmed current room. Neither was produced. | **Unavailable** |

No required test was claimed successful. Static archive inspection is not a test of parsing, extraction, coordinate conversion, room classification, randomized-layout selection, or route correctness.

## Next evidence needed

1. The virtual path, local Lumina read, real asset hash, and deterministic output were verified in the follow-up extraction report. The raw asset remains outside Git; extend format/failure tests only if a concrete gap is identified.
2. Obtain user-labelled EO B1 samples pairing floor, active A/B template, current room, pose, and visible/open connections; validate bounds on held-out samples. The existing 66-snapshot replay comparison is unlabeled and exploratory only.
3. Separately identify an allowed source for dynamic room connections/reveal state and traversability. Static geometry alone is insufficient for BFS edges.
4. Only after all inputs are proven, test complete graph construction and expected BFS paths using held-out layouts. Until then, keep BFS disconnected from live map evidence.

If a documented reader or matching ground truth cannot be obtained, report the specific missing prerequisite and ask the user whether to provide sanitized map samples or stop this path. Do not download game assets from unapproved sources, read process memory, infer active layout from nearby objects, or mark the map gate PASS.
