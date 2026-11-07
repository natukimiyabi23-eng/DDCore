# DDCore continuation

Status: complete
Updated: 2026-11-07 JST

## Objective

Phase 2ブランチを基点に `phase3/mechanics-foundation` を作成し、ゲーム操作を伴わないP3.0〜P3.5（来歴管理、幾何計算、WorldSnapshot、JSONリプレイ/モック、解析基盤、ガンカナグ解析）を段階実装・検証してPhase 2ブランチ向けPRを作成する。mainマージと実機操作は行わない。

現在の追加目的: 固定BossModコミットとPhase 3解析基盤を精度照合し、AoEライフサイクル、幾何、時系列、安全側処理、決定論的リプレイの不具合を修正してDraft PR #2を更新する。

## Current state

- Repository: `https://github.com/natukimiyabi23-eng/DDCore` (public, default branch `main`)
- Working branch: `phase3/mechanics-foundation`
- Local path: `work/DDCore`
- BossMod source: `https://github.com/awgil/ffxiv_bossmod`
- BossMod analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- BossMod license: BSD-3-Clause
- DD source files found: 63, including Global shared code, Palace of the Dead, Heaven-on-High, Eureka Orthos, and Pilgrim's Traverse.
- Source version: `0.1.0.0` (Phase 2 foundation complete).
- Deployed version: none.
- Documentation commit: `1217bac70961debec3201ea74e7a8e1966e5fd57`

## Completed

- GitHub authentication verified for `natukimiyabi23-eng`.
- Confirmed no existing `DDCore` repository before creation.
- Created private repository with README and default branch `main`.
- Cloned and pinned current BossMod `master` for research.
- Identified shared DD classes: `AutoClear`, `FloorPathfind`, `DeepDungeonState`, `AIHints`, `ActionQueue`, room/trap/minimap/config components.
- Confirmed current BossMod uses `Dalamud.NET.Sdk/15.0.0`, .NET SDK 10, and BSD-3-Clause.
- Confirmed official Dalamud restrictions do not accept automated combat/gameplay plugins into the official repository; private/custom distribution must be treated separately.
- Built the pinned `BossMod.Modules` project in Release configuration with .NET SDK 10.0.302: 0 errors, 25 warnings, elapsed 1:52.88.
- Completed all requested planning and research documents for Phase 1 through Phase 6.
- Recorded facts, assessments, source paths/classes, pinned permalinks, risks, tests and completion criteria.
- Validated the requested repository tree and retained this continuation record as an additional project-control file.
- Re-fetched BossMod and confirmed `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0` remains the public `origin/master` head.
- Integrated verified source mappings, replay/action-hook evidence, dependency direction, implementation methods, risk register, validation matrix and approval gates into the MasterPlan.
- Rechecked all 17 pinned BossMod blob links against the analyzed checkout and confirmed the 63-file DD inventory.
- Confirmed that no `.cs`, `.csproj` or `.sln` implementation file was added.
- Implemented the standalone .NET 10 / Dalamud API 15 plugin foundation without a BossMod runtime reference.
- Implemented Japanese main/configuration windows, Observer-first lifecycle, five mode definitions, emergency stop, safety-checked transitions, responsibility contracts, configuration migration/persistence and logging.
- Added a permanently disabled Phase 2 execution gateway; no movement, combat, target, item or game-operation implementation exists.
- Added 15 unit tests and a Windows CI workflow.
- Updated the Phase 2 documentation and verification record.
- Added fail-closed configuration load recovery: Observer mode with emergency stop latched, error logging, and one safe replacement-save attempt.
- Added non-throwing save failure handling with persistence health state and session-only fallback.
- Tightened authorization boundaries and expanded the test suite from 15 to 26 tests.
- Added the mandatory execution-time safety revalidation contract.
- Added reverse-order constructor rollback and idempotent disposal for plugin resources.
- Re-ran Release build, all tests, format verification, static reference checks and NuGet vulnerability audit.
- Converted PR #1 to Draft and updated it with the 26-test result and explicit live-Dalamud verification gap.
- Reconfirmed the fixed BossMod commit and audited 63 DD paths: 59 C# files and 4 JSON resources.
- Added the Phase 3 mechanic-analysis design covering immutable snapshots, geometry/hazards, deterministic analyzers, replay/mock tests, provenance, official distribution constraints, implementation gates, risks and unresolved decisions.
- Re-ran the Phase 2 Release build, 26 tests, format verification and NuGet vulnerability audit after the documentation work.

## Unfinished

- Requested Phase 3 accuracy-review work is complete.
- Live Dalamud/FFXIV verification remains the user's responsibility and is not reported as successful.


## Accuracy review checkpoint

