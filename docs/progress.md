# Progress

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
