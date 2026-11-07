# DDCore continuation

Status: complete
Updated: 2026-11-08 05:48 JST (Phase 5 DD state and strategy engine complete; Draft PR review pending)

## Objective

Current objective: implement the user-defined Phase 5 on `phase5/strategy-engine`, based on `phase4.5/bossmod-integration-proof`: add a read-only, deterministic Deep Dungeon state model and strategy engine that preserves unknown observations, uses the Phase 4 `RoomPathfinder`, returns recommendations with reasons/preconditions/risk/blocking information, and is covered by synthetic tests and full verification. This user-defined numbering intentionally treats Phase 5 as the decision-engine phase even though the original master plan labels decision logic as Phase 4 and operation infrastructure as Phase 5. Do not add gameplay execution, BossMod runtime dependency, Phase 6 work, live FF14 operation, or a `main` merge.

Current objective: implement Phase 4.5 on `phase4.5/bossmod-integration-proof`: prove separately whether DDCore can reach BossMod's fixed-commit IPC surface and whether that surface provides the Deep Dungeon strategy data DDCore needs. Add only a read-only, optional, fail-closed diagnostic probe when supported by the public Dalamud IPC mechanism; do not add BossMod runtime references, private hooks, gameplay operation, Phase 5 work, deployment, live FF14 operation, or a `main` merge. Record source-line evidence, comparison, verification, and create a stacked Draft PR targeting `phase4/reuse-analysis`.

Current objective: implement Phase 4 on `phase4/reuse-analysis`: quantitatively analyze the pinned BossMod Deep Dungeon strategy code, map dependencies and required inputs to DDCore `WorldSnapshot`, classify direct reuse/dependency removal/reimplementation, extract the smallest viable pure C# algorithm module, compare it against the pinned implementation where executable, measure reuse using explicit definitions, and re-estimate Phase 5-8 scope/tokens. Preserve Phase 3.7, do not implement Phase 5 operation, do not merge to `main`, and create a Draft PR targeting `phase3/diagnostics-and-recording`.

Current objective: implement Phase 3.7 on `phase3/diagnostics-and-recording`: bounded local diagnostics and explicit opt-in observation recording, Japanese diagnostic controls, replay-compatible export, privacy safeguards, tests, documentation, verification, and a Draft PR targeting `phase3/live-observation-adapter`. Preserve read-only operation, `DisabledExecutionGateway`, no external communication, no live FF14 operation, and no main merge.

Current objective: implement Phase 3.6 on `phase3/live-observation-adapter`: a read-only, public-Dalamud-API-15 observation adapter that converts observable game state into `WorldSnapshot`, feeds the existing analyzer registry, exposes Japanese diagnostics, and remains fail-closed. No game operation, input injection, private API access, deployment, live verification, or main merge is authorized.

Phase 2ブランチを基点に `phase3/mechanics-foundation` を作成し、ゲーム操作を伴わないP3.0〜P3.5（来歴管理、幾何計算、WorldSnapshot、JSONリプレイ/モック、解析基盤、ガンカナグ解析）を段階実装・検証してPhase 2ブランチ向けPRを作成する。mainマージと実機操作は行わない。

現在の追加目的: 固定BossModコミットとPhase 3解析基盤を精度照合し、AoEライフサイクル、幾何、時系列、安全側処理、決定論的リプレイの不具合を修正してDraft PR #2を更新する。

## Current state

- Repository: `https://github.com/natukimiyabi23-eng/DDCore` (public, default branch `main`)
- Working branch: `phase5/strategy-engine`
- Local path: `work/DDCore`
- BossMod source: `https://github.com/awgil/ffxiv_bossmod`
- BossMod analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- BossMod license: BSD-3-Clause
- DD source files found: 63, including Global shared code, Palace of the Dead, Heaven-on-High, Eureka Orthos, and Pilgrim's Traverse.
- Source version: `0.5.0.0` (Phase 5 local and CI verification complete).
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

- No required Phase 5 implementation or verification step remains.
- User review and merge decisions for Draft PR #7 remain pending; do not merge it or begin Phase 6 without explicit approval.
- Live Dalamud/FFXIV behavior and real-floor strategy quality remain outside this phase and must not be reported as verified.

## Phase 5 checkpoint

