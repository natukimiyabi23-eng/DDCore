# Progress

## 2026-11-16 — EO B1 read-only integration status and data collection guide

- Added `docs/eo-b1-readonly-integration.md` to distinguish plugin runtime observation, offline static map parsing, synthetic BFS, and the still-missing live graph. Added `docs/eo-b1-live-test-guide.md` describing privacy-preserving user labels for exact floor, layout family, room index, and visible passage state; it explicitly notes that current label validation does not validate edges.
- Linked both documents from README. No plugin runtime code or game data was changed in this documentation slice; real-time map recognition and route preview remain unavailable.

## 2026-11-16 — EO static wall-template crosswalk across floor sets

- Added an offline `--deep-dungeon-wall-crosswalk <Walls.json>` LuminaMapProbe mode and pure geometry metric tests. It compares every pinned BossMod EO wall template (`3.1`-`3.10`) with matching-index Room A/B planmap candidates; it does not copy BossMod code/data or affect plugin runtime.
- Rechecked the fixed replay-export folder read-only: the six newest valid schema-5 EO recordings remain the documented set (producer `0.5.3.0`, 869 snapshots, 869 finite player poses, zero exact-floor observations). The probe again produced the existing sanitized aggregate SHA-256 `3EC606D7AB84FB6DB1458410D62190003A69A601C3D8965CBE9FD1FCC52B58D5`; no replay originals were changed or committed.
- Inspected source was pinned BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`; `Walls.json` SHA-256 `D33C758C19753043CCD830B28D25719877659C13ED0EEE852E6693DD4E7F4D75`; local `planmap.lgb` parsed-data SHA-256 `1B4148C41AD911AB7B056FE2F08E2794E22FA12C10BEC1ED2E281A9FFC672497`.
- The 20 templates yielded 560 nonzero directional points. The planmap contained 53 candidate shapes over 42 layer/cell keys, with 10 keys having multiple shapes. Across 280 samples per pairing, median nearest orientation-matched infinite-line distance was A→A 11.21, A→B 571.17, B→A 565.86, B→B 10.47; cross-pairings had no values within 10 units. This uncalibrated metric ignores segment extents and cannot establish wall truth or room identity.
- Two executions matched at summary SHA-256 `143768D54AA60197D2CC3A2CE903CF86DC3EB30CB4B0E0282B45F0F066C486D7`. M0 remains UNKNOWN; dynamic layout, current room, edges and walkability are unavailable, so live BFS remains disconnected.
- Committed as `08464f8c37e527273962dc491bfb941a732c4377` to the existing PR #22 branch; CI run `43765809122` passed restore, Release build, all tests, offline probe build, and package artifact storage. PR remains Open/Draft/unmerged.

## 2026-11-16 — EO room-to-Level static reference probe

- Added `--deep-dungeon-room-levels` to the offline Lumina probe. It follows nonzero Room A/B grid references from EO's ten static floor-set rows into distinct `DeepDungeonRoom` rows and their public `Level` references, emitting counts only.
- Local data contained 20 variant grids, 252 distinct referenced room rows, 1,260 Level links, and 589 distinct nonzero Level row IDs. Public `GetSheet<Level>("Level").GetRow(uint)` failed with the sheet's out-of-range lookup for all 589 IDs; zero resolved Level rows, so no territory/map/coordinate summary was available. Two executions matched at SHA-256 `B71EB83290943762D2AD0988FC4E51CAFA4CBE48856F84B6381B42D9C3C4C95B`.
- This is an unresolved static linkage, not evidence that no alternate documented resolver exists. It does not establish dynamic layout, current room, edges, or walkability; M0 remains UNKNOWN and BFS is not connected. No plugin runtime or game interaction changed.

## 2026-11-16 — Lumina public DD sheet surface / EO Room C check

- Added `--deep-dungeon-sheet-surface` to the offline Lumina probe. It reports public property/field signatures for 17 `DeepDungeon*` sheet types and `Level` (18 total), without iterating game-data rows in this mode. The local loaded Lumina assembly identity was `7.0.0.0`; two runs matched at SHA-256 `660EA6C221E76ABDAC69F5547CCECF9634C1E58F6ADC04C952B1FF6F659B6085`.
- The reflected static model includes `DeepDungeonLayer.RoomA/RoomB/RoomC`, 5x5 room-grid references, a `DeepDungeonRoom.Level` collection, and static `Level` fields such as territory/map references and X/Y/Z/yaw/radius. None of these public type signatures documents current randomized-layout selection, open passage state, or walkability.
- Extended the EO grid probe to report RoomC reference row IDs. In local game data marker `2026.11.03.0000.0000`, all ten EO floor sets have RoomC row ID 0. Room A/B grid crosswalk remains 10/10 floor sets, planmap present, zero invalid grids and zero mismatches; repeated canonical summaries matched at SHA-256 `60EAC0302AD572CAD6870BAEC84CF4411DB766A4CBB031AECF5F32874B076ACC`.
- This is additional static-data evidence only. Current dynamic layout, exact room, open edges, traversability, and complete safe BFS inputs remain unknown; M0 stays UNKNOWN. No plugin runtime or game operation was changed.

## 2026-11-16 — Six-export EO box-trace recheck

- Revalidated the fixed local replay folder: 12 replay exports total; six valid schema-5 EO exports (producer 0.5.3.0) contain 869 snapshots; none has an exact-floor value. A read-only, redacted trace completed all six with zero failures.
- Counts: 244 unique static box candidates, 5 ambiguous distinct variant/cell candidates, and 620 without box containment. This aligns with the prior split: the later three had 244/5/18; the older three had 602 no-containment samples. No independent user-authored room labels or screenshot fixtures exist inside the repository; no claim is made about other machine locations.
- This is a consistency check of uncalibrated geometry, not map accuracy or traversability. M0 remains UNKNOWN. Updated `docs/phase5.6c-lumina-extraction-verification.md` and `CONTINUATION.md`; no source or game data changed in this verification.

## 2026-11-16 — Redacted replay-to-static-box trace

- Added a standalone offline `--replay-room-box-trace` mode to help manually correlate replay snapshot ordinals with independently inspected evidence. It reports only sample index, EO floor band, fail-closed context/pose status, and static candidate box variant/index; it omits coordinates, exact floor, timestamps, source filenames, session/object IDs, chat, and names.
- Actual static data repeats multiple box shapes for some variant/cell pairs; the trace collapses identical candidate keys and counts ambiguity only across distinct variant/cell candidates. Empty/invalid candidate sets fail closed.
- The mode labels results as box containment under the current uncalibrated bounds, not room identity or active layout. It does not generate labels, topology, walkability, or BFS inputs.
- Against the existing schema-5 EO replay with 66 snapshots, it returned 50 unique containments, 3 ambiguous containments, and 13 without containment. Two runs yielded identical JSON SHA-256 `71E792F412F50C82CA51E75D1D47FD72AABE25FACE407526085734A6DB527D2F`; prohibited pose/identity/chat/coordinate field scan was empty. Replay original remained read-only.
- Verification: probe tests 13/13; solution Release build 0 warnings/errors; full suite 373/373 (Core 197, Observation 163, probe 13); format check passed; NuGet vulnerability audit reported no vulnerable packages in all six solution projects. The final code/evidence commit `508622bdf781253b199e0c6c246d7ec644efe9e4` passed GitHub Actions run `43761251847` (push); PR run `43760996169` covered the preceding docs checkpoint.
- Branch remains `research/phase56c-sharedgroup-inspection` on top of PR #22, now pushed with a clean worktree; no runtime plugin changes, deployment, game operation, BFS connection, or merge. PR #22 remains Open/Draft/unmerged.
- M0 remains UNKNOWN: these counts reproduce geometric matching only. Independent user-authored floor/room labels and active connection/walkability evidence are still absent.

## 2026-11-16 — Offline user-label evaluation path

- Added `--validate-room-labels` to the standalone Lumina probe and a small evaluator that compares one schema-5 replay's finite EO player positions with user-authored exact-floor / Room A-B / row-major cell labels. Labels are bound to exact replay bytes by SHA-256; duplicate samples, unsupported schema/evidence, absent static cells, floor-band conflicts, malformed context, and extra properties are rejected or excluded rather than coerced.
- Output is aggregate only: matched expected candidate, ambiguous, other candidate, unmatched, invalid pose, and context conflict counts. It does not emit the replay path, sample coordinates/timestamps, per-sample labels, or identifiers. The command is local/offline; no replay label file or screenshot is included in the repository.
- Added `docs/room-label-validation.md` with strict schema, run instructions, evidence rules, and interpretation limits. No human-labeled replay was available, so the evaluator has only synthetic unit coverage and has not produced a real-map accuracy result.
- Added 8 synthetic tests covering exact match, overlapping ambiguity, wrong candidate vs no candidate, floor conflict, replay/map hash mismatches, duplicate indices, missing pose, valid versioned JSON, and rejection of unknown label fields. All prior tests remain included.
- The existing EO candidate and nearest-distance results remain uncalibrated; M0 remains UNKNOWN, and the evaluator does not produce live room IDs, edges, a BFS graph, or route input.
- Final checks for this slice: Release solution build passed with 0 warnings/errors; all 368 tests passed (197 Core, 163 Observation, 8 label-evaluator); probe Release build, format verification, NuGet vulnerability audit (none across six projects), and `git diff --check` passed. CI on `0236997c67f5806d8bce6c6240821b389e477853` passed both build-and-test jobs (runs `43756839243`, `43756847048`). No user-labeled real replay was available, so label accuracy remains unverified. No runtime plugin or game interaction was added.
- Updated existing Draft PR #22 only. Branch `research/phase56c-sharedgroup-inspection`; code/evidence commit `0236997c67f5806d8bce6c6240821b389e477853` passed both build-and-test jobs (runs `43756839243`, `43756847048`). Documentation checkpoint `88715af11627ace6689cced622503844400ced65` passed CI run `43757054014`. PR remains Open/Draft/unmerged.

## 2026-11-16 — EO typed room-grid / planmap crosswalk

- Extended the standalone offline Lumina probe to enumerate all ten EO `DeepDungeonLayer` floor sets and their Room A/B 5×5 `DeepDungeonRoom` grids, then compare occupied row-major cell indices with `planmap.lgb` Room A/B `EventRange` priority minus 100.
- Installed game data contains all ten floor sets. All 20 floor-set/variant grids share the same 21-cell pattern and match their planmap index set exactly; each grid also has 32 orthogonal neighboring occupied-cell pairs. These are static cell positions/potential neighbors only, not confirmed open passages, active randomized layout, or traversability.
- Re-read the latest valid fixed-folder replay (`ddcore-export-20261108-143027-aa5cbe505b0848bfb5872ce9c0cf8566.replay.json`: schema 5, producer 0.5.3.0, 66 snapshots, `createdAtUtc` 2026-11-08 23:29:45.946997 +09:00, exact floor null, range 1-10). Read-only geometric overlay: 50 unique Room B candidates (cell 12: 22 samples, 17: 13, 18: 15), 3 ambiguous, 13 unmatched. The replay SHA-256 remained `0EB0B3B6D175CEFAD552F3440D7482B0F624BDD8034D0AD077E86D161E45DB71`.
- Expanded the read-only overlay to the six latest schema-5 EO exports (869 samples; all poses finite). Three later recordings had 244 unique box candidates, 5 ambiguous and 18 unmatched; three earlier recordings had no box containment for any of 602 samples. Across the 236 consecutively classified pairs from the later three, 230 stayed in the same candidate cell and 6 changed to an orthogonal grid-neighbor index; there were no non-neighbor index jumps. This is geometric/index consistency only, not route or open-passage evidence, and the zero-containment recordings show the current box matcher is incomplete.
- Added a repeatable anonymous CLI mode `--replay-room-candidates` to the offline Lumina probe. It selects the six newest schema-5 EO exports by internal timestamp, counts geometric candidate and sequence-transition outcomes without emitting session/actor/chat/name fields, resets sequence comparisons across floor-band boundaries, and reports aggregate projected X/Z bounds plus distances from unmatched poses to candidate boxes. Two actual runs agreed at sanitized-summary SHA-256 `3EC606D7AB84FB6DB1458410D62190003A69A601C3D8965CBE9FD1FCC52B58D5`; the inspected folder had no malformed or other-DD schema-5 files, and a missing export directory was rejected with exit code 2.
- All six selected recordings carry only the 1-10 floor band, with no exact-floor labels. The oldest three zero-containment recordings have a much narrower projected-coordinate envelope than the later three with some matches. This narrows the mismatch to a spatially localized subset but does not identify its cause; no room or geometry hypothesis is confirmed.
- All 53 static Room A/B candidates parsed as finite, unrotated boxes. In the same uncalibrated AABB convention, the 602 unmatched poses in the three oldest records had nearest-box distances of 417.98-445.39 coordinate units (record medians 428.99-441.32). This rules out a small boundary overshoot under that convention only; it does not identify why those samples are outside the candidate set.
- Nearest-box refinement verification: solution Release build and probe Release build passed with 0 warnings/errors; all 360 tests passed (Core 197, Observation 163); `dotnet format DDCore.slnx --verify-no-changes --no-restore`, NuGet vulnerability audit, and `git diff --check` passed. Two probe runs matched at sanitized-summary SHA-256 `3EC606D7AB84FB6DB1458410D62190003A69A601C3D8965CBE9FD1FCC52B58D5`.
- No active variant, exact current room, connection state, or full trusted live BFS input is established. Cross-recording pose-to-box matching is inconsistent. M0 stays UNKNOWN and live BFS remains disconnected. No plugin runtime, game, raw replay, or game asset was modified or committed.
- Verification for the typed-grid slice: solution Release build succeeded with 0 warnings/errors; Core 197/197 and Observation 163/163 tests passed; probe Release build succeeded with 0 warnings/errors; `dotnet format --verify-no-changes`, NuGet audit, and `git diff --check` passed. Two grid-probe executions had identical canonical SHA-256 `160AC3FFA6EC9149ED23F96973796FCEDBEF15C82F4A5C8D522D3E5B7774A8E3`.
- Replay-tool and coordinate-envelope slice local verification: solution Release build succeeded with 0 warnings/errors, all 360 tests passed, probe Release build succeeded with 0 warnings/errors, format verification and NuGet audit passed. Two replay analysis executions had identical canonical sanitized-summary SHA-256 `D440CC2F6247A07E61E9E65D636FF6E3F003FB3DF7C624DB4394EB29D41BA995`; `git diff --check` passed.
- Follow-up branch: `research/phase56c-sharedgroup-inspection`, stacked on PR #21; existing Draft PR #22 updated only. Evidence commit `4a92bf146ecb726eff02b3945f7c4b2131e68e2d` passed CI run `43754693339`; the follow-on continuation checkpoint records the final verification state. PR #22 remains Draft/Open/unmerged.

## 2026-11-16 — Dalamud public addon lifecycle map-data boundary

- Read the locally installed Dalamud API 15 `15.0.4.0+bd3a49a6c012b6735fd5dc07dbc9221120db1a06` XML contract and official docs for `IGameGui` and `IAddonLifecycle`. `GetAddonByName` returns an `AtkUnitBasePtr`; `ScreenToWorld` raycasts one supplied point. Lifecycle args may expose setup `AtkValue` arrays or requested-update number/string-array data.
- No inspected contract identifies a DD map addon or defines array/node fields as room IDs, open passages, or traversability. This is a public event/interop surface, not a typed map-data API. Treat it as a candidate for a bounded opt-in UI-correlated experiment; decoding undocumented game-native layout would need separate approval and was not done here.
- M0 remains UNKNOWN, BFS stays disconnected, and no runtime code, event subscription, game interaction, or UI-memory read was added. See `docs/phase5.6-environment-research.md`.

## 2026-11-16 — Exploratory EO wall-template / room-box geometry check

- Compared the 28 nonzero directional wall positions for BossMod EO key `3.1` with same-index Room A/B boxes from the locally parsed `planmap.lgb`, using the probe's uncalibrated `Translation +/- abs(Scale)` bounds and orientation-compatible nearest-edge distance.
- Same-letter medians were A→A 12.39 and B→B 12.57 coordinate units (3/28 and 3/28 samples within 5 units); cross-letter medians were A→B 807.52 and B→A 809.84 (0/28 within 10). This supports only that the static A/B coordinate families are widely separated; it does not validate individual room/wall correspondence, active random layout, or geometry semantics.
- One floorset, no ground truth, no code/asset committed. M0 remains UNKNOWN; no map or edge data is eligible for BFS.
- Documentation/evidence commit `75ed3665e32bd7b14cb42a2a5805490eee05b8c8` passed CI run `43745937797`; the worktree was clean after push.
- Extended `LuminaMapProbe` to report EO TerritoryType 1099's direct `Map` row reference. Installed game data returned row 0; two runs still loaded the known planmap and returned the same canonical summary hash `3F8BD88B7E901530BE08030429A059EFCA2D951350F2BFF01CFF72D041809151`. This rules out only a direct TerritoryType.Map-sheet link in this data, not alternate/static assets or dynamic map APIs. Release tool build passed with 0 warnings/errors.
- Commit `4a494e737cc41cc0edce372f75d2e1ede3e890d0` passed PR CI run `43746937448` (build, tests, offline Lumina probe, and package steps).

## 2026-11-16 — Pinned BossMod EO static wall-template inspection

- Re-read BossMod at fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. Its `Walls.json` contains EO keys `3.1`-`3.10`; each has 25 Room A and 25 Room B slots, with 28 nonzero directional depth entries per variant.
- `Rooms.cs` selects the template using dungeon/floor-set and the current tileset, then suppresses a candidate wall when a dynamic `RoomFlags.Connection*` says the passage is open. The same flags define adjacency; `AutoClear.cs` emits remaining walls as temporary obstacle hints.
- This confirms static template data exists in the reference implementation, not that DDCore's permitted live sources expose the active template, connection state, collision topology, or walkability. M0 remains UNKNOWN; no code/data was copied, no runtime dependency or BFS link was added, and no gameplay operation occurred.
- Documentation-only commit `135822b8ca0457798022fb28aca0f108f0dc0c66` passed PR #22 CI run `43744587438`; `git diff --check` passed and the worktree was clean after push.
- Details and fixed-source line references: `docs/phase5.6c-lumina-extraction-verification.md`.

## 2026-11-16 — Lumina public index-surface verification

- Extended the standalone `tools/LuminaMapProbe` with `--public-api-surface`, which reflects only public members from the installed Lumina assembly and summarizes repository/category index counts without emitting file paths or raw asset data.
- Confirmed local Lumina API identity 7.0.0.0 (file 7.7.0.0, product `7.7.0+b92c0cbdb65c9e53b0ec5faafa83b2cc32aa9a7c`) and game data marker `2026.11.03.0000.0000`. `GameData` exposes path-based `GetFile`, `FileExists`, and metadata calls; no all-virtual-path enumeration method appeared in the inspected local `GameData` public surface or its official API docs.
- Public `Repository.FindIndexes(category, expansion, chunk)` returns index-file paths, while public `Category.IndexHashTableEntries`/`Index2HashTableEntries` expose numeric hash-keyed records, not original virtual names. Local `ex4` reported 12 categories and 134,220 ordinary hash entries; these counts cannot semantically identify map/PCB assets or reverse arbitrary paths.
- The public API surface output repeated byte-identically (SHA-256 `3CBE046631F656C51F5FCC97B50DB46CBDA98601DA722668AC91998C72F71C46`); the actual EO planmap probe still exited 0. This narrows discovery to known-path verification or externally sourced candidate lists; it does not reveal a current random layout, room graph, or walkability. M0 remains UNKNOWN and live BFS remains disconnected.
- Local verification: locked restore and Release solution build passed (0 warnings/errors); all 360 tests passed (Core 197, Observation 163); `dotnet format --verify-no-changes` passed; NuGet vulnerability audit reported none for all five solution projects. The API-surface JSON matched across two runs; the actual planmap remained readable (19 layers / 53 room candidates), and a deliberately missing virtual path was rejected with `exists=false` / exit 1. GitHub Actions run `43743456333` passed at commit `7ba167728d697c80dc3172b03c47a74f39d50f4e`.
- No game process/UI interaction, internal memory access, archive reverse-engineering, raw asset export, or gameplay feature was added. M0 remains UNKNOWN and the live BFS stays disconnected.

## 2026-11-16 — Static shared-SGB inspection

- Extended the standalone `tools/LuminaMapProbe` to follow the 42 `SharedGroup` records in EO `planmap.lgb` through Lumina's parsed `SgbFile` API. All 42 placements reference `bgcommon/world/lvd/shared/for_vfx/sgvf_w_lvd_b0945.sgb` (21 under each zone layer).
- The actual local SGB parsed as 1 layer group / 1 layer / 11 objects: 8 `CollisionBox`, 2 `Sound`, 1 `VFX`. The 8 collision boxes expose transform, box trigger shape, attributes and `CollisionAssetPath="9"`; no verified mapping or wall/passage/navmesh meaning was established.
- Inspected the local Lumina.Excel collision-related row models. `CollisionIdPallet` has only row metadata and opaque `Unknown0..2` fields in this generated version; it did not resolve the `"9"` reference or establish a PCB path.
- Candidate sheet row 9 was readable through Lumina and contained `Unknown0..2 = 264, 265, 282`; these opaque values do not prove the SGB string `"9"` is a row reference or identify a path.
- The probe emits the reflected Excel sheet API/type shape and row values without assigning opaque semantics. Two full outputs matched SHA-256 `0B21FE17A724431EE54C13572FD166DC62CFE1AFA5CB17945B8B12D5E570C121`; a missing virtual asset was rejected with exit code 1.
- Full Release build passed with 0 warnings/errors; all 360 tests passed; format and NuGet audits passed. Both CI runs passed at code/evidence commit `4e4027a1ac8b7cc6a0106b5d3984d85e103007cc` (`43741684190`, `43741692034`) and at continuation head `bd466e3f2811d1dec79c96449e1164dade4896ae` (`43741875615`, `43741882400`).
- No raw game asset or generated JSON was added to Git. No runtime plugin, private API/memory access, BFS link, game operation, or automation was added. M0 remains UNKNOWN; active randomized layout, room/edge truth, collision mesh and walkable space remain unproven. See `docs/phase5.6c-lumina-extraction-verification.md`.

## 2026-11-16 — Exploratory EO static-box / replay-pose comparison

- Re-read the latest local replay export in place: `ddcore-export-20261108-143027-aa5cbe505b0848bfb5872ce9c0cf8566.replay.json`, `createdAtUtc` 2026-11-08T14:29:45.946997+00:00, schema 5, producer 0.5.3.0, 66 snapshots. All 66 snapshots had a finite direct player pose, EO context, floor band 1-10, and unknown exact floor. SHA-256 before/after: `0EB0B3B6D175CEFAD552F3440D7482B0F624BDD8034D0AD077E86D161E45DB71`.
- Compared each pose to all 53 candidate boxes from the locally parsed `planmap.lgb`, using the probe's `Translation +/- abs(Scale)` bounds and the source-confirmed X/Z-to-2D mapping. Result: 50 unique containments (all Room B), 3 ambiguous samples, 13 with no matching box.
- These counts are exploratory only: no active Room A/B label, exact floor, or current-room ground truth exists for this replay; box boundary calibration is unverified. No current room, random layout, edges, or BFS input is established. Live BFS remains disconnected.
- Updated `docs/phase5.6c-lumina-extraction-verification.md`, corrected the stale historical summary in `docs/phase5.6c-static-map-feasibility.md`, and aligned `docs/autonomous-development-roadmap.md` with the extracted asset and the exploratory matching limits. No code or test files changed; `git diff --check` passed and the documentation diff was reviewed. Commit `9e3457afa5649ea4fe90d3d3089a175c12d37c09` is on Draft PR #21; CI `build-and-test` succeeded at this head (run `43737354682`).

## 2026-11-16 — Stage 5.6C Lumina static map extraction

- Used the installed Lumina 7.7.0 file build (`b92c0cb...`) and local game data marker `2026.11.03.0000.0000` to read TerritoryType 1099 and derive `bg/ex4/07_lak_l5/cnt/l5c1/level/planmap.lgb` using the exact path rule in pinned BossMod `Rooms.cs`.
- Verified that the virtual asset exists in local SqPack and parses as an LGB: 19 layers, 55 EventRange records, 42 SharedGroup records, and 2 PopRange records. `LVD_Room_A/B` contain 26/27 box candidates with 21 distinct priorities each; all 53 transforms are finite.
- Added an isolated, reflection-based offline probe at `tools/LuminaMapProbe`. Release build is clean; two runs emitted identical JSON and asset/summary hashes; an absent virtual path is rejected.
- Recorded the decoded LGB data hash and limitations in `docs/phase5.6c-lumina-extraction-verification.md`. Raw game asset and extracted JSON were not added to Git.
- Static room-box extraction is now verified, but active A/B selection, randomized layout identity, current room, edges/walls/walkability, labelled multiple layouts, and complete BFS input remain unknown. M0 remains UNKNOWN; no live strategy/BFS connection or gameplay operation was added.

## 2026-11-11 — Phase 5.6 environment-recognition research and design

- Rechecked the installed Dalamud API 15.0.3.9 public contracts at product commit `17cd86dfacbc1016c6e5aac0d49833b774ce037c` for object, client, duty, framework, game-data and game-GUI observations.
- Confirmed public generic player/object identity, kind, targetability, position and rotation, but found no public typed contract for the current DD room graph, room index, passage progress, collision/navmesh, structured floor map or hidden traps.
- Rechecked BossMod fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`: its topology uses native `InstanceContentDeepDungeon.MapData`, room geometry uses static Lumina `planmap.lgb`, and hidden-trap support can use Palantir IPC. These sources are not treated as public DDCore observations.
- Read all six schema-5 originals without modification: 869 snapshots, 10,225 Enemy observations, 8,214 usable coordinates, 2,011 quarantined coordinates and 869 anonymous player poses. They contain no EventObject/Treasure, room, passage, wall or exit fields.
- Designed a source/quality/epoch-aware environment model, strict floor-transition invalidation, and a topology gate that refuses BFS unless a fresh complete reciprocal 25-room graph and endpoints are Confirmed.
- Proposed a public-object diagnostics slice first, followed by separately approved semantic mapping, static-geometry feasibility, topology-source decision and read-only BFS integration stages.
- Added the documentation branch to the existing CI push allowlist because the workflow did not report checks for the stacked Phase 5.6 PR; no job steps, permissions or implementation behavior changed.
- Documentation only. No implementation code, replay original, private/native access, gameplay operation, execution gateway, existing PR or merge changed.

