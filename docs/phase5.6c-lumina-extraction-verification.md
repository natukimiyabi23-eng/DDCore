# Stage 5.6C — Lumina EO static-map extraction verification

Date: 2026-11-16
Baseline: `85b8993c54de84a32a43b8e97ecd0bf296caf258`
Scope: local game-data read only; no game process, plugin runtime, or gameplay operation.

## Conclusion

**The local Lumina reader successfully loaded and parsed an actual EO-related `planmap.lgb` from the installed SqPack archive.** The TerritoryType row 1099 resolves to `bg/ex4/07_lak_l5/cnt/l5c1/level/planmap.lgb`. It contains 19 layers, including two static room layers (`LVD_Room_A` and `LVD_Room_B`) with 26 and 27 `EventRange` boxes respectively. Each has 21 distinct room priorities; all 53 candidate transforms are finite.

This proves that static room-box candidates can be read from the local asset. It does **not** identify which A/B template is selected in a particular run, the current randomized room layout, actual open connections, current room, walkable navmesh, or a complete safe BFS graph. M0 therefore remains **UNKNOWN**, not PASS.

## Local reader and game-data version

| Item | Observed value |
|---|---|
| Reader files | `Lumina.dll` and `Lumina.Excel.dll` under the installed Dalamud API 15 hook directory |
| Hook directory | `C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56` |
| Lumina file version | `7.7.0.0` |
| Lumina product version | `7.7.0+b92c0cbdb65c9e53b0ec5faafa83b2cc32aa9a7c` |
| Loaded assembly identity | `7.0.0.0` (distinct from the file/product version above) |
| Local game-data marker | `ffxivgame.ver`: `2026.11.03.0000.0000` |
| SqPack root passed to Lumina | `C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack` |

