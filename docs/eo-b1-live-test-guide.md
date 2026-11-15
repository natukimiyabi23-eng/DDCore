# EO B1 Read-Only Data Collection Guide

Updated: 2026-11-16

## Goal

Collect independent labels needed to test whether static Room A/B candidates correspond to an actual EO floor. This is a manual evidence-collection procedure. DDCore does not navigate, fight, interact, select targets, or infer open passages. Do not treat the current candidate-box output as a route.

## Before a run

1. Record the DDCore version and confirm the plugin is in Observer/read-only mode. The main window must continue to show game-operation features as unimplemented/disabled and the emergency-stop control as available.
2. Use the normal replay recorder only if you want to preserve pose samples. It is local and bounded; start and stop it manually. Do not include an unredacted replay or screenshot in GitHub.
3. Keep screenshots and any label file in a private local folder. The screenshots may show character names, chat, party members, or other personal information; crop/redact before sharing. The repository needs only aggregate, anonymized results.
4. Do not enable an unreviewed memory reader, addon parser, input feature, or third-party runtime integration to collect missing map fields.

## At each observation point

While standing still in the ordinary course of play, manually capture the visible game map and the exact floor text. Do not send any command or interact with doors for this test. For each screenshot, note:

- replay snapshot index or a nearby timestamp that can be matched to one snapshot;
- exact floor shown by the game (not only the 1-10 band);
- which static layout family (A/B) is independently supported by visible geometry; leave it unknown if not clear;
- room grid index only if the displayed map gives an unambiguous correspondence;
- the player's visible room/position and the evidence source;
- each visible adjacent passage as Open, Closed, or Unknown, with a screenshot reference kept locally;
- whether the point is near a room boundary or is in a corridor, so unmatched positions are not discarded.

Take samples in more than one room and, if practical, after naturally occurring floor transitions. Do not deliberately perform gameplay actions for this data request. A missing screenshot, unclear floor, uncertain layout, or uncertain room must remain unlabeled rather than guessed.

## Offline room-candidate comparison

The existing labels evaluator accepts schema 1 with these fields:

```json
{
  "schemaVersion": 1,
  "replaySha256": "<SHA-256 of the exact replay file>",
  "planmapSha256": "<parsed planmap data hash from the probe output>",
  "labels": [
    {
      "snapshotIndex": 12,
      "exactFloor": 2,
      "variant": "A",
      "roomIndex": 7,
      "evidence": "userScreenshot"
    }
  ]
}
```

`variant` must be `A` or `B`; `roomIndex` is 0-24 in the existing row-major 5x5 convention. `evidence` is `userScreenshot` or `userConfirmed`. The replay and planmap hashes bind labels to exact inputs. The sample above is illustrative only; do not use it as a real label.

Run the offline probe with the same local Lumina/SqPack inputs documented in `docs/phase5.6c-lumina-extraction-verification.md`: `dotnet run --project tools/LuminaMapProbe -c Release -- <Lumina.dll-directory> <sqpack-directory> --validate-room-labels <replay.json> <labels.json>`. The evaluator reports matched, ambiguous, other-candidate, unmatched, invalid/untrusted pose, and context-conflict counts. A good aggregate match rate is not sufficient by itself: inspect boundary cases and require multiple different layouts before promoting any classifier.

## Connection labels and route limits

The current labels schema does not encode or validate Open/Closed passage edges. Keep edge observations in a private manual checklist; do not convert them into BFS input. A route preview requires a separate reviewed, versioned graph dataset and tests proving 25-node coverage, in-bounds reciprocal edges, known current room/destination, and freshness. Unknown or conflicting edges must prevent route output.

## Safe stop and cleanup

- Stop the replay recorder from the DDCore UI when the observation period ends.
- Keep original replay JSON read-only. Do not edit, rename, move, or commit it.
- If an export is shared for analysis, first remove identifiers and unnecessary fields; preserve the original locally.
- If the UI reports invalid observation, unknown context, or diagnostic failure, record only the fixed reason code and stop that diagnostic. Do not infer a room or continue using stale data.
- This guide authorizes no in-game validation by Codex and no automatic action. The user remains in control of ordinary gameplay and any future test.

## Acceptance target for the next data review

For at least two distinct user-confirmed layouts, obtain exact-floor and room labels that can be aligned to replay snapshots, plus independently visible edge-state labels. The static candidate matcher must preserve ambiguous and unmatched samples; a false room classification is a failure. This still validates only observation/classification. It does not authorize a live route or movement feature.
