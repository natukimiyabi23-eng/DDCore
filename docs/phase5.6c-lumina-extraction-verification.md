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

### Follow-up: public SqPack index surface (2026-11-16)

To avoid guessing additional `planmap.lgb`/collision paths, the offline probe gained `--public-api-surface`. It inspects **public** members of the installed Lumina assembly and summarizes the public repository/category metadata. This was run against the same local API 15 hook and SqPack; no live game process or non-public member was read.

| Public surface / observed value | Result | What it does not establish |
|---|---|---|
| `GameData.GetFile<T>(string)`, `FileExists(string)`, `GetFileMetadata(string)` | Present in the local assembly and documented by Lumina. Each requires a virtual path. | No method that lists every virtual file path was found in the inspected `GameData` public surface. |
| `GameData.Repositories` → `Repository.Categories` | Publicly exposes the loaded repository/category collections. The local `ex4` repository reports 12 categories and 134,220 `IndexHashTableEntries` in aggregate; the other expansion repositories were also summarized. | Repository/category IDs and index-entry counts are not path names or semantic asset labels. |
| `Category.IndexHashTableEntries` / `Index2HashTableEntries` | Public dictionaries keyed by 64-bit / 32-bit file hashes with index metadata. In this local snapshot the `ex4` categories showed 134,220 ordinary hash entries and no loaded index2 entries. | Hash keys cannot be reversed to recover arbitrary original paths. They do not identify which entry is EO topology, collision geometry, or a map file. |
| `Repository.FindIndexes(byte, int, int)` | Public method returns matching `.index`/`.index2` files for category/expansion/chunk. | It enumerates index containers, not their original virtual path strings. |
| Candidate-path lookup | `GameData.GetFileHash(string)` and `FileExists(string)` permit checking a path already known from a source/reference. | It cannot discover an unknown path without a candidate name. |

