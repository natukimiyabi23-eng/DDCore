# DDCore continuation

Status: complete
Updated: 2026-11-07 JST

## Objective

`phase2/plugin-foundation` ブランチで、BossMod非依存のDalamud API 15プラグイン基盤、日本語UI、5モード、安全ゲート、緊急停止、責務別インターフェース、設定保存・移行、ログ、単体テストを実装する。自動操作とゲーム内部フックは実装しない。

## Current state

- Repository: `https://github.com/natukimiyabi23-eng/DDCore` (public, default branch `main`)
- Working branch: `phase2/plugin-foundation`
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

## Unfinished

- No required Phase 2 source work remains.
- User verification remains: enable/load the plugin, confirm Japanese rendering, confirm configuration persistence across a live reload, and confirm unload/reload behavior.
- Review and merge the Phase 2 PR only after user approval.

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
- `docs/progress.md`
- `global.json`
- `src/DDCore.Core/**`
- `src/DDCore.Plugin/**`
- `tests/DDCore.Core.Tests/**`

## Build / test

- `dotnet build DDCore.slnx -c Release --no-restore`: success, 0 warnings, 0 errors.
- `dotnet test tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj -c Release --no-build --no-restore`: 15 passed, 0 failed, 0 skipped.
- `dotnet format DDCore.slnx --verify-no-changes --no-restore`: success.
- Package manifest: `InternalName=DDCore`, version `0.1.0.0`, Dalamud API level 15.
- Assembly/package audit: no BossMod reference; execution gateway is disabled.

## Deployment

- None. No dev-plugin deployment or live UI operation was requested for this repository task.

## Artifact hashes (SHA-256)

- `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`: `D71BF70AC5399F7A5E460BF975118BB526FEBAF74C9C70060896059AC6F1A21D`

## Known evidence

- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` derives from `ZoneModule` and consumes `AIHints`, `ActionQueue`, `WorldState.DeepDungeon`, pathfinding, Dalamud IPC, and BossMod services.
- `BossMod/Data/DeepDungeonState.cs` models rooms, party, pomanders, chests, magicite, floor and passage/return progress.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs.
- Official Dalamud plugin restrictions state that automated combat/gameplay is not acceptable for the official plugin repository.

## Safe next step

Wait for user review of the Phase 2 PR. Do not merge it without approval. If live verification is requested, the user performs plugin enablement/reload; record observed results here before claiming those checks complete.
