# Phase 3.7 diagnostic and observation recording

## Scope

Phase 3.7 adds a read-only diagnostic log and an explicit opt-in local observation recorder. It does not operate FFXIV, change targets, execute actions, use items, inject input, install hooks, or transmit diagnostic data.

No live FFXIV or Dalamud test was performed in this phase. The implementation and tests establish the local contracts only.

## Separation of responsibilities

Normal diagnostics and detailed recording are separate:

- `ObservationDiagnosticLog` is an in-memory, 256-entry ring buffer. `ObservationRuntime` adds session resets, state changes, hazard summaries and contained exceptions. Repeated identical states are not logged every tick. Errors are also sent to the existing `IDDLogger`/Dalamud log adapter.
- `ObservationRecordingService` is initially OFF and receives snapshots only after the user presses `記録開始`. It samples at most twice per second and writes a bounded local replay at most once every five seconds, plus a final flush when stopped.

The observer and analyzer keep running when recording is OFF. Recording failure stops only the recorder and does not turn an unknown observation into a safe result.

## Recording format

The stored file is the existing strict JSON `ReplayDocument` format and is validated by `ReplayCodec` before export. This keeps the file directly usable by deterministic replay tests.

Each snapshot contains:

- observation time, random session ID and sequence;
- Deep Dungeon/content/floor observation state and quality;
- enemy/event-object IDs, positions, rotations, HP and lifecycle flags;
- enemy casts, action IDs, predicted start/finish, target location when observable and cast lifecycle;
- statuses already present in `WorldSnapshot`;
- observation reason codes.

AoE shape, origin, rotation, activation prediction, safety result and unsupported reasons are deterministically regenerated from each snapshot by `AnalyzerRegistry`. The bounded normal diagnostic log also includes the latest hazard summary for immediate troubleshooting. The replay file does not duplicate derived `MechanicFrame` data, avoiding two potentially inconsistent sources of truth.

Strict replay limits from `ReplayCodec` still apply: 4 MiB maximum document size, 10,000 snapshots, 512 actors per snapshot, 128 statuses per actor, bounded diagnostics and strict unknown-field rejection.

## Phase 3.7 local limits

Defaults are deliberately lower than the codec maximum:

| Limit | Default | Behavior at limit |
|---|---:|---|
| Serialized size | 2 MiB | discard the sample that would exceed the limit, flush prior valid data, stop |
| Snapshots | 1,800 | flush valid data and stop |
| Recording time | 15 minutes | flush valid data and stop |
| Sample interval | 500 ms | faster observations are skipped |
| Flush interval | 5 seconds | atomic local replacement; stop/dispose also flushes |

Files are stored under the plugin configuration directory in `recordings`. An export is created under `recordings/exports` only after recording has stopped and the user checks the UI confirmation that the preview was reviewed. Delete requires a separate confirmation and is unavailable while recording.

File writes use a temporary file followed by same-directory replacement. A directory, write, stop-flush, export or delete failure is contained and shown as an error. A write failure changes the recorder to `Faulted` and prevents further samples.

## Session boundaries

Logout/login, territory change or current content-row change causes `LiveWorldSnapshotProvider` to create a new random session ID. An active recording stops before accepting the first snapshot of a different session. Zone transitions and logout therefore never mix sessions in one replay.

Plugin disposal performs one final bounded flush and stops the recording. Disposal remains idempotent.

## Privacy

The live source never reads chat text, account data or player names. Before a snapshot is recorded, `SnapshotPrivacyFilter` removes player and party-member actors and their numeric identities. Enemy/event-object references to removed actors are cleared. Status sources pointing to removed actors are also cleared.

The file retains only enemy/event-object numeric identities needed to correlate casts and hazards. It is local-only: DDCore contains no uploader, HTTP client, telemetry or automatic external transmission path.

The UI preview shows session/time range, count, size, enemy/cast/AoE counts, unsupported reason, save path and latest error before export. A random session ID is not an account or character identifier.

## Safety and performance

- `DisabledExecutionGateway` remains the only execution gateway.
- Observation and recording code has no target, action, item, key-state, input or native-memory service.
- Core and Observation remain independent of Dalamud and BossMod assemblies.
- Bounded collections, state-change logging, 2 Hz sampling and 5-second atomic flush intervals avoid per-frame allocation and disk-write loops.
- Malformed snapshots and storage exceptions never enable an operating mode or produce a known-safe assessment.

## Known limitations

1. Live disk latency and rendering impact have not been measured in FFXIV.
2. Actual plugin configuration paths and permissions have not been verified in a running Dalamud session.
3. A crash between flushes can lose at most the unflushed interval; prior atomic data remains valid.
4. AoE summaries in the normal diagnostic log are in memory and Dalamud log output; replay files store the source snapshot and regenerate analysis rather than duplicating derived frames.
5. Actual Gancanagh observations, privacy preview rendering and export/delete UI behavior remain unverified in game.