## 2026-11-11 — Phase 5.5D EO floor 1-to-2 live verification

- Inspected the original schema-2 diagnostic read-only: producer 0.5.4.2, three entries, no retained chat text or identity fields, SHA-256 `12969A9D4BA6B4D57959F5742CAD52A4C641D8B52432A10778D698E89E748378`.
- Confirmed for this single EO floor-1-to-floor-2 transition: row 7248 with zero parameters, row 7270 with integer index 0/value 2 and consistent candidate 2, then ChatMessage kind 57 with consistent candidate 2.
- Recorded the user's independent visible floor-2/chat confirmation and no-error result separately from the JSON facts.
- Kept every other EO floor/band, Palace of the Dead, Heaven-on-High, row 9218, other languages, ordering generalization, reload/re-entry and future-version behavior explicitly unverified.
- Documentation only: the original JSON was not modified or committed; no code, strategy integration, execution behavior, merge, or next-phase work changed.

## 2026-11-11 — Phase 5.5D live callback failure correction

- Recorded the user-reproduced EO floor-transition failure separately from unverified row/parameter semantics: zero events/bytes and `chat-floor.log-callback-failed:InvalidOperationException` on 0.5.4.1.
- Identified the deterministic pre-record cause as `ImmutableArray.Builder.MoveToImmutable()` with unequal Count/Capacity, not a confirmed Dalamud event-ID or parameter change.
- Replaced it with bounded immutable copying, added sparse/empty parameter regression coverage, and split callback failures into fixed privacy-safe stage reason codes.
- Kept diagnostics initial-OFF and read-only; no chat text/identity, exact-floor strategy integration, native access, gameplay operation, merge, or next-phase work was added.
- Release 0.5.4.2 builds with zero warnings/errors; Core 197/197 and Observation 141/141 tests pass (338 total), with format and vulnerability checks passing. Live retest remains required.