- Started: `2026-11-08T05:30:26+09:00`.
- Authoritative preflight: five-hour window 86% remaining; weekly window 67% remaining.
- Estimated requirement: 48% total (research 6%, implementation 17%, tests/audits 9%, documentation/PR 6%, contingency 10%); projected five-hour balance `86% - 48% = 38%`.
- Branch: `phase5/strategy-engine`, created from clean Phase 4.5 head `07b3fd2c00cc39a0c4f2b7c59c8728345fad9764`.
- Draft PR #6 remains open and clean, targeting `phase4/reuse-analysis`; it is not merged.
- Five-hour continuation automation: `ddcore-phase-5-followup`, scheduled once for `2026-11-08 10:30:26 JST`, created and verified `ACTIVE`. Purpose: inspect the active goal, this file, Git state, build/test state, and remaining usage, then resume only the first unfinished step or report completion without rerunning work.
- No source implementation has been edited at this checkpoint.
- Implemented immutable Unknown-preserving strategy state for dungeon/floor, 25 rooms, exit, enemies, traps, pomanders, aggregate resources and remaining time.
- Implemented deterministic, read-only strategy decisions for fail-closed stop, danger retreat, combat continuity, resource conservation, active exit, exploration and exit search. Safe routes exclude known/unknown-danger rooms, traps and non-defeated enemies.
- Reused only the attributed Phase 4 `RoomPathfinder`; no BossMod runtime reference or operation adapter was added.
- Added 30 Phase 5 Core tests; the final Core suite passes 169/169 and Observation passes 40/40 (209 total).
- Added Phase 5 design documentation, numbering reconciliation and package version `0.5.0.0`.
- Locked restore and final Release build passed with 0 warnings/errors; all 209 tests passed; format, NuGet vulnerability, project/reference and prohibited-operation audits passed.
- Package `0.5.0.0`, API 15, SHA-256 `558A4A8E6C688BEEE3D9D199D93EFD0DFD80C06BCA6A9636EA503C147278BD0A`.
- Final diff review, commit/push and Draft PR creation completed.
- Implementation commit `b46f020` and documentation commit `8148741` were pushed. Draft PR #7 targets `phase4.5/bossmod-integration-proof`: `https://github.com/natukimiyabi23-eng/DDCore/pull/7`.
- CI did not initially trigger because the workflow branch filters ended at the Phase 4.5 push branch and Phase 4 PR base. Commit `1b0e938` added only the Phase 4.5 PR base and Phase 5 push branch filters.
- Final push run `42412810484` and Draft PR run `42412814600` both passed at verified head `bc2e5dc`. The existing `actions/upload-artifact@v4` Node.js 20 deprecation annotation did not fail either run. The final continuation-only commit uses the CI skip marker to avoid recursively spawning another identical verification run.
- Final authoritative usage checkpoint after CI and documentation closeout: five-hour window 48% remaining; weekly window 61% remaining.

- Live Dalamud/FFXIV validation remains outside this phase and must not be reported as successful.
- User review and merge decisions remain pending. Do not merge Draft PR #7 or start Phase 6 without explicit approval.

## Phase 4 checkpoint

- Start condition approved by the user using the authoritative account display: five-hour window 84% remaining and weekly window 82% remaining. Exact remaining token count is not exposed; estimated requirement is 70,000-95,000 tokens including contingency.
- Created `phase4/reuse-analysis` from clean `phase3/diagnostics-and-recording` at `8ebc76b`; Draft PR #4 remains open/Draft with successful CI.
- BossMod checkout is intentionally left on its current `master`; fixed commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0` exists locally and will be read with commit-qualified Git commands.
- Reproducible fixed-commit measurement: 67 files / 28,609 physical lines (63 DD-direct paths plus two support and two cross-cutting files); lexical dependency-family definitions are explicit in `tools/Measure-Phase4BossMod.ps1`.
- Analyzed all eight requested components by source line and mapped exploration, combat, recovery, resources, hazards and action-selection inputs to current `WorldSnapshot` gaps.
- Added pure-C# `RoomPathfinder`, strict 5x5 graph validation, a source-faithful BossMod oracle and deterministic/all-pairs comparison tests. No live adapter or executor consumes it.
- Added Phase 4 provenance, BSD notice, reuse measurements (1/8 portable after dependency removal; 56/2,249 reviewed-line extraction coverage) and Phase 5-8 planning estimates.
- Locked restore and final Release build passed with 0 warnings/errors; all 173 tests passed (Core 139, Observation 34); format and NuGet vulnerability audit passed.
- Core has no project references. Static matches for BossMod are attribution/comments only; no BossMod/Dalamud/Lumina/FFXIVClientStructs runtime dependency was introduced into Core/Observation.
- Package `0.4.0.0`, API 15, SHA-256 `2EDB2C5F48E0BC15992379CC0B1DEFAA83B9EB02FE7F7A44E7EF1E39D03CDB5F`; includes both provenance manifests and `THIRD-PARTY-NOTICES.md`.
- Implementation commit `0d07b68` and this completion record were pushed to `phase4/reuse-analysis`.
- Draft PR #5 targets `phase3/diagnostics-and-recording`: `https://github.com/natukimiyabi23-eng/DDCore/pull/5`.
- Both push and Draft PR `build-and-test` runs passed (runs `42388673278` and `42388678876`).
- Exact next step: user reviews Draft PR #5. Do not merge or start Phase 5 without explicit approval.

