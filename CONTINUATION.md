# DDCore continuation

Status: complete
Updated: 2026-11-07 JST

## Objective

BossModのディープダンジョン関連機能を独立プラグイン化するため、非公開GitHubリポジトリ、開発仕様書、Phase 1〜6の事前調査を作成する。今回はプラグイン本体を実装しない。

## Current state

- Repository: `https://github.com/natukimiyabi23-eng/DDCore` (private, default branch `main`)
- Local path: `work/DDCore`
- BossMod source: `https://github.com/awgil/ffxiv_bossmod`
- BossMod analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- BossMod license: BSD-3-Clause
- DD source files found: 63, including Global shared code, Palace of the Dead, Heaven-on-High, Eureka Orthos, and Pilgrim's Traverse.
- Source version: documentation only; plugin version not assigned.
- Deployed version: none.

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

## Unfinished

- No required work remains for the repository-creation and pre-research objective.
- Plugin implementation remains intentionally unstarted and requires explicit user approval.
- User decisions are still needed for Pilgrim's Traverse scope, absolute versus optional BossMod independence, optional execution providers, the first solo job/floor target, and private-only Autonomous distribution.

## Files changed

- `.gitignore`
- `CONTINUATION.md`
- `README.md`
- `docs/DDCore_Codex_MasterPlan.md`
- `docs/architecture.md`
- `docs/dependencies.md`
- `docs/migration-plan.md`
- `docs/research-report.md`
- `docs/progress.md`

## Build / test

- `dotnet build .\BossMod.Modules\BossMod.Modules.csproj -c Release`: success, 0 errors, 25 warnings, elapsed 1:52.88.
- Documentation checks confirmed all six phases contain implementation target, dependencies, technical risks, test method and completion criteria.
- Requested file tree is complete. No DDCore plugin project exists yet by design.
- DDCore implementation intentionally not started.

## Deployment

- None. Documentation/research task only.

## Known evidence

- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` derives from `ZoneModule` and consumes `AIHints`, `ActionQueue`, `WorldState.DeepDungeon`, pathfinding, Dalamud IPC, and BossMod services.
- `BossMod/Data/DeepDungeonState.cs` models rooms, party, pomanders, chests, magicite, floor and passage/return progress.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs.
- Official Dalamud plugin restrictions state that automated combat/gameplay is not acceptable for the official plugin repository.

## Safe next step

After explicit user approval, reopen this file with `Status: in progress` and start only the approved Phase 2 foundation slice: solution skeleton, Observer-only lifecycle/config/UI, provenance files and tests. Do not enable game operation by default.
