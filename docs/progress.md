# Progress

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