## 2026-11-10 — Phase 5.5D offline hardening

- Rechecked the installed/public Dalamud API 15 `LogMessage` and `ChatMessage` contracts and dispatch implementation at pinned commit `3e47d4f`; no event ID, parameter mapping, cross-surface order or DD coverage was promoted to verified.
- Added fail-closed callback/recording exception handling, strict invalid-kind/value rejection, integer index/value preservation in independent diagnostic schema 2, and a zero-event ambiguity notice without broadening the allowlist or collecting chat text.
- Expanded edge-case coverage for boundaries, ambiguity, duplicates, reversed order, different DD context, limits, stop/restart/dispose and privacy.
- Read all six local schema-5 originals through the production codec without modification. They preserve EO 1-10 classification, one session per file, nullable exact floor, player pose and B2 actor quality; they do not prove exact-floor event reception.
- Added a bounded, opt-in metadata-only fallback proposal and completed the live checklist. Live verification remains pending; Phase 5.6 and PR merges remain prohibited.

## 2026-11-08 — Phase 5.5C chat exact-floor investigation and design

- Confirmed from the installed Dalamud API 15.0.3.9 contract that public `IChatGui` exposes both structured `LogMessage` and displayed `ChatMessage` callbacks.
- Located pinned game-data candidates: row 7248 (`転移が実行された！`), row 7270 (`地下UNKNOWN階`) and row 9218 (`第UNKNOWN層`), all with LogKind 57.
- Kept event reception, parameter mapping, event order and PotD/HoH coverage explicitly unverified; the user-observed EO `地下9階` remains visual evidence only.
- Designed a structured-row-first parser, B1 range reconciliation, `Verified`/`Unverified`/`Missing`/`Conflict`/`Stale` quality model, floor-epoch invalidation and privacy-minimal diagnostics.
- Added a separate live verification plan for EO, Palace of the Dead and Heaven-on-High without claiming support for untested dungeons.
- Documentation only. No chat subscription, schema change, internal access, gameplay operation, execution gateway change, deployment or merge was performed.

