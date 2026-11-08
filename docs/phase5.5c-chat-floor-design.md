# Phase 5.5C: chat exact-floor design

## Scope and safety boundary

This is an implementation design, not implemented functionality. It preserves read-only observation, `DisabledExecutionGateway`, B1/B2 behavior and the Phase 5 fail-closed strategy gate. It never sends/suppresses/modifies chat, stores ordinary chat, reads private structures, or performs game input.

## Recommended pipeline

```text
IChatGui.LogMessage / IChatGui.ChatMessage
    -> callback-local primitive envelope
    -> bounded ordered queue
    -> Japanese/structured floor parser
    -> B1 kind/range reconciliation
    -> exact-floor state + floor epoch invalidation
    -> WorldSnapshot fusion (only Verified may populate Floor)
```

The source callback copies only row ID, typed primitive parameters, `LogKind`, callback timestamp and an internal sequence. It must not retain `ILogMessage`, `IChatMessage`, sender, payload objects or full message text. A bounded queue is drained at the observation/framework boundary so parsing and snapshot mutation remain deterministic.

## Proposed components

| Component | Responsibility |
|---|---|
| `IExactFloorEventSource` | Emits normalized read-only candidates; Dalamud types remain in the plugin adapter |
| `DalamudChatFloorEventSource` | Subscribes/unsubscribes to public chat events and copies minimal primitives only |
| `ChatFloorCandidateParser` | Parses an allowlisted structured row or a separately verified locale-specific display candidate |
| `ExactFloorReconciler` | Checks DD context, range, session, ordering and conflicts |
| `ExactFloorStateTracker` | Tracks quality, last observation, transition invalidation and idempotent duplicates |
| `DeepDungeonObservationFusion` | Populates `DeepDungeonContext.Floor` only for a current `Verified` result |

Subscription must be registered after all dependencies are constructed and immediately paired with cleanup in the existing stack. Constructor failure and `Dispose` must both remove it exactly once.

## Model

Names should follow the existing observation-quality vocabulary during implementation, but the required semantics are:

```csharp
public sealed record ExactFloorObservation(
    int? Floor,
    ExactFloorSource Source,
    ExactFloorQuality Quality,
    DateTimeOffset? ObservedAtUtc,
    long? EventSequence,
    Guid? SessionId,
    string? ReasonCode);

public enum ExactFloorQuality
{
    Verified,
    Unverified,
    Missing,
    Conflict,
    Stale,
}
```

| Quality | Meaning | May populate `DeepDungeonContext.Floor` |
|---|---|---|
| `Verified` | Allowlisted system/log event, valid value, current session and B1 kind/range agree | Yes |
| `Unverified` | Floor-shaped candidate exists but row/type/parameter/locale trust is not established | No |
| `Missing` | No candidate has been observed for the current scope | No |
| `Conflict` | Candidate contradicts B1 range/context or a same-epoch trusted observation | No |
| `Stale` | A transition/reset/age boundary invalidated a previous result | No |

Stable reason codes should include `floor.chat.missing`, `floor.chat.row-unverified`, `floor.chat.parameter-invalid`, `floor.chat.out-of-range`, `floor.chat.band-conflict`, `floor.chat.session-mismatch`, `floor.chat.transition-invalidated`, `floor.chat.stale`, and `floor.chat.unsupported-locale`.

## Parsing and trust rules

### Structured route (preferred)

1. Accept only a live-verified allowlist entry containing the exact `LogMessageId`, expected parameter count/type/index and supported dungeon applicability.
2. Copy the integer through `TryGetIntParameter` during the callback. Do not call `FormatLogMessageForDebugging`; API documentation warns that it can cause side effects.
3. Reject missing, additional/ambiguous, non-integer, zero, negative or above-dungeon-maximum values.
4. Reconcile the result with current B1 `DeepDungeonKind`, `IsDeepDungeonObserved == true`, and `FloorRange.Contains(floor)`.
5. A row ID alone or row `7270` before live parameter verification yields `Unverified`, not `Verified`.

### Display-text route (secondary)

For the first Japanese experiment only, the grammar candidate is anchored to the entire normalized visible text: `^地下([1-9][0-9]{0,2})階$`. Full-width digits, alternate whitespace, punctuation and other languages are not accepted until observed. Parsing text alone never produces `Verified`; it additionally requires a live-confirmed system `LogKind`, non-player origin characteristics, current B1 evidence and correlation with a structured row or an explicitly approved text-only policy.

