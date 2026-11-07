# Phase 3.7 verification

## Scope

This report covers the game-independent diagnostic and recording implementation on `phase3/diagnostics-and-recording`. It does not claim live Dalamud loading, UI rendering, FFXIV observation, performance, or Gancanagh encounter verification.

## Verification matrix

| Check | Result |
|---|---|
| Release build | Passed, 0 warnings, 0 errors |
| Full unit test suite | Passed, 159/159 (125 Core + 34 Observation) |
| Format verification | Passed |
| NuGet vulnerability audit | No known vulnerable package |
| Static BossMod/Dalamud dependency audit | Passed; Core/Observation independent, Plugin uses API 15 |
| Static prohibited-operation/external-communication scan | Passed; no prohibited source matches |
| GitHub Actions | Push and Draft PR runs passed |

## Implemented test coverage

- explicit start/stop and initial OFF;
- duplicate-start rejection;
- sample, snapshot, byte and duration limits;
- directory/write failures and fail-stop behavior;
- session/territory/logout transitions;
- replay round-trip compatibility and strict validation;
- player/party identity removal;
- preview-confirmed export and confirmed delete;
- disposal flush and resource release;
- bounded normal diagnostics, error capture and repeated-state suppression.

Package `0.3.1.0` targets Dalamud API 15. The package contains Core, Observation and Plugin assemblies, BSD notice and provenance, with no BossMod assembly. SHA-256: `C9129D7C5B6A6D0BF88A0FB416BB14EDD2102D94118D7364D8C5E465A376F954`.

Implementation commit: `478ab76`. Draft PR: <https://github.com/natukimiyabi23-eng/DDCore/pull/4>, targeting `phase3/live-observation-adapter`.

Push run `42385808420` and Draft PR run `42385836623` both passed build, 159 tests and package upload. Both runs contain the existing `actions/upload-artifact@v4` Node.js 20 deprecation annotation while GitHub forces Node.js 24; the annotation did not fail either job.

Live checks remain assigned to the user procedure in `docs/phase3-live-test-procedure.md`.