## 2026-11-08 — Phase 5.5B B2 actor spatial quality and schema 5

- Added explicit actor spatial `Usable`, `Quarantined` and `Missing` states with source, quality and stable reason evidence.
- Preserved exact `(0,0)` separately from missing data and quarantined it without any BaseId-specific production rule.
- Raised new replay/world output to schema 5 and added strict schema 3/4 DTO migration from legacy `objectId` to normalized `BaseId`.
- Required usable position and rotation before hazard geometry; uncertain evidence remains unknown/unsupported and does not feed rooms, routes or distances.
- Added aggregate-only Japanese spatial diagnostics while retaining player/party privacy filtering.
- Read all six original schema 3/4 exports in place without modification: 1,298 snapshots and 14,359 actor observations normalized successfully, all legacy positions quarantined.
- Added 17 B2 tests; 301 tests pass at the implementation checkpoint. Final audits and CI are recorded in `phase5.5b-b2-verification.md`.
- No internal/native access, game operation, deployment or merge was performed.

## 2026-11-08 — B1 live UI result and B2 implementation plan

- Recorded the user-confirmed DDCore 0.5.2.0 UI result for Eureka Orthos 1-10: the DD field displayed `オルト・エウレカ 1～10階帯（正確な階層は未取得）`.
- Kept Palace of the Dead, Heaven-on-High and every other floor band explicitly unverified.
- Rechecked the fixed replay folder: no producer-0.5.2.0 export exists, so the UI result is labelled user-observed rather than replay-confirmed.
- Added the B2 actor spatial-quality/schema-5 implementation plan with explicit legacy migration, generic origin quarantine, consumer gates, tests, acceptance criteria and approval boundary.
- Documentation only. No B2 code, native access, game operation, deployment or merge was performed.