Use `OriginalMessage`, not a possibly plugin-modified current message. The implementation phase must verify the exact safe API used to flatten a `ReadOnlySeString`; this design does not assume an unconfirmed helper name.

Ordinary player channels (`Say`, party, tell, linkshell, free company, shout, yell and similar) are rejected before text parsing. Sender names and message bodies are never logged.

## B1 reconciliation

The following is the only successful shape:

```text
B1 kind = EurekaOrthos
B1 range = 1-10
candidate = 9
candidate source/type = live-verified
session/epoch = current
=> Floor 9, Verified
```

`candidate = 25` with range `1-10` produces `Conflict`, leaves `DeepDungeonContext.Floor` null and must not replace B1. Unknown DD/content, absent DD condition, unsupported locale or an unverified row produces Unknown-equivalent behavior.

## Lifecycle and invalidation

| Event | Result |
|---|---|
| DD entry, plugin start or reload | `Missing`; never reconstruct from old chat history |
| Trusted exact-floor event | Reconcile; establish/update current floor epoch only if `Verified` |
| Duplicate same row/value/session | Idempotent; do not create another epoch |
| Trusted different floor in current band | New epoch after reconciliation; invalidate all previous floor-scoped data |
| Verified transfer-start marker | Immediately mark the old exact floor `Stale`; wait for a new floor event |
| B1 kind/range/content/session changes | Clear current exact floor and invalidate floor-scoped state |
| DD condition false, logout/disconnect | `Missing` and clear state |
| Conflicting candidates | `Conflict`; no value is exposed |
| Out-of-order older event | Reject by event sequence/session; do not roll state backward |

The existing DDCore session key catches login, territory, content ID and DD-condition changes, but not ordinary same-band transitions. A verified transfer marker (candidate row `7248`) can close that gap. If it is missed, chat-only observation cannot guarantee that a previously seen floor is still current. Therefore implementation must add a bounded freshness/invalidation policy validated against real floor durations, or keep the value unavailable to strategy after any ambiguous transition signal. No duration is selected in this design without live evidence.

Floor-epoch invalidation clears room graph, current room, exploration, exit/passage, traps and floor-scoped enemies/resources before strategy evaluation. A chat observation must not directly mutate strategy state.

## Replay/schema compatibility

Schema 3 and 4 remain readable through existing migration, and schema 5 remains the current B2 format. Two implementation choices require later approval:

1. Keep exact-floor evidence runtime-only initially. No schema change; replays continue to show exact floor only through the existing `DeepDungeonContext.Floor` value, without provenance.
2. Persist the full source/quality/epoch evidence. This changes the replay contract and should use schema 6 while retaining schema 3/4/5 readers.

The second option is recommended for reproducible live verification, but it is **not** authorized in Phase 5.5C design work. Raw chat, sender and full `SeString` payloads are excluded from every schema.

## Privacy and diagnostics

Allowed persisted fields are: recognized floor candidate, row ID, numeric chat/log kind, quality, reason code, internal sequence, observation time and current B1 kind/range. Do not persist sender, source/target entity, full text, payload bytes, chat history, account/character identifiers or unrelated parameters.

Normal diagnostics should count accepted/rejected candidates by reason. An opt-in live verification mode may display the sanitized metadata above, but must not export raw chat. External transmission remains prohibited.

## Planned tests

- Structured row with verified integer parameter and matching B1 range becomes `Verified`.
- Japanese `地下9階` parser returns candidate `9`; partial/extra text is rejected.
- Player channel containing `地下9階` is rejected before parsing.
- Unknown row/type, malformed value and unsupported locale remain `Unverified`/`Missing`.
- Floor `25` against range `1-10` becomes `Conflict`.
- Duplicate messages are idempotent; reordered/old sequence cannot roll back state.
- Transfer marker invalidates old floor before the next exact-floor event.
- Reload, logout, DD exit, content/range/session change and disconnect clear/invalidate state.
- Missing event and stale observation preserve Phase 5 `Stop` / `Risk=Unknown`.
- Schema 3/4/5 decoding remains unchanged; any future schema 6 gets explicit migration tests.
- Subscription is removed on normal dispose and partial initialization failure.
- Tests assert no `PreventOriginal`, chat-printing, raw-chat persistence or execution gateway change exists.

## Approval boundary

Implementation requires separate approval. It should be split into: (1) sanitized diagnostic probe, (2) pure parser/reconciler and tests, (3) live-verified allowlist, and only then (4) optional snapshot/replay integration. A visual line alone is not enough to skip the probe.
