# Phase 3 verification

## Scope

This record covers P3.0 through P3.5 on `phase3/mechanics-foundation`. It covers game-independent mechanic analysis only. It does not claim live Dalamud state acquisition, in-game UI rendering, movement, targeting, combat, item use, input injection or full Deep Dungeon automation.

## Implemented

- P3.0: schema versions, explicit support/observation states, BossMod provenance manifest, BSD-3-Clause notice and manifest validation.
- P3.1: pure C# signed-distance/containment models for circle, cone, forward rectangle, donut, donut sector and cross, with translation and rotation.
- P3.2: immutable versioned `WorldSnapshot`, Deep Dungeon context, actors, casts, statuses, sequence/time/source/quality and validation.
- P3.3: deterministic JSON replay, explicit capture/export intent, a 4 MiB document limit, collection limits, strict fields/enums, ordering checks and a mock snapshot builder.
- P3.4: `IMechanicAnalyzer`, `AnalyzerRegistry`, immutable analyzer state, `HazardInstance`, `MechanicFrame`, cast-hazard lifecycle and fail-closed unknown/invalid results.
- P3.5: Eureka Orthos floor 10 Gancanagh analyzer and synthetic golden replay tests.

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
dotnet test tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj -c Release --no-build --no-restore
dotnet format DDCore.slnx --verify-no-changes --no-restore
dotnet list DDCore.slnx package --vulnerable --include-transitive
```

Final command results and package hash are recorded in `CONTINUATION.md` after the full verification run.

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