## Phase 4.5 checkpoint

- Authoritative preflight at start: five-hour window 38% remaining and weekly window 75% remaining. Estimated remaining work 30% plus 4% contingency; projected five-hour balance `38% - 34% = 4%`.
- Created `phase4.5/bossmod-integration-proof` from clean `phase4/reuse-analysis` at `497278d`; Draft PR #5 remains the Phase 4 base.
- Fixed BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0` registers 37 `BossMod.*` IPC functions in `BossMod/Services/IpcService.cs` through Dalamud's IPC provider mechanism.
- The fixed IPC surface exposes configuration, rotation status, preset management and obstacle-map operations/status, but no active mechanic/AoE DTO, `AIHints`, safe position, action recommendation, `DeepDungeonState`, rooms/passages/resources or target-priority data.
- Current upstream still contains the IPC provider but the file was moved/renamed and 10 of the 37 fixed-commit endpoints were removed, demonstrating an unversioned compatibility risk.
- Exact unfinished step: add a read-only diagnostic probe for invocation reachability only, explicitly report protocol version and DD strategy-data capability as unavailable, test absent/success/error paths, then document and verify. Do not claim useful DD integration from reachability alone.
- Implemented the optional probe in Observation/Plugin, added Japanese UI diagnostics and five tests. Observation tests pass 39/39 and the first full Release build passed with 0 warnings/errors.
- Added `docs/phase4.5-bossmod-integration.md` and the verification scaffold. Five-hour balance after this milestone: 29%; weekly balance: 73%.
- Exact unfinished step: run the locked full verification matrix, record final counts/hash, review the diff, commit/push, open the stacked Draft PR and wait for CI.
- Final local verification passed: locked restore, Release build (0 warnings/errors), 179 tests (Core 139 + Observation 40), format and NuGet vulnerability audit.
- Static audit found no BossMod assembly/package reference and no new movement, targeting, action, item, hook or client-struct operation. Package `0.4.5.0`, API 15, SHA-256 `6D61D5E0CBD30E75126F419B1869B85B800CFE9732E4945A3189904D7C1016A4`.
- Exact unfinished step: final diff review, commit/push, open the stacked Draft PR against `phase4/reuse-analysis`, wait for CI, then record PR/check results and mark complete.
- Implementation commit `fcb9602` was pushed. Draft PR #6 targets `phase4/reuse-analysis`: `https://github.com/natukimiyabi23-eng/DDCore/pull/6`.
- Added CI coverage for the Phase 4.5 stacked branch. Push run `42392652552` and Draft PR run `42392656475` both passed at head `cc0acb8`.
- Phase 4.5 conclusion: public IPC proves optional reachability only. The fixed 37-function surface supplies none of the seven required DD strategy-data categories, so independent migration remains recommended unless BossMod adds a versioned read-only DD snapshot contract.
- Exact next step: user reviews Draft PR #6. Live IPC invocation remains unverified. Do not merge PR #6, operate FF14, or start Phase 5 without explicit user approval.

## Phase 3.7 checkpoint

