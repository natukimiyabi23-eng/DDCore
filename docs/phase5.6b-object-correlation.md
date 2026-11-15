# Stage 5.6B: Selected Object Correlation

## Scope

This stage adds an opt-in, read-only diagnostic correlation between the current public Dalamud target and an object already observed in the same framework sample. It does not classify object meaning, confirm that a chest was opened, build room topology, feed BFS/strategy, or issue game operations.

## Observation contract

`DalamudEnvironmentObjectDiagnosticSource` reads `ITargetManager.Target` while handling a framework update and copies only value fields before returning: `GameObjectId` and `ObjectIndex` for transient in-memory matching, `ObjectKind`, `BaseId`, position, rotation, targetability, and validity. It retains no `IGameObject` reference. The target is correlated against the already-filtered `IObjectTable` observations only when runtime ID, object index, kind, and BaseId all match exactly and exactly one candidate exists.

The transient runtime ID/index are never part of the serialized records. A successful match exports only the recording-session anonymous ID, correlation quality, copied public fields, coordinate quality, and a fixed reason code. No match, duplicate candidate, unsupported target kind, or unavailable identity does not select a nearest or same-BaseId substitute. Coordinate quality is independent of identity correlation: an exact identity match may still have `Missing` or `Quarantined` coordinates.

The API contract is the Dalamud API 15 public `ITargetManager.Target` property and `IGameObject` properties; implementation uses no native memory, signature scan, private API, chat, or name field. Runtime API behavior remains subject to user-run live verification.

## Transition contract

Each sample may report `TargetabilityChanged`, `Disappeared`, or `Reappeared`. Disappearance has reason `environment-object.disappeared-not-open-confirmed`; reappearance is based only on a transient runtime-key returning after a sampled gap and uses `environment-object.runtime-key-returned-continuity-unknown`. The returned object receives a fresh anonymous ID. This intentionally does not assert that it is the same physical object: object-ID reuse, range changes, and identity continuity cannot be ruled out from this evidence.

Transient recently-absent runtime keys are memory-only and capped by the configured maximum sample count. Session or confirmed exact-floor changes clear active and recently absent tracking. Stop/start creates a new recording session and clears all tracking.

## JSON and compatibility

New exports use the independent environment-object diagnostic schema 2. Schema 1 remains accepted by the compatibility reader/test; ordinary replay schemas 3/4/5 and chat-floor diagnostic schema 2 are untouched. The JSON contains no raw runtime IDs, names, chat content, character/account fields, or stable cross-session actor key. Records remain local, initial-OFF, explicitly started/stopped, and bounded by the existing 512 KiB / 1,000 samples / 15 minute limits.

## Verification

Automated tests cover exact match, mismatch, duplicate ambiguity, missing target coordinates, targetability transitions, disappearance and return after a gap, fresh anonymous identity on return, raw-ID exclusion, and schema-1 document compatibility. Release build/test/format/audit and CI results are recorded in `CONTINUATION.md` and `docs/progress.md` as they are completed.

The fixed replay export folder contained 12 valid replay files when inspected read-only (schema 3: 412 snapshots; schema 4: 886; schema 5: 869; total 2,167). The latest replay by filesystem modification time was schema 5 / producer 0.5.3.0 with 66 snapshots. SHA-256 was unchanged before and after each replay read. Replay actor data did not contain the two reported BaseIds. This was not the full set of original diagnostics, however: a separate recursive read of the local `recordings` tree found 11 schema-1 environment diagnostic files with four unique content hashes; all hashes were unchanged after reading. These diagnostics were producer 0.5.6.0 and contained the following transitions, derived from consecutive object samples (schema 1 stores no transition array):