- BossMod固定コミットから `D10Gancanagh.cs`、`GenericAOEs.cs`、`AOEShapes.cs`、`Angle.cs` を取得・照合済み。
- 詠唱地点/回転、開始時刻優先の6件制限、瞬間/持続ライフサイクル、欠落キャスター保持を実装済み。
- Registryの時刻逆行とunknown集約、遭遇アクター欠落、連続詠唱、generation付きID再利用を安全側へ修正済み。
- 回転変換規約、安定した距離計算、無効寸法/角度/NaN/Infinity/境界誤差テストを追加済み。
- `docs/phase3-accuracy-review.md` とBossMod共通AoE/shape来歴を追加済み。
- Snapshot/MechanicFrame/Replay schemaは2、プラグイン版は`0.2.1.0`へ更新済み。

## Files changed

- `.github/workflows/phase2-build.yml`
- `.gitignore`
- `CONTINUATION.md`
- `DDCore.slnx`
- `Directory.Build.props`
- `README.md`
- `THIRD-PARTY-NOTICES.md`
- `docs/DDCore_Codex_MasterPlan.md`
- `docs/architecture.md`
- `docs/dependencies.md`
- `docs/phase2-verification.md`
- `docs/phase3-mechanics-design.md`
- `docs/progress.md`
- `global.json`
- `src/DDCore.Core/**`
- `src/DDCore.Plugin/**`
- `tests/DDCore.Core.Tests/**`

## Build / test

- `dotnet build DDCore.slnx -c Release --no-restore`: success, 0 warnings, 0 errors.
- `dotnet test tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj -c Release --no-build --no-restore`: 26 passed, 0 failed, 0 skipped.
- `dotnet format DDCore.slnx --verify-no-changes --no-restore`: success.
- `dotnet list DDCore.slnx package --vulnerable --include-transitive`: no known vulnerable packages in all three projects.
- Package manifest: `InternalName=DDCore`, version `0.1.0.0`, Dalamud API level 15.
- Assembly/package audit: no BossMod reference; execution gateway is disabled.
- Phase 3 final tests (2026-11-07再実行): 103 passed, 0 failed, 0 skipped.
- Phase 3 final Release build (2026-11-07再実行): success, 0 warnings, 0 errors.
- `dotnet format DDCore.slnx --verify-no-changes --no-restore`: success.
- NuGet vulnerability audit: no known vulnerable packages in all three projects.
- Phase 3 package manifest: version `0.2.0.0`, Dalamud API level 15.
- Phase 3 ZIP contains `THIRD-PARTY-NOTICES.md` and `provenance/bossmod-phase3.json`.
- GitHub Actions: both push and Draft PR `build-and-test` runs succeeded.
- Draft PR: `https://github.com/natukimiyabi23-eng/DDCore/pull/2` targeting `phase2/plugin-foundation`.
- CI maintenance note: `actions/upload-artifact@v4` emitted a Node.js 20 deprecation annotation while GitHub forced Node.js 24; the job still succeeded.
- Accuracy checkpoint Release build: success, 0 warnings, 0 errors.
- Accuracy checkpoint tests: 125 passed, 0 failed, 0 skipped.
- Accuracy checkpoint format verification: success.
- Accuracy final NuGet vulnerability audit: no known vulnerable packages in all three projects.
- Accuracy final static dependency audit: Core has no project references or BossMod/Dalamud assembly reference; Plugin references Core and Dalamud SDK only.
- Accuracy final package: `InternalName=DDCore`, version `0.2.1.0`, Dalamud API level 15, no BossMod assembly.
- Accuracy-review push and Draft PR GitHub Actions: both succeeded.
- Draft PR #2 body now records the BossMod comparison, 125-test result, residual differences and live-verification gap.

## Deployment

- None. No dev-plugin deployment or live UI operation was requested for this repository task.

## Artifact hashes (SHA-256)

- Latest local rebuild of `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`: `9CFDA877FF0351D164117C82FD94204AC03DE8DF1777491914A22BF89770138B`
- Current Phase 3 local package: `4DF9B67F6C4BB4F9077773F72085234147C56F42631C2C886B603C6C3703EB83`
- Accuracy review package `0.2.1.0`: `BB37604E72600FF97743B38F75E7268ACAB96AE62ECFE360771C27FB3331837E`
- No artifact was deployed.

## Known evidence

- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` derives from `ZoneModule` and consumes `AIHints`, `ActionQueue`, `WorldState.DeepDungeon`, pathfinding, Dalamud IPC, and BossMod services.
- `BossMod/Data/DeepDungeonState.cs` models rooms, party, pomanders, chests, magicite, floor and passage/return progress.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs.
- Official Dalamud plugin restrictions state that automated combat/gameplay is not acceptable for the official plugin repository.

## Safe next step

Review Draft PR #2. Do not merge to `main` without explicit user approval, and do not infer live behavior from unit/replay tests.
