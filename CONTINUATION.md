# DDCore continuation

Status: complete
Updated: 2026-11-07 JST

## Objective

既存の非公開GitHubリポジトリを使用し、BossMod公開ソースの再確認結果とPhase 1〜6の詳細な技術調査・開発計画を `docs/DDCore_Codex_MasterPlan.md` に統合する。今回はプラグイン本体を実装しない。

## Current state

- Repository: `https://github.com/natukimiyabi23-eng/DDCore` (private, default branch `main`)
- Local path: `work/DDCore`
- BossMod source: `https://github.com/awgil/ffxiv_bossmod`
- BossMod analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- BossMod license: BSD-3-Clause
- DD source files found: 63, including Global shared code, Palace of the Dead, Heaven-on-High, Eureka Orthos, and Pilgrim's Traverse.
- Source version: documentation only; plugin version not assigned.
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

## Unfinished

- No required research or design work remains for this objective.
- Plugin implementation remains intentionally unstarted and requires explicit user approval.
- User decisions remain for Pilgrim's Traverse scope, runtime BossMod independence, the first solo job/floor target, optional execution providers, distribution mode and replay-data redaction.

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

## Artifact hashes (SHA-256)

- `docs/DDCore_Codex_MasterPlan.md`: `D9082ADDFBB63539C00E93919C681C151677C0282B9E6F12CB54A2F58F86F18E`
- `docs/architecture.md`: `E189A91FD056C031E96D6939B22C571DF2740B33995850FE4497601EE65E88B7`
- `docs/dependencies.md`: `526925CAE96CB48A5DE45D9290E9FD7D56AD576177D2A57A3A4934EC2C4C1927`
- `docs/migration-plan.md`: `5F8D00245CBF6F4094DF1B8ECECFEF3534EA6601C38189BB3707AC8734958C82`
- `docs/research-report.md`: `5A5B76D5F92C9328D1EEBCE69B9D18D42F5BA27A20B10B007F2296D38CE7131F`
- `docs/progress.md`: `3CB50B0D3E8A69406DE6D3BBA398232222042E656FCAB20A8457DA82A0463160`

## Known evidence

- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` derives from `ZoneModule` and consumes `AIHints`, `ActionQueue`, `WorldState.DeepDungeon`, pathfinding, Dalamud IPC, and BossMod services.
- `BossMod/Data/DeepDungeonState.cs` models rooms, party, pomanders, chests, magicite, floor and passage/return progress.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs.
- Official Dalamud plugin restrictions state that automated combat/gameplay is not acceptable for the official plugin repository.

## Safe next step

After explicit user approval, reopen this file with `Status: in progress` and start only the approved Phase 2 foundation slice: solution skeleton, Observer-only lifecycle/config/UI, provenance files and tests. Do not enable game operation by default.
