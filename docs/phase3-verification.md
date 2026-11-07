# Phase 3 verification

## Scope

This record covers P3.0 through P3.5 on `phase3/mechanics-foundation` and the Phase 3.6 observation work on its child branch. It does not claim live Dalamud loading, in-game rendering, movement, targeting, combat, item use, input injection or full Deep Dungeon automation.

## Implemented

- P3.0: schema versions, explicit support/observation states, BossMod provenance manifest, BSD-3-Clause notice and manifest validation.
- P3.1: pure C# signed-distance/containment models for circle, cone, forward rectangle, donut, donut sector and cross, with translation and rotation.
- P3.2: immutable versioned `WorldSnapshot`, Deep Dungeon context, actors, casts, statuses, sequence/time/source/quality and validation.
- P3.3: deterministic JSON replay, explicit capture/export intent, a 4 MiB document limit, collection limits, strict fields/enums, ordering checks and a mock snapshot builder.
- P3.4: `IMechanicAnalyzer`, `AnalyzerRegistry`, immutable analyzer state, `HazardInstance`, `MechanicFrame`, cast-hazard lifecycle and fail-closed unknown/invalid results.
- P3.5: Eureka Orthos floor 10 Gancanagh analyzer and synthetic golden replay tests.
- P3.6: public-Dalamud-API read-only observation, normalized conversion, generation/cast/session tracking, analyzer connection and Japanese diagnostics.

Phase 3.6 changes nullable unknown territory/content/status-stack representation and therefore advances `WorldSnapshot` and replay schemas to version 3. `MechanicFrame` remains version 2.

## Gancanagh support

