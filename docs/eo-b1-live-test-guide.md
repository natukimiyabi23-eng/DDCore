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

The offline probe accepts a separate, user-authored schema-3 graph label file. The file must enumerate all 25 room rows and all 40 canonical orthogonal passage rows, but rows may remain Unknown. To generate any route, it also requires replay and extracted planmap SHA-256 values, a context screenshot SHA-256, exact floor, A/B family, replay snapshot index, and current room. Every `userScreenshot` row must reference its own screenshot SHA-256 and a replay `evidenceSnapshotIndex`; `userConfirmed` rows are explicit user assertions and have neither value. Unknown rows use `notObserved` and have neither value. The tool binds the file to replay and planmap hashes, verifies the selected snapshot is EO with compatible floor band and direct public pose, checks the current pose maps to exactly one Room A/B candidate matching the labeled current room, and confirms every screenshot hash exists in a local evidence directory. Each screenshot-backed row's replay snapshot must independently have compatible EO floor context, trusted direct public pose, and a unique static candidate in the graph's selected A/B family. This is context consistency only: no pixel content, screenshot timestamp, room label, or passage state is automatically verified.

The top-level fields are `schemaVersion`, `replaySha256`, `planmapSha256`, `evidenceSha256` (context screenshot hash), `snapshotIndex`, `exactFloor`, `variant`, `currentRoom`, `rooms`, and `passages`. Screenshot-backed room rows include `evidenceSha256` and `evidenceSnapshotIndex`, for example `{ "room": 0, "state": "present", "evidence": "userScreenshot", "evidenceSha256": "<sha256>", "evidenceSnapshotIndex": 12 }`; passage rows work the same way with canonical endpoints. The indexed replay snapshot must exist and its direct public pose must uniquely support the graph's chosen A/B family. Room state is `present|absent|unknown`; passage state is `open|closed|unknown`. Unknown rows require `evidence: "notObserved"` and null hash/index. Room indices are row-major 0-24; the passage rows must contain each horizontal/vertical adjacent pair exactly once. A partial example is not a valid graph file.

To avoid hand-authoring the structure, create an Unknown-only template from an existing schema-5 EO replay and selected direct-pose snapshot: `dotnet run --project tools/LuminaMapProbe -c Release -- <Lumina.dll-directory> <sqpack-directory> --create-manual-room-graph-template <replay.json> <snapshot-index> <new-output.json>`. The output path must not already exist. The template has null floor/layout/start/evidence fields and all room and passage states set to Unknown; it cannot yield a route until those fields are independently established and every row is labeled. Exact floor and screen evidence are not inferred from the replay.

The evaluator uses only edges explicitly labeled Open and requires both endpoint rooms to be explicitly Present. Unknown edges are never traversed; a path using known-open edges may be reported as `offline-partial-route` even when unrelated graph rows remain Unknown. Unknown room/passage counts are included in the result. Such a path is only the shortest path in the currently known-open subgraph; unknown edges may contain a shorter route. If no known-open path exists while labels remain Unknown, the result is `incomplete`, not proof that no route exists. A complete, fully labeled graph can return `offline-route` or `no-route`. Example invocation: `dotnet run --project tools/LuminaMapProbe -c Release -- <Lumina.dll-directory> <sqpack-directory> --validate-manual-room-graph <graph.json> <replay.json> <destination-room-index> <private-evidence-image-directory>`. The directory is read-only: the tool hashes supported local PNG/JPEG/WebP/BMP files (maximum 256 files, 50 MiB each, and 250 MiB total), checks the extension against the image signature, and prints no file paths or image contents. It confirms referenced bytes exist and match their hashes, but does not decode or interpret image semantics; label-to-image meaning remains a user assertion. Both route results have `isOfflineOnly: true`; they are not live routes, do not validate within-room walkability, collision, traps, or dynamic blockers, and must never be consumed by the plugin or used to move the character. Missing evidence, incompatible floor, invalid pose, ambiguous box candidate, or stale input hash prevents a route.

Keep graph labels and screenshots local unless scrubbed and explicitly reviewed. Do not treat a route from illustrative/synthetic labels as a real EO route. A graph result does not promote M0 to PASS or authorize live BFS integration.

## Safe stop and cleanup

- Stop the replay recorder from the DDCore UI when the observation period ends.
- Keep original replay JSON read-only. Do not edit, rename, move, or commit it.
- If an export is shared for analysis, first remove identifiers and unnecessary fields; preserve the original locally.
- If the UI reports invalid observation, unknown context, or diagnostic failure, record only the fixed reason code and stop that diagnostic. Do not infer a room or continue using stale data.
- This guide authorizes no in-game validation by Codex and no automatic action. The user remains in control of ordinary gameplay and any future test.

## Acceptance target for the next data review

For at least two distinct user-confirmed layouts, obtain exact-floor and room labels that can be aligned to replay snapshots, plus independently visible edge-state labels. The static candidate matcher must preserve ambiguous and unmatched samples; a false room classification is a failure. This still validates only observation/classification. It does not authorize a live route or movement feature.