## 2026-11-08 — Phase 5.5B B1 public classification

- Added public `ConditionFlag.InDeepDungeon` observation independently from duty-start state.
- Added a checked manifest for all 40 supported Palace of the Dead, Heaven-on-High and Eureka Orthos ten-floor bands, pinned to `ffxiv-datamining` commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`.
- Classification now requires the observed CFC row, observed territory, row-referenced territory, data-content row and short code to agree. Unknown or conflicting evidence remains unsupported.
- Added an optional ten-floor range and direct-DD-condition evidence to `DeepDungeonContext` without changing schema 3/4 numbers. Exact floor remains `null` for public classification.
- Removed boss-actor exact-floor inference and the shortcut that treated any started duty as a Deep Dungeon.
- Preserved the Phase 5 fail-closed gate: a ten-floor band is evidence only and does not make the exact floor known.
- Release build, 284 tests, format, vulnerability and prohibited-operation audits passed; both push and Draft PR CI runs passed.
- Pushed implementation commit `cf54cad` and opened stacked Draft PR #10 against `phase5.5b/implementation-plan`.
- Live Dalamud behavior remains unverified; B2/native access/game operation were not added.

## 2026-11-08 — Phase 5.5B live-evidence implementation plan

- Read four original exports from the configured local folder without modifying or committing them; identified the latest valid schema-4/producer-0.5.1.0 EO capture with 34 snapshots.
- Confirmed that recorded `contentId` is the public ContentFinderCondition RowId and matched row 897 plus territory 1099 to the EO 1-10 floor band in a fixed game-data dump.
- Found that replay `objectId` currently stores `BaseId`; aggregated eight repeated BaseId-6388 actors at `(0,0)` without claiming their runtime meaning.
- Designed public DD classification/floor-band, actor spatial quality/schema migration, separately approved exact-floor native prototype and later topology slices.
- Added repository-wide replay handling/privacy rules in `AGENTS.md`.
- Documentation only: no Phase 5.5B functionality, native read, game operation, execution gateway change or merge was performed.

## 2026-11-08 — Phase 5.5A observation foundation

- Added reusable source/quality/missing-reason metadata and an identifier-free nullable player pose.
- Read public Dalamud API 15 `LocalPlayer` position/rotation without persisting the player identity; missing, exception and invalid-value paths remain explicit.
- Raised replay/world output to schema 4 while retaining strict schema 3 read compatibility. Both original exports still decode unchanged (412 snapshots total).
- Preserved anonymous pose through recording privacy sanitization while continuing to remove Player/PartyMember actors and references.
- Exposed pose only as Phase 5 evidence; no room inference or actionable strategy input was invented, and Unknown/content-exit states still stop.
- Added tests for origin/missing distinction, public read failure, absent player, logout/content exit, legacy JSON, privacy and fail-closed conversion.
- No native access, BossMod runtime dependency, gameplay operation, Phase 5.5B, Phase 6 or `main` merge was added.

## 2026-11-08 — Phase 5.5 observation acquisition research

- Compared every requested P0-P2 observation with public Dalamud API 15, BossMod fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`, current DDCore and the two original replay exports.
- Confirmed public actor/self pose, cast, status, duty and `InDeepDungeon` observations, but found no verified public floor, room-map, passage, trap, pomander or DD remaining-time contract.
- Confirmed that BossMod obtains DD ID, floor, map, party room, passage, items, chests and magicite through FFXIVClientStructs in `UpdateDeepDungeon`; this is internal, patch-sensitive access and was not implemented.
- Preserved the user-provided 3-to-4 floor label separately from JSON facts; the 412 snapshots still contain no authoritative floor transition.
- Designed source/quality provenance, optional pose, floor epochs, transition invalidation and a source-neutral fusion boundary.
- Recommended the first approved slice as provenance/optional coordinates plus anonymized self pose, followed by separately gated kind/floor and topology work.
- Added documentation only. No production code, internal access, game operation, BossMod runtime dependency, Phase 6 work or `main` merge was performed.