| User-provided label | JSON evidence | What the JSON does not prove |
|---|---|---|
| EO floor 1, reported first chest / BaseId `2007357` | The 200-sample diagnostic created `2026-11-15T22:05:31.563+09:00` records targetable `true -> false` at sample 25 (`22:05:37.690+09:00`); last observed at sample 43 (`22:05:42.309+09:00`) as false; absent in samples 44-200. | It does not contain the screen selection, chest-open event, or proof this BaseId is the first physical chest. Those are user labels, not JSON fields. |
| EO floor 1, reported second chest / BaseId `2007358` | The 160-sample diagnostic created `2026-11-15T22:09:55.603+09:00` records targetable `true -> false` at sample 79 (`22:10:15.563+09:00`); last observed at sample 99 (`22:10:20.664+09:00`) as false; absent in samples 100-160. | It does not contain the screen selection, chest-open event, or proof this BaseId is the second physical chest. Those are user labels, not JSON fields. |

This corrects the earlier draft statement that the observations were not present in accessible JSON: they were absent from the 12 replay payloads but present in the separate environment diagnostic exports. Other schema-1 exports contain repeated `2007358` observations under more than one anonymous ID, so BaseId alone is not a unique object identity. These old files do not contain selected-target correlation; Stage 5.6B's exact same-sample runtime match is the missing evidence to collect in a new opt-in recording. A screen-to-BaseId mapping remains unverified until that user-run live test.

## Stage 5.6B live evidence and completion assessment

For DDCore 0.5.7.0/schema 2, local original diagnostics were re-read read-only after the user reported an EO B1 test. Relevant observations:

- BaseId `2007358`: `ddcore-environment-objects-20261115-135052.diagnostic.json`, created `2026-11-15T22:50:52+09:00`, contains 69 consecutive `exactRuntimeMatch` selected-object samples for `obj-000005` (samples 74-142), with `usable` coordinate quality. Another recording has 118 exact matches for the same BaseId and serialized anonymous label, but this is not treated as cross-recording identity. A separate 44-sample run records a disappearance for BaseId `2007358` at different coordinates; that does not prove it is the selected chest. The user reports manual opening of the selected chest; this is not inferred from disappearance.
- BaseId `2013286`: schema-2 recording created `2026-11-15T22:53:32+09:00` contains 80 exact matches for `obj-000002` (samples 23-102), with `usable` coordinate quality, while a screenshot showed the visible label “再生装置”. Screenshot/JSON association relies on the user's supplied operation/time correlation. Activation-state recognition was not tested.
- BaseId `2007357`: `ddcore-environment-objects-20261115-135426.diagnostic.json` has 37 exact selected-object samples for `obj-000001`. In the separate 42-sample `...135441...` run, `obj-000001` is targetable at sample 1, changes true to false at sample 20, and has a `disappeared` transition at sample 40; same-BaseId `obj-000003` remains observed through sample 42. This demonstrates object-level distinction in that recording, not semantic classification by BaseId.

| Completion item | Status | Boundary |
|---|---|---|
| Public target and exact same-sample correlation | Confirmed | Reported schema-2 samples contain exact matches. Screenshot-to-record alignment depends on the user's time/action labels. |
| Anonymous distinction for concurrent same-BaseId objects | Confirmed | `obj-000001` and `obj-000003` are distinct within the recording. No cross-recording identity is claimed. |
| Coordinate quality | Confirmed for reported `2007358` selection | `usable` was reported for those matching samples; automated tests cover missing-coordinate behavior. |
| Targetability/disappearance reporting | Confirmed for reported `2007357` run | Sample 20 change and sample 40 disappearance were reported. Neither means opened. |
| Manual-open causal mapping | Partially confirmed | User reports manual opening and later disappearance; that sequence does not prove causality. |
| Reproducer visible-label association | Partially confirmed | Screenshot shows “再生装置”; JSON association depends on user-supplied timing. |
| Reproducer activation-state detection | Unverified | No activation-state detector is part of this stage. |
| Exact floor, floor transition, re-entry, other DDs | Unverified | `exactFloor` remains null; no broader behavior is claimed. |

**Stage 5.6B implementation and the scoped B1 live checks are complete.** Broader continuity, semantic classification, other Deep Dungeons, and automatic reproducer-state detection remain unverified/out of scope. Original JSONs and screenshots were not added to the repository.