- Created `phase3/diagnostics-and-recording` from clean `phase3/live-observation-adapter`; Draft PR #3 and both Phase 3.6 CI checks were confirmed successful before branching.
- Implemented bounded in-memory diagnostics, explicit local recording, strict ReplayCodec export, 2 MiB/1,800 snapshot/15 minute limits, 2 Hz sampling, five-second atomic flushes, safe fault stop and session-boundary stop.
- Added a privacy filter that removes player/party actors and references before persistence. No names, chat, account fields, uploader or network client were added.
- Added Japanese start/stop/status/preview/export/delete UI and package version `0.3.1.0`.
- Added recording, storage failure, zone/logout, privacy, replay, disposal and diagnostics tests. Observation tests pass: 34/34; Core tests pass: 125/125; total: 159/159.
- Added `docs/phase3-diagnostics-recording.md`, `docs/phase3-live-test-procedure.md` and the pending final verification report.
- Final local verification: locked restore succeeded; Release build succeeded with 0 warnings/errors; 159 tests passed; format passed; NuGet audit found no known vulnerabilities; static dependency/prohibited-operation scan passed.
- Package `0.3.1.0`, API 15, SHA-256 `C9129D7C5B6A6D0BF88A0FB416BB14EDD2102D94118D7364D8C5E465A376F954`; contains Core/Observation/Plugin, BSD notice and provenance, with no BossMod assembly.
- Implementation commit `478ab76` was pushed and Draft PR #4 was opened against `phase3/live-observation-adapter`: `https://github.com/natukimiyabi23-eng/DDCore/pull/4`.
- Push run `42385808420` and Draft PR run `42385836623` both passed the full build/test/package job. The existing `actions/upload-artifact@v4` Node.js 20 deprecation annotation did not fail either run.
- Phase 3.7 changed `.github/workflows/phase2-build.yml`, `CONTINUATION.md`, `README.md`, Phase 3 architecture/progress/verification documents, added the recording specification and live-test procedure, added `ObservationDiagnostics.cs`/`ObservationRecording.cs`, integrated runtime/plugin/UI controls, and added/expanded Observation tests.
- Safe next step: review Draft PR #4 and perform `docs/phase3-live-test-procedure.md` manually if desired. Do not merge to `main` without explicit user approval and do not infer live behavior from unit tests.

## Phase 3.6 checkpoint

- Branch created from clean `phase3/mechanics-foundation`: `phase3/live-observation-adapter`.
- PR #1 and PR #2 confirmed open and Draft; PR #2 targets `phase2/plugin-foundation`.
- Usage remaining at start: 82%; estimated work: 30-40%.
- Exact next step: inspect the local Dalamud API 15 assembly/XML documentation and official sources, then record which requested fields are public, unavailable, or derived before writing the adapter.
- Public API audit completed against local `Dalamud.dll` 15.0.3.9/XML and official API 15 docs. Directly available: login/territory, duty CFC, object identities/base IDs/position/rotation/HP/validity/target/combat flags, cast action/target/progress/total, and status ID/source/remaining time. Not public: cast ground location, cast-specific rotation, explicit status stack count, and Deep Dungeon floor.
- Added `DDCore.Observation`, public-API Plugin adapters, 10 Hz framework polling, generation/cast/session tracking, partial-observation fail-closed analysis, and Japanese diagnostics UI. No write service, target manager, input, hook, or client-struct access is used.
- Final local verification: locked restore succeeded; Release build succeeded with 0 warnings/errors; 125 Core tests and 15 Observation tests passed (140 total); format passed; NuGet audit found no known vulnerabilities; static read-only/dependency scan passed.
- Package `0.3.0.0`, API 15, SHA-256 `139CE2C81C8DD4857ED6AA65B48797A452D14CDC9FBE09C0121948B5C8561C14`; contains Core/Observation/Plugin, BSD notice and provenance, with no BossMod assembly.
- Commit `4a8db4d` pushed and Draft PR #3 opened against `phase3/mechanics-foundation`: `https://github.com/natukimiyabi23-eng/DDCore/pull/3`.
- CI correction commit `ae4d4fb` covers the Phase 3 base/head and full solution tests.
- Both push run `42363700322` and Draft PR run `42363704906` passed. The only annotation is the existing `actions/upload-artifact@v4` Node.js 20 deprecation notice while GitHub forces Node.js 24; it did not fail either run.
- Draft PR #3 remains unmerged and targets `phase3/mechanics-foundation`: `https://github.com/natukimiyabi23-eng/DDCore/pull/3`.
- Usage remaining after implementation checkpoint: 57% in the five-hour window and 93% in the weekly window.


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
- Phase 4 package `0.4.0.0`: `2EDB2C5F48E0BC15992379CC0B1DEFAA83B9EB02FE7F7A44E7EF1E39D03CDB5F`
- No artifact was deployed.

## Known evidence

- `BossMod.Modules/Global/DeepDungeon/AutoClear.cs` derives from `ZoneModule` and consumes `AIHints`, `ActionQueue`, `WorldState.DeepDungeon`, pathfinding, Dalamud IPC, and BossMod services.
- `BossMod/Data/DeepDungeonState.cs` models rooms, party, pomanders, chests, magicite, floor and passage/return progress.
- `BossMod/Framework/WorldStateGameSync.cs` reads `InstanceContentDeepDungeon` through FFXIVClientStructs.
- Official Dalamud plugin restrictions state that automated combat/gameplay is not acceptable for the official plugin repository.

## Safe next step

Review Draft PR #5 and `docs/phase4-reuse-analysis.md`. Live room/resource observation remains unresolved. Do not merge to `main`, start Phase 5, operate FF14, or infer live behavior from offline tests without explicit user approval.