## 2026-11-08 — Phase 5 real-data validation

- Auto-detected two unmodified local replay exports and validated 412 schema-version 3 snapshots with the production replay codec.
- Added a read-only `WorldSnapshotStrategyAdapter` that transfers only direct observations and preserves unavailable room, exit, resource and timing inputs as Unknown.
- The user-labelled floor 3 to floor 4 candidate contains 124 snapshots, but no floor number or transition event; the label cannot be independently derived from the JSON.
- All 412 strategy evaluations completed without exceptions and returned deterministic fail-closed `Stop` / `Unknown` results. No actionable decision was produced.
- Added four adapter/fail-closed tests. Core tests now pass 173/173 and the full local verification is recorded in `phase5-verification.md`.
- Phase 6 is not ready because authoritative floor, room graph/membership, exit, traps, pomanders, remaining time and resource readiness are not observed.

## 2026-11-08 — User-defined Phase 5 DD strategy engine

- Reconciled the explicit Phase 5 instruction with the original master plan: this branch completes a bounded decision-engine layer and does not start the original Phase 5 operation foundation.
- Added immutable Unknown-preserving state for dungeon/floor, the complete 25-room graph, exploration/danger, exit, enemies, traps, pomanders, aggregate resource readiness and remaining time.
- Added deterministic fail-closed decisions for danger retreat, engaged combat, resource preservation, hostile enemies, active exits, unexplored rooms and exit recheck.
- Reused the Phase 4 attributed `RoomPathfinder` and excluded unsafe/unknown rooms, traps and non-defeated enemies from candidate routes.
- Added 30 synthetic Core tests for normal, abnormal, missing, regressing, no-route and deterministic scenarios.
- Locked restore, Release build, all 209 tests, format, dependency/reference, NuGet vulnerability and prohibited-operation audits passed.
- Package `0.5.0.0`, API 15, SHA-256 `558A4A8E6C688BEEE3D9D199D93EFD0DFD80C06BCA6A9636EA503C147278BD0A`.
- Opened stacked Draft PR #7 against `phase4.5/bossmod-integration-proof`, and confirmed final push run `42412810484` plus PR run `42412814600` passed at verified head `bc2e5dc`.
- Live FFXIV/Dalamud and real-floor strategy behavior remain unverified; the public adapter still cannot populate the required DD room/resource state.

## 2026-11-08 — Phase 4.5 BossMod integration proof

- Verified 37 fixed-commit BossMod IPC functions and separated technical reachability from DD strategy-data availability.
- Added an optional, read-only, fail-closed reachability probe using public Dalamud IPC only.
- Added Japanese diagnostics that explicitly report the missing protocol contract and missing DD data contract.
- Added automated absent/reachable/faulted/OID tests and documented the required BossMod-side versioned DTO contract.
- Release build passed with 0 warnings/errors; all 179 tests passed; format and NuGet vulnerability checks passed.
- Recommendation: retain independent migration unless BossMod adds and maintains a versioned read-only DD snapshot API.

## 2026-11-07

- Created the private GitHub repository and cloned it locally.
- Pinned BossMod research to commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.
- Located 63 Deep Dungeon source/resource files and the principal shared dependencies.
- Confirmed the current Dalamud API level and official distribution restrictions.
- Verified the local toolchain with .NET SDK 10.0.302.
- Built `BossMod.Modules` in Release configuration: 0 errors, 25 warnings, elapsed 1:52.88.
- Recorded the ImageSharp 3.1.12 transitive dependency advisories without treating them as a DDCore finding.
- Completed the Phase 1-6 master plan, architecture, dependency inventory, migration plan and research report.
- Kept plugin implementation out of scope as instructed.
- Next action requires user approval: select the Phase 2 foundation scope and resolve the open scope questions before implementation.
- Re-fetched BossMod and confirmed the pinned commit remains the public `origin/master` head.
- Integrated the verified source map, replay/action-hook findings, implementation methods, cross-phase risk register, validation matrix and approval gates directly into `DDCore_Codex_MasterPlan.md`.
- Reconfirmed API 15 as the stable baseline and recorded API 16 as an explicitly unfinished future migration risk.

## 2026-11-07 — Phase 2 plugin foundation

- Created branch `phase2/plugin-foundation`; `main` remains unchanged.
- Added the .NET 10 / Dalamud API 15 solution with `DDCore.Core`, `DDCore.Plugin`, and `DDCore.Core.Tests`.
- Implemented the five mode definitions with Observer as the mandatory startup mode.
- Implemented fail-closed mode transitions, persistent emergency stop, and a permanently disabled Phase 2 execution gateway.
- Added Japanese main/configuration windows, `/ddcore` commands, versioned configuration persistence and Dalamud logging.
- Added World, Mechanics, Navigation, Combat, Resources, Decision and Execution contracts without concrete game-operation implementations.
- Release build succeeded with 0 warnings and 0 errors.
- All 15 unit tests passed; package and manifest contents were inspected.
- `dotnet format --verify-no-changes` passed and NuGet vulnerability audit reported no known vulnerable packages.
- Live Dalamud loading, Japanese UI rendering and live reload persistence are explicitly unverified; the user performs those checks.

## 2026-11-07 — Phase 2 review fixes

- Added fail-closed recovery for configuration load exceptions: Observer mode plus a latched emergency stop, followed by one safe replacement-save attempt.
- Added non-throwing save failure handling, error logging, persistence health state, and a session-only recovery message.
- Tightened safety boundaries: authorization is granted only from Observer and is revoked by non-operating transitions, emergency stop, and restored stop state.
- Added an execution contract requiring fresh safety revalidation inside the gateway immediately before any future external side effect.
- Added reverse-order rollback for command, UI callback and window resources when plugin construction fails; disposal remains idempotent.
- Expanded the test suite from 15 to 26 tests. Live Dalamud failure injection remains unverified.