Pinned upstream: BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`, `BossMod.Modules/Endwalker/DeepDungeon/EurekaOrthos/D10Gancanagh.cs`.

| Upstream action | ID | DDCore behavior |
|---|---:|---|
| MandrashockSingle | 31478 | radius-10 circle at the cast location; instantaneous activation/expiry at cast finish |
| MandrashockTitan | 32700 | radius-10 circles; deterministic six-instance limit ordered by observed cast start |
| Mandrastorm | 31479 | radius-18.5 circle at the boss |
| AuthoritativeShriek | 31477 | known non-hazard activation cast; no hazard fabricated |
| Unknown encounter cast | any other | explicit unsupported mechanic; frame is not treated as known/safe |

Tests cover simultaneous AoEs, cast interruption/completion, missing/dead/destroyed actors, elapsed and consecutive casts, actor-ID generation reuse, target location/rotation, unknown actions, snapshot gaps and deterministic replay output. Arena constants are retained for provenance and future bounded projection, but Phase 3 does not calculate a route or perform movement. Detailed source comparison and unresolved differences are recorded in `docs/phase3-accuracy-review.md`.

## Provenance and reimplementation

No BossMod assembly or package is referenced. DDCore geometry, snapshots, replay and analysis infrastructure are original code. Gancanagh action/object IDs, radii, arena values and the Titan six-instance limit are behavior/data reimplemented from the pinned BossMod file. Their destination and derivation type are recorded in `provenance/bossmod-phase3.json`; the BSD-3-Clause text is reproduced in `THIRD-PARTY-NOTICES.md`.

## Verification commands

```powershell
dotnet restore DDCore.slnx --locked-mode
dotnet build DDCore.slnx -c Release --no-restore
dotnet test DDCore.slnx -c Release --no-build --no-restore
dotnet format DDCore.slnx --verify-no-changes --no-restore
dotnet list DDCore.slnx package --vulnerable --include-transitive
```

Final command results and package hash are recorded in `CONTINUATION.md` after the full verification run.

## Accuracy-review verification

- Release build: succeeded with 0 warnings and 0 errors.
- Unit tests: 125 passed, 0 failed, 0 skipped.
- Format verification: succeeded.
- NuGet vulnerability audit: no known vulnerable package in Core, Plugin or tests.
- Static dependency audit: Core has no project reference and its assembly test reports no BossMod or Dalamud reference; Plugin references only Core and `Dalamud.NET.Sdk/15.0.0`.
- Package: `DDCore` version `0.2.1.0`, Dalamud API level 15.
- Package contents: Core/plugin assemblies, manifest, BSD notice and Phase 3 provenance; no BossMod assembly.
- Package SHA-256: `BB37604E72600FF97743B38F75E7268ACAB96AE62ECFE360771C27FB3331837E`.

## Fail-closed behavior

- No matching analyzer: unknown, not safe.
- Partial/stale/unsupported observation: unknown, not safe.
- Invalid observation, sequence/time regression or analyzer exception: invalid, not safe.
- Missing encounter actor or a missing pre-activation caster: the prediction is retained where possible and the frame is unknown, not safe.
- Instantaneous hazards use equal activation/expiry timestamps intentionally; persistent hazards require a positive evidence-based duration.
- Unknown Gancanagh action: unsupported, not safe.
- Malformed, oversized, out-of-order or cross-session replay: rejected.
- Replay capture/export without explicit user intent: rejected.

## Not verified

- Live Dalamud loading and Japanese UI rendering for version 0.2.1.0.
- Live FFXIV state acquisition or adapter accuracy.
- Actual encounter timing/positions against a recorded live Gancanagh session.
- Official Dalamud repository acceptance.
- Any gameplay operation; none is implemented in Phase 3.

The user performs plugin enablement, reload and in-game verification. Replay and unit tests are not reported as live-game verification.

Phase 3.6 field availability, derivations and remaining public-API gaps are recorded in `docs/phase3-live-observation.md`. Final Phase 3.6 command results, package hash, commit and PR are recorded in `CONTINUATION.md` only after the final verification run.

## Phase 3.6 verification

- Locked restore: succeeded for all five projects.
- Release build: succeeded with 0 warnings and 0 errors.
- Unit tests: 140 passed (125 Core + 15 Observation), 0 failed, 0 skipped.
- Format verification: succeeded.
- NuGet vulnerability audit: no known vulnerable package in any project.
- Static dependency audit: Core and Observation have no BossMod or Dalamud reference; Plugin references Core, Observation and `Dalamud.NET.Sdk/15.0.0`.
- Source scan: no FFXIVClientStructs, interop provider, signature scanner, target manager, key state, address, send-action or execute-command use under `src`.
- Package: `DDCore` version `0.3.0.0`, Dalamud API level 15; includes Core, Observation, Plugin, BSD notice and provenance, with no BossMod assembly.
- Package SHA-256: `139CE2C81C8DD4857ED6AA65B48797A452D14CDC9FBE09C0121948B5C8561C14`.
- GitHub Actions: push run `42363700322` and Draft PR run `42363704906` both passed the full solution build/test/package job.
- CI annotation: `actions/upload-artifact@v4` still targets deprecated Node.js 20 and was forced to Node.js 24 by GitHub; this did not fail the runs.
- Live Dalamud loading, UI rendering and Gancanagh observation remain unverified.

## Phase 3.7 verification

- Added a 256-entry, change-only in-memory diagnostic log and a separate explicit-start local recorder.
- Recording is OFF at startup and bounded to 2 MiB, 1,800 snapshots, 15 minutes, 2 Hz sampling and five-second atomic flushes.
- The persisted format is the existing strict `ReplayDocument`; derived AoE frames are regenerated deterministically rather than duplicated.
- Player/party actors and references are removed before persistence. Names, chat and account data are not observed, and no network/export service exists.
- Storage failure, session/territory/logout transitions, limit exhaustion and plugin disposal stop or flush the recorder without enabling operation.
- Japanese UI adds working start, stop, status, preview-confirmed local export and confirmation-gated delete controls.
- Release build: succeeded with 0 warnings and 0 errors.
- Unit tests: 159 passed (125 Core + 34 Observation), 0 failed, 0 skipped.
- Format verification: succeeded.
- NuGet vulnerability audit: no known vulnerable package in any project.
- Static dependency audit: Core and Observation have no BossMod/Dalamud dependency; Plugin references Core, Observation and Dalamud API 15 only.
- Static prohibited-operation/communication scan found no client structs, interop/signature scanner, target/key/action/command, `System.Net`, HTTP, socket, Discord, webhook or upload use under `src`.
- Package: `DDCore` version `0.3.1.0`, Dalamud API 15, no BossMod assembly.
- Package SHA-256: `C9129D7C5B6A6D0BF88A0FB416BB14EDD2102D94118D7364D8C5E465A376F954`.
- Live Dalamud loading, UI rendering, local path permissions, performance and Gancanagh recording remain unverified. Follow `docs/phase3-live-test-procedure.md` manually.
