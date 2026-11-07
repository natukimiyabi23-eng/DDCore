# Phase 3 mechanic accuracy review

## Scope and evidence

This review covers the game-independent Phase 3 engine on `phase3/mechanics-foundation`. The comparison baseline is BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`:

- [`D10Gancanagh.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod.Modules/Endwalker/DeepDungeon/EurekaOrthos/D10Gancanagh.cs)
- [`GenericAOEs.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Components/GenericAOEs.cs)
- [`AOEShapes.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/BossModule/AOEShapes.cs)
- [`Angle.cs`](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Util/Angle.cs)

No live Dalamud or FFXIV test was performed. Synthetic comparisons establish agreement with the pinned source behavior only; they do not establish live adapter accuracy.

## Gancanagh cross-check

| Item | Pinned BossMod | DDCore after review | Result |
|---|---|---|---|
| Boss OID | `0x3D54` | `BossObjectId = 0x3D54` | match |
| Mine OID | `0x3D55` | `PachypodiumMineObjectId = 0x3D55` | match |
| Authoritative Shriek | `31477`, boss self-cast, no AoE component | known non-hazard only when cast by boss | match and actor-scoped |
| Mandrashock Single | `31478`, radius 10 circle | radius 10 instantaneous cast prediction | match |
| Mandrashock Titan | `32700`, radius 10 circle, `maxCasts = 6` | radius 10, deterministic six-instance limit | behavioral match with ordering caveat |
| Mandrastorm | `31479`, component radius 18.5 | radius 18.5 instantaneous cast prediction | component match; live damage threshold unverified |
| Arena | center `(-300,-300)`, square half-size 19.5 | same constants | match; routing not implemented |

BossMod `StandardAOEs` stores casters in cast-start event order and displays the first `MaxCasts`. DDCore has snapshots rather than the original event stream, so it orders by `StartedAt`, then `FinishesAt`, entity ID and generation. This is deterministic and selects earlier casts before later casts, but exact ordering of genuinely simultaneous cast-start events cannot be compared without a live event replay.

The pinned Mandrastorm source comment says the action has range 60 while the component deliberately models radius 18.5 as the observed flat-damage threshold. DDCore matches the component's 18.5 value. The actual live falloff/damage boundary remains unverified and is not claimed to equal the action metadata range.

## Cast origin, rotation and coordinate contract

BossMod `StandardAOEs` uses `CastInfo.LocXZ`, `CastInfo.Rotation` and `CastFinishAt`. DDCore now uses `CastSnapshot.TargetLocation` when present, falls back to actor position only when no cast location was observed, uses cast rotation rather than actor rotation, and activates at `FinishesAt`.

DDCore geometry uses a conventional 2D coordinate system: zero radians points along positive X and positive rotation is counter-clockwise toward positive Y. BossMod `Angle.ToDirection()` maps zero to positive Z/Y and increasing angles toward positive X. An adapter must therefore convert with `Coordinate2D.FromClockwisePositiveYRotation`. No live adapter exists in Phase 3, so adapter conversion remains unverified.

## AoE lifecycle specification

### Instantaneous cast hazards

- A supported in-progress cast produces a predictive hazard immediately.
- `ActivatesAt` is the known cast finish.
- `Lifetime` is `Instantaneous` and `ExpiresAt == ActivatesAt`; equality is intentional and no unverified duration is fabricated.
- Explicit completion, interruption, death or destruction removes the prediction.
- If the caster disappears before activation, DDCore retains the last prediction and returns `UnknownOrUnsupported` with a deterministic diagnostic.
- Once the known activation instant is reached, the prediction expires even if the source remains missing. Encounter actors still being absent keeps the Gancanagh frame unknown rather than safe.

### Persistent hazards

- A persistent definition must provide a strictly positive, evidence-based duration.
- The hazard remains after cast completion until `FinishesAt + PersistentDuration`.
- A missing or non-positive duration is rejected. Gancanagh currently defines no persistent hazard because the pinned source provides no such duration.

## Ordering, identity and missing data

- Sequences must strictly increase.
- Capture times may remain equal but must not move backward.
- Actor identity is `(EntityId, Generation)`; reuse with a new generation cannot alias the old cast.
- Consecutive casts by the same identity use cast start ticks in the hazard ID and replace the prior prediction when explicitly observed.
- Partial, stale, unsupported or invalid observations do not produce a known-safe assessment.
- Analyzer exceptions are converted to an invalid frame and do not terminate the registry.
- The registry now preserves an analyzer's unknown assessment even when no `UnsupportedMechanic` item exists.

## Geometry review

Circle, cone, forward rectangle, donut, donut sector and cross behavior were compared with the pinned BossMod shape contracts. Tests cover translation, rotation, both radial/angular boundaries, zero-distance cone apex, configurable boundary tolerance, invalid dimensions and angles, NaN, infinity and very large finite coordinates. Distance magnitude now uses a scaled calculation to avoid intermediate overflow.

The current geometry comparison is analytic and synthetic. Bit-for-bit equality with BossMod's float-based SDF implementation is neither expected nor claimed; classification at the documented boundaries is tested instead.

## Replay determinism

- Replay and snapshot schemas are version 2 because cast rotation and hazard lifetime are now explicit.
- JSON parsing remains strict and bounded to 4 MiB, 10,000 snapshots, 512 actors per snapshot and 128 statuses per actor.
- Sequence regression, time regression and cross-session replay are rejected.
- Repeated analysis of the same synthetic Gancanagh replay produces the same ordered frame signatures.
- A synthetic one-frame actor gap retains the predicted AoE and reports unknown, then returns to known analysis when the same cast observation returns.

## Remaining risks and unverified items

1. No live FFXIV/Dalamud adapter or recorded Gancanagh encounter has been tested.
2. Mandrastorm's live damage/falloff boundary is not proven; only the pinned component radius 18.5 is reproduced.
3. Snapshot `StartedAt` ordering cannot reproduce BossMod event insertion order for casts with identical timestamps; entity ID and generation are deterministic tie-breakers.
4. Actor generation must be assigned correctly by the future live adapter for entity-ID reuse protection to work.
5. Cast target location and rotation must be populated and converted correctly by the future adapter.
6. BossMod uses single-precision geometry and DDCore uses double precision; exact SDF values are not compared.
7. No movement, route selection, combat, targeting, item use or game input exists in this phase.