## 2026-11-07 — Phase 2 completion and Phase 3 preparation

- Converted PR #1 to Draft and updated it with the verified 26-test result, clean Release build, dependency audit and explicit live-Dalamud verification gap.
- Re-fetched BossMod and confirmed the analysis commit remains `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.
- Re-audited the DD inventory: 63 paths, comprising 59 C# files and 4 JSON resources.
- Documented an immutable `WorldSnapshot`, normalized event deltas, serializable shape algebra, hazard/mechanic output, deterministic analyzer contracts and a fail-closed unknown state.
- Defined replay/mock/golden/differential/malformed-input/architecture test layers and the Phase 3 implementation gates.
- Recorded BSD-3-Clause provenance requirements and current official Dalamud publication constraints.
- Recommended Eureka Orthos floor 10 (Gancanagh) as the first bounded vertical slice, pending user approval.
- Phase 3 implementation, game operation and `main` merge were not performed.

## 2026-11-07 — Phase 3 mechanic foundation

- Created `phase3/mechanics-foundation` from the unmerged Phase 2 branch; `main` remains untouched.
- Implemented P3.0 through P3.5 without game operations or a BossMod runtime dependency.
- Added six pure C# AoE shapes, immutable snapshots, bounded local JSON replay, deterministic analyzer contracts and fail-closed unknown handling.
- Implemented Gancanagh cast analysis for actions 31478, 32700 and 31479, plus interruption, disappearance, elapsed-cast and unknown-action handling.
- Added pinned BossMod provenance and the BSD-3-Clause binary/source notice.
- Synthetic replay tests pass; live Dalamud and live Gancanagh behavior remain unverified.

## 2026-11-07 — Phase 3 accuracy review

- Compared Gancanagh, `StandardAOEs`, common AoE shapes and BossMod angle conventions at fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.
- Corrected cast hazards to use cast location/rotation and cast-start ordering rather than actor position/rotation and entity ordering.
- Made instantaneous and persistent lifetimes explicit without inventing a Gancanagh duration.
- Retained pre-activation hazards across a missing-caster snapshot and preserved unknown analyzer assessments in registry aggregation.
- Added time-regression, consecutive-cast, actor-generation reuse, missing/dead/destroyed actor, target location/rotation, lifetime, geometry error and replay-gap tests.
- Bumped snapshot, mechanic-frame and replay schemas to version 2 and the plugin package to `0.2.1.0`.
- Recorded remaining live-adapter, Mandrastorm threshold, simultaneous event-order and float/double comparison risks in `phase3-accuracy-review.md`.

## 2026-11-07 — Phase 3.6 live observation adapter

- Created `phase3/live-observation-adapter` from the unmerged Phase 3 branch.
- Verified the used API surface against local Dalamud 15.0.3.9 metadata/XML and official API 15 documentation.
- Added a Dalamud-free observation assembly and a public-API-only Plugin adapter; no private/native access or game operation was added.
- Added session, entity-generation and cast-lifecycle tracking, explicit nullable unknowns, stale-time handling and fail-closed partial analysis.
- Connected live snapshots to the existing Gancanagh analyzer and added a Japanese read-only diagnostics panel.
- Direct DD floor, cast ground location, cast-specific rotation and status stack count remain unavailable through the verified public API and are not fabricated.
- Added 15 observation tests while retaining all 125 Core tests. Live Dalamud and encounter behavior remain unverified.
- Extended the existing Windows CI to run the full solution tests for the Phase 3.6 branch and its Draft PR base.
- Confirmed both the push and Draft PR full-solution GitHub Actions runs succeeded; PR #3 remains Draft and unmerged.

## 2026-11-07 — Phase 3.7 diagnostics and recording

- Created `phase3/diagnostics-and-recording` from the clean unmerged Phase 3.6 branch.
- Added bounded change-only normal diagnostics and explicit-start local observation recording.
- Reused the strict JSON replay format with 2 Hz sampling, five-second atomic flushes, and lower 2 MiB/1,800-snapshot/15-minute limits.
- Added safe stop behavior for storage failure, session/territory/logout transition, limit exhaustion and plugin disposal.
- Removed player and party-member actors and references before recording; chat, names and account data are not observed.
- Added Japanese start/stop, preview, export and confirmed-delete controls. Recording remains OFF by default and has no uploader.
- Added recording format/privacy documentation and a user-run live test procedure. Live Dalamud and FFXIV testing remains unperformed.
- Pushed implementation commit `478ab76`, opened Draft PR #4 against the Phase 3.6 branch, and confirmed both push and PR GitHub Actions runs passed.

## 2026-11-08 — Phase 4 BossMod reuse analysis

- Created `phase4/reuse-analysis` from the unmerged Phase 3.7 branch; `main` remains untouched.
- Measured the fixed BossMod corpus as 67 files and 28,609 physical lines under an explicit, reproducible scope.
- Analyzed `FloorPathfind`, `AutoClear`, `DeepDungeonAI`, `DeepDungeonState`, `AIHints`, `ActionQueue`, `Rooms` and `Traps` by pinned source line.
- Mapped strategy inputs to the current immutable `WorldSnapshot` and recorded missing authoritative room, passage, resource, trap and execution inputs.
- Adapted only the independent 5x5 room-graph BFS into pure C# with strict graph validation and per-call state.
- Added a source-faithful comparison oracle, deterministic fixtures and all-pairs route comparison without adding any game operation.
- Recorded component-level and reviewed-line reuse measurements, provenance, BSD notice and Phase 5-8 planning estimates.
- Locked restore and Release build passed with 0 warnings/errors; 173 tests passed (Core 139, Observation 34).
- Format verification and NuGet vulnerability audit passed; Core has no project references and the package contains both provenance manifests plus the BSD notice.
- Pushed implementation commit `0d07b68`, opened Draft PR #5 against `phase3/diagnostics-and-recording`, and confirmed both push and PR GitHub Actions runs passed.

## 2026-11-09 — Phase 5.5D chat floor diagnostics

- Confirmed Phase 5.5C Draft PR #12 is complete as a design phase; its public-event candidates remain live-unverified.
- Added an explicit-start, read-only `IChatGui.LogMessage` / `ChatMessage` probe for candidate rows 7248, 7270 and 9218 and system chat kind 57.
- Stored only bounded row IDs, integer parameters, timestamps and B1 reconciliation results; chat text, sender and player identity are not retained.
- Kept candidate floors out of `WorldSnapshot` and the Phase 5 strategy engine. Matching B1 evidence remains labelled consistent but unverified; conflicts stay explicit.
- Added independent schema-1 diagnostic export with 256-entry, 256 KiB and 15-minute limits. Replay schema 5 and schema 3/4 read compatibility are unchanged.
- Reverified the original schema-5 floor 3-4 replay as 105 snapshots and 1,535 actor observations (641 usable, 894 quarantined, 0 missing) without modifying it.
- Added Japanese opt-in controls, privacy preview, local export and confirmed deletion. Live FFXIV/Dalamud behavior remains unverified and requires user operation.
- Pushed implementation commit `84aac4c`, opened stacked Draft PR #13 against `phase5.5c/chat-floor-design`, and confirmed both push and pull-request GitHub Actions runs passed.

## 2026-11-11 — Stage 5.6A environment object diagnostics

- Created `phase5.6a/environment-object-diagnostics` from the unmerged Phase 5.6 design branch; PR #13, PR #14 and `main` remain untouched.
- Added an explicit-start public-API adapter for `EventObj`, `Treasure` and `Aetheryte`; `IGameObject` references are copied to values during the framework callback and never retained.
- Added session-scoped anonymous IDs, disappearance/runtime-ID reuse handling and nullable `Usable` / `Quarantined` / `Missing` coordinate quality.
- Added independent schema-1 JSON with 512 KiB, 1,000-sample, 15-minute and 250 ms limits. Raw runtime IDs, names, chat and player identity are not serialized.
- Added Japanese start/stop, summary, privacy preview, confirmed export and confirmed deletion controls. Recording remains OFF by default.
- Kept environment observations outside `WorldSnapshot`, BFS and strategy decisions; no semantic object classification or game operation was added.
- Live Dalamud and FFXIV behavior remains unverified and requires the documented user-run procedure.

## 2026-11-15 — Stage 5.6B selected-object correlation

- Started `phase5.6b/selected-object-correlation` from the unmerged Stage 5.6A head `3e1bf44`; no existing PR or `main` was merged.
- Added a read-only copy of the current public `ITargetManager.Target` values and same-sample exact matching against the existing filtered `IObjectTable` observations. Runtime IDs are transient and never exported.
- Added schema-2 selection correlation metadata, conservative mismatch/ambiguity handling, independent coordinate quality, targetability transitions, disappearance, and runtime-key return events labelled continuity-unknown with a fresh anonymous ID.
- Added actual schema-1/2 JSON deserialization validation and a schema-1 sample compatibility test. Replay schema 3/4/5 and chat-floor schema 2 are unchanged.
- Added Japanese UI display of latest match quality, BaseId/anonymous ID, coordinate quality, and transition count; added the live verification procedure and explicit privacy checks.
- Re-read the original local recordings read-only and rehashed before/after: 12 replay JSONs (2,167 snapshots total) did not contain those BaseIds, but 11 separate schema-1 diagnostic files had four unique payloads. The 200-sample 22:05:31 recording shows BaseId 2007357 targetability true-to-false at sample 25 (22:05:37.690), last visible false at sample 43 (22:05:42.309), then absent; the 160-sample 22:09:55 recording shows BaseId 2007358 true-to-false at sample 79 (22:10:15.563), last visible false at sample 99 (22:10:20.664), then absent. These are object-observation facts only; neither file records selected target, screen identity, nor an opened event. The prior draft statement about absence was too broad and has been corrected in the design doc.
- Local verification: locked restore passed; Release build passed with 0 warnings/errors; Core 197/197 + Observation 163/163 = 360 tests passed; format verification passed; NuGet vulnerability audit found no vulnerable packages; Core has no project references and Observation/Plugin reference only the intended DDCore layers. Package `0.5.7.0`, API 15, `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`, SHA-256 `CDAD99E3FD2D9428F27B1DDC2ABE904BD94E291CF55011002DF2F6C7DBE1474F`.
- Commit `bd636f9ba2aa53998e596df6a87dda91884270d5` was pushed; stacked Draft PR #16 targets `phase5.6a/environment-object-diagnostics`: `https://github.com/natukimiyabi23-eng/DDCore/pull/16`. GitHub Actions run `43720466400` passed. Live game behavior remains unverified; no merge was performed.