The locally reflected public surface included `GameData(string, LuminaOptions)`, `FileExists(string)`, `GetFile<T>(string)`, `GetFileMetadata(string)`, and `GetExcelSheet<T>(Language?, string)`. `Lumina.Data.Files.LgbFile` exposes `Layers`, `ChunkHeader`, `Header`, and raw parsed `Data`. The public Lumina guide documents the same SqPack-root constructor and `GetFile<LgbFile>` reading pattern: [Lumina basic usage](https://lumina.xiv.dev/docs/guides/basic-usage.html), [LgbFile API](https://lumina.xiv.dev/api/Lumina.Data.Files.LgbFile.html), [GameData API](https://lumina.xiv.dev/api/Lumina.Lumina.html).

The documented API describes reading game files; it does not promise that a static LGB contains the live randomized floor state. The local version above is an observed development-hook build, not a claim that all future Dalamud API 15 builds use the same Lumina version.

## Asset identification and extraction

The probe read `TerritoryType` row 1099 from the installed Excel data. Its `Bg` field is:

```text
ex4/07_lak_l5/cnt/l5c1/level/l5c1
```

At pinned BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`, [`Rooms.cs` lines 73-83](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L73-L83) reads that row's `Bg`, removes the final path component, prefixes `bg/`, and appends `planmap.lgb`. Applying that exact rule produced the candidate path below, which was then verified against SqPack with Lumina's `FileExists` and successfully loaded with `GetFile<LgbFile>`:

```text
bg/ex4/07_lak_l5/cnt/l5c1/level/planmap.lgb
```

Lumina parsed 13,098 bytes of file data across 19 layers. SHA-256 of the parsed `LgbFile.Data` bytes (not a hash of the compressed SqPack `.dat` archive) is:

```text
1B4148C41AD911AB7B056FE2F08E2794E22FA12C10BEC1ED2E281A9FFC672497
```

The installed `DeepDungeon` sheet row 3 was named `Eureka Orthos`. `DeepDungeonLayer` row 31 maps dungeon row 3 / floor set 0 to RoomA row 37 and RoomB row 38; rows 31-40 cover floor sets 0-9. This supports that the TerritoryType asset and A/B templates are associated with EO's layer data. The static sheet association does not identify the live A/B choice or a particular random floor instance. Territory 1099's association with EO is additionally consistent with the supplied prior live observation; the exact floor number was not inferred from this static asset.

### Extracted structure

| Layer | Objects | EventRange candidates | Distinct priorities | Shape / transform checks |
|---|---:|---:|---:|---|
| `LVD_Room_A` (`LayerId 269380`) | 26 | 26 | 21 | All boxes; all 26 positions/scales/rotations finite; zero nonzero rotations |
| `LVD_Room_B` (`LayerId 269381`) | 27 | 27 | 21 | All boxes; all 27 positions/scales/rotations finite; zero nonzero rotations |
| `LVD_Boss_00` | 4 | 2 | not treated as room nodes | Also contains 2 `PopRange` records |
| Other layers | 46 | 0 | n/a | Includes 42 `SharedGroup` objects in `LVD_zone_A/B` and 4 records in `LVD_Boss_00` |
| **Total** | **99** | **55** | — | Asset type counts: 55 `EventRange`, 2 `PopRange`, 42 `SharedGroup` |

Across the 53 records in the two room layers, X/Z centers range from `-426.207` to `-159.1526` in A and `163.9951` to `435.7297` in B; Z centers range from `168.358` to `437.8789` in A and `-441.4097` to `-171.9926` in B. The probe also reports candidate X/Z bounds using `Translation ± abs(Scale)`, matching the static box convention used by pinned BossMod `Rooms.cs` lines 133-140. This agreement is source-backed but is not an independent in-game calibration. No coordinate-axis transformation beyond reading the LGB `Translation`, `Scale`, and `Rotation` was applied.

## Offline probe and checks

Added standalone tool [`tools/LuminaMapProbe`](../tools/LuminaMapProbe/Program.cs). It loads the caller-supplied local Lumina assemblies and SqPack root via reflection, obtains TerritoryType 1099, derives the candidate asset path by the pinned BossMod rule, and outputs a JSON summary of layer IDs, object counts, EventRange priority/shape/transforms, and data hashes. It does not depend on or modify DDCore runtime, write extracted game assets, or require an FF14 process.

Commands used:

```powershell
dotnet build tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release --no-restore
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56' `
  'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack'
```

Observed checks:

- Release build: passed, 0 warnings, 0 errors.
- Same invocation executed twice: both exited 0 and produced byte-identical JSON; asset hash `1B4148C41AD911AB7B056FE2F08E2794E22FA12C10BEC1ED2E281A9FFC672497` and canonical-summary hash `50F110D350F8217ED3A463AFE456E4110E92E659B3B88B04D1A2190CDAFA6501` matched.
- All 53 Room A/B transforms had finite components; all were box-shaped and had zero rotation components.
- A deliberately nonexistent virtual asset path was rejected (`FileExists=false`, tool exit code 1); no synthetic map was substituted.
- No full malformed-LGB corpus, coordinate-to-screen/world calibration, player-room ground truth, or route/BFS test was run. These remain untested, rather than passed.
- The raw LGB asset and extracted full JSON were not committed. Only the small reflection-based probe and this report are included.

### Exploratory player-coordinate containment check (2026-11-16)

The latest local `*.replay.json` was read in place, without modification, before comparing its already-recorded player pose to the extracted A/B candidate bounds:

| Field | Value |
|---|---|
| File | `ddcore-export-20261108-143027-aa5cbe505b0848bfb5872ce9c0cf8566.replay.json` |
| `createdAtUtc` | `2026-11-08T14:29:45.946997+00:00` |
| Schema / producer | 5 / 0.5.3.0 |
| Snapshots / direct finite player poses | 66 / 66 |
| DD context | `eurekaOrthos` 66/66; floor range 1-10 66/66; exact floor unknown 66/66 |
| Replay SHA-256 before and after | `0EB0B3B6D175CEFAD552F3440D7482B0F624BDD8034D0AD077E86D161E45DB71` |

The offline comparison mapped snapshot `position.x` to LGB X and `position.y` to LGB Z, consistent with `LiveWorldSnapshotProvider.ConvertPlayerPose` using `RawPosition.X` and `.Z`. It then tested inclusive containment in each Room A/B box calculated by the existing probe as `Translation +/- abs(Scale)`. Counts were **50 snapshots in exactly one candidate box, all in `LVD_Room_B`; 3 snapshots in multiple boxes; and 13 snapshots in no candidate box**. No player identity or raw export was emitted by the comparison.

This is only a reproducible geometric candidate lookup over one replay and one static asset. The replay has no exact-floor label, no independently labelled room IDs, no known active A/B choice, and no open-edge labels. The bounds convention also lacks independent in-game calibration. Therefore the 50 unique matches are not verified current-room classifications; the 3 ambiguous and 13 unmatched samples demonstrate that a candidate-only implementation must preserve ambiguity/no-match rather than force a room. No runtime feature or BFS connection was added, and the replay hash is unchanged.

## Static data versus dynamic floor state

### Follow-up: referenced shared SGB and collision-box records (2026-11-16)

The standalone probe was extended to inspect the `SharedGroup` references exposed by Lumina's parsed `LgbFile` and the parsed `SgbFile` layer objects. This remains an offline, read-only experiment; no asset bytes or full extraction output are committed.

| Observation | Result |
|---|---|
| `planmap.lgb` SharedGroup records | 42 total: 21 in each of `LVD_zone_A` and `LVD_zone_B`; each record references the same SGB path below. |
| Referenced SGB | `bgcommon/world/lvd/shared/for_vfx/sgvf_w_lvd_b0945.sgb`; locally present and parsed by Lumina; 5,219 parsed data bytes; parsed-data SHA-256 `1C994B121673A1F60204F9DC42E0E61BD866090ADFC67CE0A740BE29724B47DD`. |
| SGB structure exposed by Lumina | 1 layer group, 1 layer, 11 instance objects: 8 `CollisionBox`, 2 `Sound`, 1 `VFX`. |
| CollisionBox fields | All 8 expose a box trigger shape, enabled parent data, transform, attributes, push-out value, and `CollisionAssetPath` string `9`. The boxes are thin and clustered near the SGB-local origin. |
| Collision-related local Lumina.Excel row models | Reflection found `CollisionIdPallet`, `SnipeCollision`, and `WKSZoneFloorCollision`. The generated `CollisionIdPallet` model exposes only row metadata plus `Unknown0..2` (`UInt16`), but no named path column. Candidate row 9 can be read and has values `264`, `265`, `282`; these fields are opaque, and this does not prove that the `CollisionAssetPath` string `"9"` refers to that row. |

These are parsed static records, not proof that the boxes are walls, room boundaries, passage geometry, collision meshes, or walkable-space data. In particular, the shared asset path contains `for_vfx`, but its name alone does not prove the semantic purpose of every contained object. The local generated `CollisionIdPallet` row model does not explain the numeric-looking `CollisionAssetPath` value, so no `.pcb` mesh was resolved from it. No collision topology or navigation surface was generated. Accordingly, this follow-up does not change the existing room/topology table or M0 verdict.

An independent public tool, [TerritoryViewer](https://github.com/Enceladeum/TerritoryViewer), demonstrates a possible wider LGB/SGB/PCB extraction approach. Its own source comments describe custom SGB parsing by reverse-engineered offsets, and its README cautions that composed transforms are not exhaustively validated. It is evidence that further offline investigation may be possible, not validation of an official Lumina contract, DDCore's topology, or a safe-to-reuse parser. No code or dependency was copied from it.

The probe now emits local collision-sheet type/API metadata and the candidate sheet row 9, without assigning meanings to opaque fields or asserting a link from the SGB value. Two complete outputs had identical JSON SHA-256 `0B21FE17A724431EE54C13572FD166DC62CFE1AFA5CB17945B8B12D5E570C121`. A deliberately nonexistent virtual path returned `exists=false` and exit code 1. The full-solution Release build and all 360 existing tests passed; `dotnet format --verify-no-changes` and NuGet vulnerability checks passed. Both CI runs on the current code/evidence head `4e4027a1ac8b7cc6a0106b5d3984d85e103007cc` and the final continuation update head `bd466e3f2811d1dec79c96449e1164dade4896ae` succeeded; the latter runs were `43741875615` and `43741882400`.

| Question | Result | Evidence status |
|---|---|---|
| Can the room candidate geometry be loaded? | Yes: 26 A / 27 B EventRange boxes; 21 priorities per variant. | **Verified from installed real game data** |
| Are centers and stored box scales available? | Yes, as LGB transforms; finite for all 53 candidates. | **Verified as raw fields; boundary semantics not independently calibrated** |
| Are walls/corridors/walkable cells available in these room layers? | No wall, corridor graph, collision mesh, or walkable polygon was extracted from these EventRange records. Other object types exist in other layers but were not interpreted as topology. | **Not established by this asset probe** |
| Can the current room be identified? | One replay's player poses can be geometrically contained in candidate boxes (50 unique, 3 multiple, 13 none), but no exact-floor, active-template, or room ground-truth labels validate those matches. | **Unknown** |
| Can the selected randomized layout be identified? | No. The file contains A/B candidate layers; static Excel links both but no live selection marker was established. | **Unknown** |
| Can room edges be built? | No. The asset records do not provide verified current open-connection/reveal state. | **Unknown** |
| Is complete BFS input available? | No verified active 25-node graph, reciprocal edges, and current endpoint were produced. | **No; keep BFS disconnected** |

The pinned BossMod source makes the distinction explicit: `Rooms.cs` lines 90-120 parses static boxes, while lines 142-157 uses `Palace.Rooms` flags for current room adjacency. `AutoClear.cs` lines 14-53 selects wall templates using dungeon, floor set, current `Tileset`, room flags, and a separate `Walls.json` resource. These additional inputs were not obtained from the public static LGB. The local pinned checkout is at `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`; BossMod is BSD-3-Clause, but this probe copies no BossMod code or data and adds no runtime dependency.

## M0 gate impact

**M0 remains UNKNOWN.** This pass resolves the previously unperformed static asset extraction and demonstrates reproducible room-box candidate parsing. It does not satisfy M0 requirements for multiple labelled layouts, live A/B selection, current-room ground truth, dynamic edges, verified obstacles, or complete BFS input. It is not a PASS and does not justify connecting static candidates to the strategy engine.

## Next prerequisites

1. Obtain multiple user-labelled EO layouts with player positions in the same coordinate frame, plus independent labels of room identity, active A/B template, and open passages. Keep samples anonymized and outside the plugin runtime.
2. Determine whether any permitted/public source exposes active template and connection/reveal state. If not, keep those fields Unknown; do not substitute BossMod's native `InstanceContentDeepDungeon.MapData` input without separate review/approval.
3. Only after the above, validate box boundary conventions and room assignment on held-out samples, recover a complete reciprocal graph, then test BFS output offline. Pass criteria must include stale/ambiguous input rejection.

No M1 work, in-game feature, private/native access, automatic movement/combat, input injection, BFS connection, existing PR update, or merge was performed.
