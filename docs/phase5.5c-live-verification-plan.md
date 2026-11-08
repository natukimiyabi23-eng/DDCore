# Phase 5.5C: live verification plan

## Goal

Determine whether public Dalamud API 15 exposes a trustworthy, privacy-preserving exact-floor event for each supported Deep Dungeon. This plan does not authorize implementation or current-game operation. The user performs all live steps after a separately approved diagnostic build is provided.

## Prerequisites

- Keep `DisabledExecutionGateway` and Observer mode.
- Add only an opt-in, read-only diagnostic probe after implementation approval.
- Initial state is OFF; start/stop/export require explicit user actions.
- Record no sender, character/account identifier, ordinary chat, full message text or `SeString` bytes.
- Record the plugin/API/game-data versions and B1 kind/range so a capture is reproducible.

## Sanitized event record

For candidate events only, capture:

```yaml
observedAtUtc: timestamp
eventSequence: integer
eventSurface: LogMessage | ChatMessage
logMessageId: integer-or-null
logKind: numeric-and-known-name
parameterCount: integer-or-null
integerParameters: bounded index/value pairs for allowlisted candidate rows only
recognizedFloor: integer-or-null
b1DungeonKind: enum
b1FloorRange: start/end-or-null
quality: Unverified | Verified | Missing | Conflict | Stale
reasonCode: stable-code
```

The probe must copy `ILogMessage` fields inside the callback because the API states that object is callback-scoped. It must never invoke `PreventOriginal` or `FormatLogMessageForDebugging`.

## EO first capture

1. User enables the approved diagnostic probe before entering or changing floor.
2. Confirm B1 shows `オルト・エウレカ 1～10階帯` and exact floor remains Unknown.
3. On a known floor, note the visible floor number independently.
4. Use the passage normally and note the visible order of `転移が実行された！` and `地下N階`.
5. Stop/export the sanitized probe immediately after the new floor stabilizes.
6. Compare timestamps/sequences for candidate rows `7248`, `7270`, `9218`, `ChatMessage` log kind and B1 snapshots.
7. Repeat at least twice, including one transition within the same ten-floor band and one crossing a ten-floor band if safely available.

Acceptance for EO requires all of:

- a repeatable public event for the visible exact floor;
- stable row/type and integer parameter mapping;
- B1 range agreement;
- transfer invalidation before the new floor is published, or a documented conservative alternative;
- no ordinary/player chat captured;
- duplicate and event-order behavior understood.

The user-confirmed `地下9階` is the expected label for the first fixture, not proof that the callback result is row `7270`.

## Palace of the Dead and Heaven-on-High

Run the same independently labelled procedure once per dungeon and on more than one floor. Do not reuse EO's row mapping unless the captures prove it. Record unknown rows as candidates without exporting full text. Until then both dungeons remain unsupported for exact-floor chat recognition.

## Lifecycle matrix

| Scenario | Expected safe result |
|---|---|
| Plugin starts after the floor line | `Missing`, exact floor Unknown |
| Plugin reload on a floor | Previous value is not restored as current |
| Duplicate event | One effective observation, no extra epoch |
| Transfer marker before floor line | Previous value becomes `Stale`, then new verified value |
| Floor line before marker or reordered callbacks | No rollback; unresolved order remains Unverified/Conflict |
| Candidate missing | Exact floor remains Unknown; strategy stops |
| DD exit | Exact-floor state cleared |
| Re-entry | New session starts Missing |
| Disconnect/reconnect | Old value is never carried forward |
| B1 range mismatch | `Conflict`, floor null |
| Player says `地下9階` | Ignored and not stored |

## Test fixtures and pass criteria

After live capture, convert only sanitized records into committed synthetic fixtures. Original exports stay local and unmodified.

Pass criteria:

1. Pure parser/reconciler tests cover matching, malformed, duplicate, conflict, stale and out-of-order cases deterministically.
2. Exact floor is populated only for a verified event whose B1 range contains it.
3. Same inputs produce byte-for-byte equivalent normalized observations.
4. Missing/conflicting/stale inputs retain Phase 5 `Stop` and `Risk=Unknown`.
5. Schema 3/4/5 compatibility tests remain green; a future persisted evidence model uses an explicitly approved schema migration.
6. Release build, all tests, format, dependency/vulnerability and prohibited-operation audits pass.
7. Live logs contain no player identity or raw chat content.

## Failure and recovery

- If no candidate event arrives, report public-event acquisition as unavailable for that case; do not substitute UI/native reads.
- If parameter type/index varies, keep the row Unverified and collect another sanitized sample.
- If a player channel can satisfy the filter, reject the design until the channel/source gate is corrected.
- If transition invalidation cannot be made reliable, exact floor remains diagnostic-only and must not feed strategy.
- Disable/unload the diagnostic build to return to the pre-test state; it makes no game-state changes.

## User actions needed after implementation approval

The user must supply the independently known dungeon/floor and perform normal floor transitions. DDCore should provide a Japanese checklist and a sanitized export preview. No instruction should ask the user to disclose ordinary chat or identifying data.

## Remaining decisions

1. Approve a diagnostics-only `IChatGui.LogMessage`/`ChatMessage` probe.
2. Decide whether verified exact-floor provenance remains runtime-only or later enters a schema-6 replay.
3. Decide the minimum evidence count per dungeon/locale before enabling its allowlist.

No production recognition should be enabled until decision 1 is approved and its live evidence passes the criteria above.