The public API reference describes `Repositories` as the available SqPack repositories and `FileExists` as checking a supplied path against indexes; `Category` exposes hash tables, while `Repository.FindIndexes` finds index files. See [Lumina GameData](https://lumina.xiv.dev/api/Lumina.Lumina.html), [Repository](https://lumina.xiv.dev/api/Lumina.Data.Repository.html), [Category](https://lumina.xiv.dev/api/Lumina.Data.Category.html), and [SqPackIndex](https://lumina.xiv.dev/api/Lumina.Data.SqPackIndex.html). Local reflection of the installed `GameData`/`Repository`/`Category`/index public surfaces agreed with those documented members. This means the checked public API supports **candidate verification**, not general path discovery; the finding does not claim there are no third-party path catalogs or other independent sources.

The API-surface JSON was identical across two executions (SHA-256 `3CBE046631F656C51F5FCC97B50DB46CBDA98601DA722668AC91998C72F71C46`). The actual `planmap.lgb` probe still exited 0 afterward. This does not change M0: static Room A/B candidates remain readable, but live layout, current room, open edges, collision topology and a complete BFS graph remain unknown. No hash-table brute-force or custom SqPack parser was added.

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

### Follow-up: TerritoryType map reference (2026-11-16)

The offline probe now also reports the generated `TerritoryType.Map` row reference for territory 1099. Against the installed game data marker `2026.11.03.0000.0000`, the value was **row 0** (no direct Map-sheet row reference). Repeated probe runs returned the same canonical summary SHA-256 `3F8BD88B7E901530BE08030429A059EFCA2D951350F2BFF01CFF72D041809151`, with territory background `ex4/07_lak_l5/cnt/l5c1/level/l5c1`, the verified `planmap.lgb` present, 19 layers, and 53 room-box candidates.

This closes only the ordinary `TerritoryType.Map` lookup path for this installed data. It does not establish that no separate map asset or runtime source exists, and it says nothing about dynamic floor state. No map image was extracted, no path was guessed, and no replay, plugin runtime, or game process was read.

### Follow-up: typed EO 5x5 room grids and planmap index crosswalk (2026-11-16)

The local Lumina Excel model exposes `DeepDungeonLayer`, `DeepDungeonMap5X`, and `DeepDungeonRoom`. Read-only iteration for EO `DeepDungeon.RowId = 3` found 10 `DeepDungeonLayer` rows (floor sets 0-9). Each layer's `RoomA` and `RoomB` references resolve to five subrows, each containing five `DeepDungeonRoom` row references. Six distinct A/B row pairs cover the ten floor sets: 37/38, 39/40, 41/42, 43/44, 45/46, and 47/48; the final four pairs are reused by adjacent floor sets.

All 20 floor-set/variant grids have the same nonzero-cell pattern in Lumina enumeration order:

```text
. # # # .
# # # # #
# # # # #
# # # # #
. # # # .
```

Each grid has 21 nonzero room references and 32 orthogonally neighboring occupied-cell pairs. These are **static room-position and possible-neighbor candidates only**; the sheets do not state that a neighboring passage is currently open or traversable. The row/column order has not been calibrated to a screen compass orientation.

The probe compared each nonzero row-major grid index (`row * 5 + column`) against the parsed `planmap.lgb` room-layer EventRange priority minus 100. The index sets matched exactly for all 20 A/B floor-set variants (zero invalid grids and zero mismatches). This is a cross-asset structural mapping: a player-pose match to a uniquely contained EventRange can yield a **candidate** 5x5 room index, following the same index convention used by pinned BossMod `Rooms.cs`. It does not prove the current variant, current room, door state, physical connectivity, or ground truth. The probe's `--deep-dungeon-grids` mode exits nonzero if the known planmap is absent, a grid is not 5x5, one of the ten floor sets is missing, or any index set differs.

### Follow-up: public Deep Dungeon sheet surface and Room C references (2026-11-16)

The standalone probe gained `--deep-dungeon-sheet-surface`, which reflects exported public properties/fields for types in `Lumina.Excel.Sheets` whose names contain `DeepDungeon`, plus the referenced `Level` row type. It reads type metadata only; it does not enumerate Excel rows in this mode. Against the installed hook, it found 18 matching public sheet types (Lumina assembly identity `7.0.0.0`). Two executions produced identical complete output SHA-256 `660EA6C221E76ABDAC69F5547CCECF9634C1E58F6ADC04C952B1FF6F659B6085`.

The relevant generated public shapes are:

| Sheet type | Public typed members observed | Limit |
|---|---|---|
| `DeepDungeonLayer` | `DeepDungeon`, `FloorSet`, and `RoomA`/`RoomB`/`RoomC` subrow references | A reference is static sheet linkage, not the active floor instance or its passage state. `Unknown0` remains opaque. |
| `DeepDungeonMap5X` | `DeepDungeonRoom` collection | Supplies the static cell-to-room reference grid already inspected above. |
| `DeepDungeonRoom` | `Level` collection | Links room rows to static `Level` rows; the type shape alone does not define connectivity or traversability. |
| `Level` | `Territory`, `Map`, `Type`, `X`, `Y`, `Z`, `Yaw`, `Radius`, plus object/event references | These are static level-record fields; no field is documented by this inspection as a live DD connection/open-door/walkability state. |

The `--deep-dungeon-grids` read-only data pass was extended to report each EO layer's `RoomC` row reference without treating it as a room grid. In the installed data marker `2026.11.03.0000.0000`, all ten EO floor-set rows (0-9) have `RoomC` row ID 0. The existing Room A/B crosswalk remained complete: 10 floor sets, planmap present, zero invalid grids, and zero priority/index mismatches. Two runs returned the same canonical summary hash `60EAC0302AD572CAD6870BAEC84CF4411DB766A4CBB031AECF5F32874B076ACC` (serialized output file SHA-256 `DFA37FBCEAAA08737939142A687705E6CB2084E183658A11293B72E622405C33`).

This narrows the installed EO static typed-grid data to the observed A/B variants; it does not prove that every dynamic layout is represented by those variants, identify which variant is active, locate the player's current room, reveal open passages, or establish walkability. No public typed live-topology contract was found in this bounded inspection. M0 remains **UNKNOWN**, and no live BFS input is justified.

### Follow-up: EO room-to-Level reference resolution (2026-11-16)

The offline `--deep-dungeon-room-levels` probe followed the 20 static Room A/B grids through their nonzero `DeepDungeonRoom` row references, then inspected the public `Level` collection on each distinct room row. It found 252 distinct referenced room rows, 1,260 Level references, and 589 distinct nonzero Level row IDs. In the installed Lumina Excel module, public `GetSheet<Level>("Level").GetRow(uint)` threw the public sheet's out-of-range lookup exception for all 589 IDs; none resolved to a Level row, so no territory/map/coordinate aggregate could be established from these references. The tool reports only counts and fixed interpretation text, not raw IDs or coordinates. Two runs were identical at SHA-256 `B71EB83290943762D2AD0988FC4E51CAFA4CBE48856F84B6381B42D9C3C4C95B`.

This is an **unresolved linkage**, not proof that the references are meaningless or that no alternate public lookup exists. It does not add evidence for EO territory mapping, current layout, current room, passage state, or walkability. A verified resolution rule for these references would be needed before using them as location data; M0 remains **UNKNOWN** and BFS stays disconnected.

#### Read-only replay overlay

The fixed export folder was searched again. The latest valid file by filesystem modification time and internal creation timestamp was `ddcore-export-20261108-143027-aa5cbe505b0848bfb5872ce9c0cf8566.replay.json`, schema 5, producer 0.5.3.0, 66 snapshots. Its stored `createdAtUtc` is `2026-11-08T23:29:45.946997+09:00` (equivalent instant `2026-11-08T14:29:45.946997Z`); exact floor is null, and the recorded floor range is 1-10.

All 66 player poses were finite. Using the existing candidate bounds convention and matching each unique EventRange priority to the floor-set-0 grid index yielded 50 unique geometric candidates, all from the Room B layer: index 12 appeared 22 times, index 17 13 times, and index 18 15 times. Three snapshots matched multiple distinct candidates; 13 matched none. The replay SHA-256 before and after was unchanged: `0EB0B3B6D175CEFAD552F3440D7482B0F624BDD8034D0AD077E86D161E45DB71`.

These are replay-to-static-geometry candidates, not verified room IDs or proof that Room B was the active layout: there is no independent room/template label, exact floor, or open-edge label in this replay. The result demonstrates that the data crosswalk can produce a compact candidate cell index while retaining ambiguous and unmatched observations as such; it does not unlock live BFS.

#### Cross-recording exploratory overlay (six schema-5 EO exports)

The six newest valid schema-5 exports whose first snapshot identifies EO and a floor band in the fixed directory were also read without modification. Other valid schema-5 DD exports are counted separately rather than classified as malformed. Selection is by internal `createdAtUtc` (filesystem modification time as tie-break); they total 869 snapshots and all 869 stored player positions are finite. Every selected recording identifies EO and the 1-10 band, while its exact floor remains null. Using the same uncalibrated inclusive Room A/B box bounds and the static row-major crosswalk, results were:

| Recording ordinal (UTC `createdAtUtc`) | Samples | Unique box candidates | Multiple boxes | No candidate | Candidate index counts |
|---|---:|---:|---:|---:|---|
| 1 (13:15:11.6933961Z) | 294 | 0 | 0 | 294 | none |
| 2 (13:18:00.5475772Z) | 245 | 0 | 0 | 245 | none |
| 3 (13:20:18.3950899Z) | 63 | 0 | 0 | 63 | none |
| 4 (13:44:33.9999913Z) | 96 | 93 | 0 | 3 | 2:18, 7:16, 8:27, 9:22, 13:10 |
| 5 (13:51:21.5841943Z) | 105 | 101 | 2 | 2 | 2:36, 7:14, 12:14, 13:21, 17:16 |
| 6 (14:29:45.946997Z) | 66 | 50 | 3 | 13 | 12:22, 17:13, 18:15 |

Among the three recordings with any unique box containment (267 samples), 244 were unique, 5 were multi-box, and 18 were outside all candidate boxes. In their ordered sample sequences, the 236 consecutive pairs with two unique classifications were either the same cell (230) or orthogonally neighboring indices (6); no non-neighbor jump was observed. This is only temporal consistency of uncalibrated geometric candidates and an abstract grid-index convention. It does not validate a traversable connection, compass orientation, active A/B template, room ground truth, or independent randomized layout labels. The three earlier recordings had finite poses but no containment in the candidate bounds, so they are evidence that this box/coordinate match is not a complete classifier.

All six selected recordings report only the same 1-10 floor band and have zero exact-floor observations. The three oldest, zero-containment recordings' projected player X/Z positions occupy the aggregate envelope X `[-310.51, -281.42]`, Z `[-319.02, -280.85]`; the three later recordings with some containment span X `[-305.12, 370.61]`, Z `[-438.55, 295.00]`. This shows the unmatched samples are spatially concentrated in a region outside the current candidate boxes, but cannot distinguish an unrepresented room/corridor, floor-specific geometry, or a coordinate/bounds mismatch. Exact floor and room labels are absent, so none of those explanations is confirmed.

#### Nearest candidate-box distance check

The parsed static candidates were also checked for a simple rotation omission: all 53 Room A/B candidates have `TriggerBoxShapeBox`, finite transforms, and rotation `(0,0,0)`, so nonzero room-box rotation does not explain this replay matcher discrepancy. The probe now reports the nearest distance from each unmatched player pose to any candidate axis-aligned X/Z box. These distances use the same uncalibrated `Translation +/- abs(Scale)` convention; median averages the middle pair when the sample count is even, and P90 uses nearest-rank `ceil(0.9*n)`. They are not hallway or walkability measurements:

| Recording ordinal (newest first) | Unmatched samples | Minimum | Median | P90 | Maximum |
|---:|---:|---:|---:|---:|---:|
| 1 | 13 | 136.70 | 430.64 | 430.64 | 430.64 |
| 2 | 2 | 137.29 | 137.29 | 137.29 | 137.29 |
| 3 | 3 | 136.85 | 136.85 | 136.85 | 136.85 |
| 4 | 63 | 440.78 | 441.32 | 445.08 | 445.08 |
| 5 | 245 | 417.98 | 428.99 | 434.98 | 445.36 |
| 6 | 294 | 418.80 | 430.97 | 438.33 | 445.39 |

The older unmatched group is consistently far from every current candidate box under this convention, so a small boundary overshoot is not a sufficient explanation for those samples. It remains unknown whether they reflect an absent floor/layout geometry, another coordinate relation, or another cause; this does not establish a transform or justify shifting the boxes.

The new offline collection mode reproduced this aggregate twice: six selected recordings, zero malformed schema-5 files, zero other-DD schema-5 files in the inspected directory, 869 samples, and canonical sanitized-output SHA-256 `3EC606D7AB84FB6DB1458410D62190003A69A601C3D8965CBE9FD1FCC52B58D5` on both runs. Sequence comparison resets across 10-floor-band changes instead of treating the old and new band as adjacent room evidence; these six recordings had no such boundary crossing. A deliberately nonexistent export directory was rejected with exit code 2. No raw replay was written, moved, or staged.

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

Added standalone tool [`tools/LuminaMapProbe`](../tools/LuminaMapProbe/Program.cs). It loads the caller-supplied local Lumina assemblies and SqPack root via reflection, obtains TerritoryType 1099, derives the candidate asset path by the pinned BossMod rule, and outputs a JSON summary of the TerritoryType Map row reference, layer IDs, object counts, EventRange priority/shape/transforms, and data hashes. Its `--public-api-surface` mode records public API/type shape and category hash-entry counts to distinguish known-path lookup from path enumeration. Its `--deep-dungeon-grids` mode extracts EO's static 5x5 sheet references and fails on grid shape, missing floorset, planmap, or room-index mismatch. Its `--replay-room-candidates <export-directory>` mode selects up to six newest schema-5 files whose first snapshot is identified as EO with a floor band; malformed schema-5 files and other-DD files are counted separately. It outputs ordinal/times, projected planar coordinate bounds, anonymous counts/index candidates, and nearest uncalibrated candidate-box distance summaries for unmatched poses (not filenames, session IDs, actor IDs, chat, or names), and summarizes adjacent-sample candidate-index transitions; it neither writes to source exports nor treats candidates as route input. Its new `--validate-room-labels <replay.json> <labels.json>` mode scores independent human labels against one replay and the extracted static boxes, with replay and `planmap.lgb` content hashes, strict labels schema, aggregate-only output, and explicit mismatch/ambiguity buckets. The label evaluator is synthetic-tested only; no user-labelled real replay was available, and this mode does not establish accuracy or route input. The tool does not depend on or modify DDCore runtime, write extracted game assets, or require an FF14 process.

Commands used:

```powershell
dotnet build tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release --no-restore
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56' `
  'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack'
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56' `
  'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack' `
  --public-api-surface
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56' `
  'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack' `
  --deep-dungeon-grids
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\addon\Hooks\15.0.4.0-80-gbd3a49a6c-estell.56' `
  'C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack' `
  --replay-room-candidates `
  'C:\Users\wasur\AppData\Roaming\XIVLauncher\pluginConfigs\DDCore\recordings\exports'
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

### Redacted per-snapshot box trace across local EO exports (2026-11-16)

The new offline `--replay-room-box-trace` was applied read-only to all six valid schema-5 EO exports in the fixed local export folder, ordered by their internal `createdAtUtc` values from 2026-11-08 22:15:11 to 23:29:45 +09:00. Their producer version was `0.5.3.0`; together they contain 869 snapshots, and none stores an exact floor. The 12-file folder also contains six other files outside this selected schema-5 EO set; their contents were not needed for this trace summary.

| Trace classification | Snapshots |
|---|---:|
| Unique static box candidate | 244 |
| Multiple distinct variant/cell candidates | 5 |
| No candidate box containment | 620 |
| **Total** | **869** |

These counts align with the earlier three-recording analysis: the three more recent files had 244 unique, 5 ambiguous, and 18 outside all boxes; the three older files had no containment in 602 samples. They remain geometric containment results under uncalibrated `Translation +/- abs(Scale)` bounds, not room identity. Exact-floor evidence, independent room labels, active A/B choice, open edges, and walkability remain absent. A repository scan found no user-authored label JSON or screenshot fixture; this does not assert that none exists elsewhere on the user's machine.

The first PowerShell aggregate wrapper mishandled status-count accumulation and converted the wrapper error into per-file failure counts; a separate single-file invocation confirmed the CLI path worked, and the corrected aggregate wrapper completed all six files with zero trace failures. No replay was changed. The trace status tally is 244/5/620, and no new ground truth was obtained. M0 remains UNKNOWN.

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

## Follow-up: pinned BossMod EO wall templates (2026-11-16)

At fixed BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`, the offline reference data [`Walls.json`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Walls.json) contains EO keys `3.1` through `3.10`. A read-only parse of that pinned file found, for each key, 25 `RoomsA` slots and 25 `RoomsB` slots; each variant has 28 nonzero directional `Depth` entries. This is a count of entries in BossMod's embedded static template, not a count of confirmed live barriers.

The consuming implementation explains the missing dynamic dependency. [`DeepDungeonState.cs` lines 8-15](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Data/DeepDungeonState.cs#L8-L15) assigns EO enum value 3. [`Rooms.cs` lines 18-35](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L18-L35) selects a floor-set key from dungeon ID and floor band, then chooses A or B by the current tileset (tileset 2 returns without selecting either). [`Rooms.cs` lines 45-53](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/Rooms.cs#L45-L53) suppresses a static wall candidate when the corresponding dynamic room connection flag is open; lines 148-155 use those connection flags for adjacency. [`AutoClear.cs` line 363](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Global/DeepDungeon/AutoClear.cs#L363) turns remaining wall records into temporary obstacle hints.

| Item | Confirmed from pinned source/data | Not established for DDCore live observation |
|---|---|---|
| EO static wall templates | 10 floor-set keys (`3.1`-`3.10`), with 25 A and 25 B room slots per key; 28 nonzero directional depth entries per A/B template | Which template is active in the user's current run |
| Open passage / room adjacency | BossMod combines selected template with `Palace.Rooms` connection flags | Public API access to current EO room flags or current tileset was not established here |
| Obstacle hints | BossMod emits remaining template walls as temporary rectangular obstacles | Validated collision shape, complete collision topology, or walkable/navmesh surface |
| BFS graph | Static candidates and dynamic connection flags are separate inputs in the reference implementation | A complete, current, reciprocal, ground-truthed graph and endpoints for DDCore |

This narrows the static-data question: a pinned reference contains per-floor-set A/B wall templates, but the live layout and edge-state inputs are separate. The template alone cannot prove a passage is open, a wall is currently present, or a location is traversable. No BossMod code/data was copied into DDCore, no runtime dependency was added, and no result was connected to BFS or strategy. **M0 remains UNKNOWN.** BossMod is BSD-3-Clause; this was source inspection and aggregate counting only, so no copied implementation or attribution change is needed.

### Exploratory floor-set 1 geometry comparison

For an offline consistency check only, the 28 nonzero directional wall positions in BossMod key `3.1` were compared with the same-index `planmap.lgb` room candidate (`priority = 100 + room index`). Candidate bounds use the probe's existing `Translation +/- abs(Scale)` convention, which has not been independently calibrated. For each point, distance was measured to the nearest edge of the matching orientation (horizontal for North/South, vertical for East/West), without treating the edge as a confirmed wall or passage.

| Wall template → box layer | Samples | Median edge distance | Samples ≤ 5 units | Samples ≤ 10 units |
|---|---:|---:|---:|---:|
| A → A, same index | 28 | 12.39 | 3 | 3 |
| B → B, same index | 28 | 12.57 | 3 | 4 |
| A → B, same index | 28 | 807.52 | 0 | 0 |
| B → A, same index | 28 | 809.84 | 0 | 0 |

This shows that the A and B candidate families in this static file occupy widely separated coordinate regions, while the same-letter wall/room pairing is only a weak geometric fit under the uncalibrated bounds metric. The close cross-layer comparison does **not** establish which layout is active, prove an individual wall-to-room mapping, verify collision geometry, or identify a live randomized floor. It was one floor-set only, used no ground-truth labels, and is not a route or BFS input.

## Next prerequisites

1. Obtain multiple user-labelled EO layouts with player positions in the same coordinate frame, plus independent labels of room identity, active A/B template, and open passages. Keep samples anonymized and outside the plugin runtime.
2. Determine whether any permitted/public source exposes active template and connection/reveal state. If not, keep those fields Unknown; do not substitute BossMod's native `InstanceContentDeepDungeon.MapData` input without separate review/approval.
3. Only after the above, validate box boundary conventions and room assignment on held-out samples, recover a complete reciprocal graph, then test BFS output offline. Pass criteria must include stale/ambiguous input rejection.

No M1 work, in-game feature, private/native access, automatic movement/combat, input injection, BFS connection, existing PR update, or merge was performed.
