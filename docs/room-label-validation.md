# Offline room-label validation

## Purpose and limits

`LuminaMapProbe --validate-room-labels` compares a small user-authored set of screenshot-confirmed room labels with the static `planmap.lgb` room-box candidates and positions in one schema-5 replay. It is an offline evidence tool only. It does not discover the active randomized layout, infer a room from unlabeled data, identify passage state, establish walkability, or provide BFS input. The coordinate bounds remain uncalibrated; a match is only containment under the current candidate-box convention.

The labels file is not game-produced. A human must identify the exact floor, visible room cell, Room A/B variant if known, and evidence source. Do not create labels from the candidate output itself. If any value is uncertain, do not label that sample. This keeps the check from circularly confirming its own assumptions.

## Labels format

Create a local labels JSON file with schema version 1. The aggregate evaluator report uses schema version 2. Bind labels to the exact replay bytes and exact `planmap.lgb` bytes using SHA-256; this prevents accidentally applying labels to a different recording or static map revision. The regular probe output's `assetSha256` is the `planmapSha256` value. `snapshotIndex` is the zero-based position in that replay's `snapshots` array. Only the following evidence values are accepted:

- `userScreenshot`: a screenshot that independently shows the exact floor and room location corresponding to the selected replay sample.
- `userConfirmed`: the user explicitly confirmed the sample-to-room correspondence.

Example structure (synthetic values only; not a DDCore observation):

```json
{
  "schemaVersion": 1,
  "replaySha256": "<64-character SHA-256 of the replay file>",
  "planmapSha256": "<assetSha256 from the regular probe output>",
  "labels": [
    {
      "snapshotIndex": 12,
      "exactFloor": 2,
      "variant": "B",
      "roomIndex": 17,
      "evidence": "userScreenshot"
    }
  ]
}
```

Room indices are row-major 0..24 in the typed 5x5 grid. The labels JSON rejects unknown properties, duplicate snapshot indices, unsupported evidence kinds, unsupported variants, and labels pointing to a cell absent from the extracted static candidates. It does not accept names, game object IDs, session IDs, chat, screenshots, or free-text notes.

## Run

From the repository root, with a local Lumina assembly directory, local SqPack root, one schema-5 replay, and the labels JSON:

```powershell
$replay = "<path to one local replay>"
Get-FileHash -Algorithm SHA256 $replay
dotnet run --project tools/LuminaMapProbe/LuminaMapProbe.csproj -c Release -- `
  "<Lumina.dll directory>" "<SqPack directory>" `
  --validate-room-labels $replay "<path to local labels.json>"
```

The computed replay digest and extracted planmap digest must match `replaySha256` and `planmapSha256`. Output contains those two local content hashes plus aggregate match, ambiguity, alternate-candidate, unmatched, invalid-pose, untrusted-pose, and context-conflict counts and a canonical summary hash; it omits the replay path, sample timestamps, coordinates, and per-sample labels. Treat hashes as local correlation tokens; keep the local label file and replay private and do not commit either.

## Interpretation

- **Matched expected candidate**: exactly one extracted box contains the recorded position, and it is the user-labelled variant/cell.
- **Ambiguous candidate**: the expected box contains the position, but another extracted box also contains it.
- **Other candidate**: one or more boxes contain the position, but none is the labelled variant/cell.
- **Unmatched**: no candidate box contains the position under the current uncalibrated bounds convention.
- **Invalid pose**: the sample has no finite position; it is not scored as geometry mismatch.
- **Untrusted pose**: the position is finite, but pose metadata does not identify both `source=publicDalamud` and `quality=direct`; it is excluded from match rate. Missing metadata fails closed.
- **Context conflict**: dungeon kind or recorded floor-band/exact-floor conflicts with the user label; it is excluded from match rate.

The match rate denominator includes only labels with compatible DD/floor context, finite position, and direct public-Dalamud pose provenance. It is not an accuracy claim for room identification unless the human labels are independent, multiple layouts are represented, and coordinate conventions are separately validated. A green command exit code means only that valid nonempty labels were evaluated; it does not mean the map model passed a feasibility gate.