## 2026-11-15 — Stage 5.6B live verification record

- User reported an EO B1 live run on DDCore 0.5.7.0/schema 2; corresponding local diagnostic originals were re-read without modification. The 69 selected `exactRuntimeMatch` observations for BaseId 2007358 / `obj-000005` and 80 for reproducer BaseId 2013286 / `obj-000002` were reproduced from schema-2 samples; selected match coordinates reported usable.
- A separate 2007357 run reports `obj-000001` exact selection samples; in another run it changes targetability at sample 20 and has a disappearance transition at sample 40, while `obj-000003` with the same BaseId remains. The screenshot's visible reproducer label and user-reported manual chest opening remain separate evidence labels; JSON does not prove semantic identity or opening causality.
- Stage 5.6B scoped completion is confirmed. `exactFloor` remains null; floor transitions, re-entry, other DDs, reappearance identity, and automatic reproducer activation-state detection remain unverified.
- Final documentation checkpoint `0e359b38ed7b161047d6c26cea611b09c2019eb2` pushed to the existing Draft PR #16; GitHub Actions run `43722744224` passed. Local Release tests passed 360/360; `git diff --check` passed. PR remains open/Draft and unmerged.

## 2026-11-16 — Offline room-label evaluator provenance guard

- Reviewed the local screenshot folders and DDCore export directory read-only. The latest six EO schema-5 replays contain 869 snapshots; every player pose is marked `source=publicDalamud`, `quality=direct`. No independent B1 room-layout screenshot or user-authored label JSON was found in the inspected locations. Reviewed captures showed EO B8/B9/B10 or the diagnostics panel, so none was used as a B1 label.
- Hardened the offline `--validate-room-labels` evaluator: finite coordinates are now scored only when pose provenance is explicitly `publicDalamud` with `direct` quality. Missing, stale, inferred, or other-source metadata increments `untrustedPoseCount` and is excluded from the match-rate denominator.
- Bumped the aggregate evaluator report schema to 2 for the new summary field; the user-authored labels input remains schema 1.
- Added two regression cases for stale quality and unknown source. Probe evaluator tests pass 10/10; the first run exposed a test JSON-string syntax issue, corrected before successful verification.
- This is an offline tooling/data-integrity change only. No plugin runtime, replay original, game asset, BFS, strategy, or game process was changed. Independent labeled B1 data remains unavailable; no live-map accuracy claim or M0 PASS is made.
- Locked restore, Release solution build (0 warnings/errors), explicit Lumina probe Release build (0 warnings/errors), all 370 tests, format verification, and diff check passed. `dotnet list ... package --vulnerable --include-transitive` stalled after restore and was interrupted, so the local NuGet vulnerability result is unverified. Source commit `a93715afee407663331eef4d70bc01b88ce1439b` was pushed to existing Draft PR #22; GitHub Actions run `43758784434` passed build, tests and probe. PR #22 remains open/Draft/unmerged.
- Follow-up API 15 feasibility check: official `IDataManager` documentation and local Dalamud.xml expose `GetFile<T>`, `GetFileAsync<T>`, `FileExists`, and Excel sheet access. The existing offline probe already derives the EO `planmap.lgb` path from the territory background and extracts static room candidates. DDCore.Plugin does not currently inject `IDataManager`; adding an opt-in read-only static-candidate display is technically plausible, but it cannot establish active random layout, open passages, collision, or walkability. Public API contract checked at [Dalamud API 15 IDataManager](https://dalamud.dev/api/api15/Dalamud.Plugin.Services/Interfaces/IDataManager/); no plugin integration or live test was performed.
