# Phase 5 real-data validation

## Scope and handling

This verification reads the user's local Phase 3.7 exports directly from
`%APPDATA%\XIVLauncher\pluginConfigs\DDCore\recordings\exports`. The two source JSON files were not changed, copied into the repository, or committed. The report contains aggregate counts only; actor identifiers and diagnostic suffixes are omitted.

The user supplied `ddcore-export-20261107-180939-e7bd7997efe34218aadad2a978beb81e.replay.json` as the ground-truth label for the floor 3 to floor 4 period. The export itself does not contain either floor number or a floor-transition event, so this label is not independently derivable from the replay.

## Detected exports

| File | Schema / producer | Snapshots | Observation interval (UTC) | Duration | SHA-256 |
|---|---:|---:|---|---:|---|
| `ddcore-export-20261107-174659-1318b1174a30426295c0805456e30c53.replay.json` | 3 / 0.4.5.0 | 288 | 2026-11-07 17:43:11.7658736 to 17:45:43.2030056 | 151.437 s | `D2A6DBAD273EF46285759C9FCE43A630F014294B19D169F12937D20B4BD938E6` |
| `ddcore-export-20261107-180939-e7bd7997efe34218aadad2a978beb81e.replay.json` | 3 / 0.4.5.0 | 124 | 2026-11-07 18:08:32.0215849 to 18:09:38.4902620 | 66.469 s | `ED9809FF3066874C10A79150CBE75541F301C95674AC335DF1FE080057D0683D` |

Both documents passed the production `ReplayCodec` schema, size, collection and monotonic-order validation. Each document contains one session. Both retain territory ID 1099 but record every snapshot as dungeon kind `Unsupported`, floor `null`, and content key `duty-unidentified`.

## Aggregate observations

| Measurement | Earlier export | Labelled candidate |
|---|---:|---:|
| Actor snapshots / unique actors | 3,778 / 28 | 1,187 / 21 |
| HP-decrease transitions | 105 | 42 |
| Alive-to-dead/destroyed transitions | 4 | 2 |
| Dead/destroyed actor snapshots | 74 | 36 |
| Cast snapshots | 57 | 13 |
| Cast action IDs | 32382 (18), 32383 (39) | 32381 (13) |
| Status snapshots / distinct status IDs | 27 / 1 | 19 / 1 |
| Actor snapshots at `(0,0)` | 2,044 | 552 |
| Diagnostics / distinct diagnostics | 1,608 / 14 | 656 / 11 |

The candidate diagnostics explicitly state that the public API did not supply DD floor, cast location/rotation, or status stack count. Cast timing and some target locations/rotations were derived by the live adapter. Because `(0,0)` can be either a real coordinate or a missing-value encoding in this schema, the validator records the count but does not classify those positions as valid or absent.

## Phase 5 conversion and decisions

`WorldSnapshotStrategyAdapter` maps only direct observations. Session, sequence, timestamp, observed dungeon enum and a non-null floor can be transferred. Current room, the 25-room graph, exit, enemy-to-room membership, traps, pomanders, aggregate resource readiness and remaining time stay Unknown. Raw enemy evidence is counted for diagnostics but is not assigned to a fabricated room.

| Result | All exports | Labelled candidate |
|---|---:|---:|
| Snapshots accepted by Phase 5 input conversion | 412 | 124 |
| Conversion/evaluation exceptions | 0 | 0 |
| Actionable decisions (`CanProceed`) | 0 | 0 |
| Fail-closed `Stop`, risk `Unknown` | 412 | 124 |
| Mechanic frames `UnknownOrUnsupported` | 412 | 124 |
| Detected hazards | 0 | 0 |

Representative decision:

- Action: `Stop`
- Risk: `Unknown`
- Reason: required observations are missing.
- Blockers: floor, current room, room graph, enemy list with room membership, traps, pomanders, resource readiness, remaining time and exit discovery.

No unsafe proceed result was observed. A correctness misclassification rate cannot be calculated because the exports contain no expected-action labels, room truth, exit truth or hazard truth. Therefore this report does not claim that zero general misclassifications occurred.

The three cast action IDs do not match the fixed Gancanagh analyzer's supported IDs, and the snapshots do not identify Eureka Orthos floor 10. The existing registry therefore returns `UnknownOrUnsupported`; it does not treat an empty hazard list as proof of safety.

## Verified and unverified outcomes

Verified:

- HP changes, explicit dead/destroyed flags, casts and statuses survive replay decoding.
- All 412 valid snapshots can enter the Phase 5 adapter and engine without an exception.
- Missing required strategy fields produce a deterministic fail-closed decision.
- Sequence and time order pass replay validation.

Not verified:

- The floor 3 to floor 4 transition cannot be detected from these exports.
- Enemy room membership, room topology, exit state, trap state, pomander state and time remaining cannot be reconstructed.
- `(0,0)` cannot be distinguished from a valid origin without a position-availability marker.
- There are no expected decisions against which to calculate an action-level error rate.

## Required follow-up before Phase 6

1. Capture authoritative DD kind and floor, or add an explicit user-labelled session/floor boundary record.
2. Add nullable/quality-qualified actor position instead of overloading `(0,0)`.
3. Observe or derive a validated room identifier and full room connection graph.
4. Observe exit, traps, pomanders, remaining time and player resource readiness.
5. Add expected-decision labels or a separately reviewed oracle for real-data accuracy measurement.

Phase 6 is not ready on this evidence. Phase 5 itself behaves safely with incomplete inputs, but the live observation contract cannot yet supply the state required for an actionable strategy decision.
