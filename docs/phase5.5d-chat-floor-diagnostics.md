# Phase 5.5D — Chat floor event diagnostics

## Result and scope

Phase 5.5D adds an explicit opt-in, read-only diagnostic probe for candidate Deep Dungeon floor notifications. It does not establish the exact current floor and does not feed any candidate into `WorldSnapshot`, `DeepDungeonStrategyState`, or the strategy engine. `DisabledExecutionGateway` is unchanged.

The callback-fix plugin package is version `0.5.4.2`. Recording is OFF on startup. The user must start and stop it in the Japanese UI. No chat is sent, modified, suppressed, or retained in full.

### 0.5.4.1 live failure and 0.5.4.2 correction

The user reproduced a failure in Eureka Orthos while moving from floor 1 to floor 2 with diagnostics enabled. The recorder remained at zero entries and zero bytes, then faulted with `chat-floor.log-callback-failed:InvalidOperationException`. This is user-supplied live evidence; it does not establish the row ID or parameter mapping.

Source review identified a deterministic pre-record defect in `DalamudChatFloorDiagnosticSource.OnLogMessage`: typed integer parameters were accumulated into an `ImmutableArray<T>.Builder` and finalized with `MoveToImmutable()`. The .NET contract throws `InvalidOperationException` when builder `Count` differs from `Capacity`. A log message with fewer retained integer parameters than the builder capacity therefore faults before `recorder.Record`, which explains the observed zero-entry state without requiring any assumption about the floor-event mapping.

Version `0.5.4.2` uses a bounded, testable capture helper and `ToImmutable()` instead. Sparse typed parameters and empty/negative counts have regression coverage. If another callback exception occurs, the recorder now reports one of these fixed, privacy-safe stages rather than the former undifferentiated reason:

- `chat-floor.log-callback-status-failed`
- `chat-floor.log-callback-id-failed`
- `chat-floor.log-callback-parameter-count-failed`
- `chat-floor.log-callback-parameter-read-failed`
- `chat-floor.log-callback-context-failed`
- `chat-floor.log-callback-record-failed`

The exception message, chat body, sender, payload, and player identifiers remain excluded. Exact-floor candidates remain diagnostic-only and are not connected to replay, world state, strategy, or execution.

## Verified API contract

The adapter uses only installed Dalamud API 15 public contracts:

- `IChatGui.LogMessage` supplies callback-scoped `ILogMessage`; the adapter copies only `LogMessageId` and at most eight integer parameters before returning.
- `ILogMessage.TryGetIntParameter` is used instead of formatting the message. `PreventOriginal` and `FormatLogMessageForDebugging` are never called.
- `IChatGui.ChatMessage` supplies `IHandleableChatMessage`; only `XivChatType.SystemMessage` (`57`) is considered.
- `OriginalMessage.ExtractText()` is inspected only inside the callback and discarded immediately. An exact Japanese match of `^地下([1-9][0-9]{0,2})階$` contributes only the parsed integer.
- Both events are unsubscribed during plugin disposal.

The original Phase 5.5C sources are pinned to installed Dalamud `15.0.3.9`, product commit `3e47d4fa819d794a3213d81ad7a341114b3ba363`, and game-data commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`. The 0.5.4.2 correction was rechecked against the currently installed API 15 product commit `17cd86dfacbc1016c6e5aac0d49833b774ce037c`: `ILogMessage.ParameterCount` reads the callback-scoped native count, and `TryGetIntParameter` returns `false` for a non-integer parameter. The public API does not promise any particular floor row or integer position.

### Offline contract review (2026-11-10)

The maintenance-window review rechecked the installed XML contract and matching public source:

- [`IChatGui.cs:13-57`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Plugin/Services/IChatGui.cs#L13-L57) says `ChatMessage` fires for a message sent to chat by the game and `LogMessage` fires for chat based on `LogMessage` sheet entries. It does not promise that every visible floor line uses either candidate row or that both surfaces fire.
- [`LogMessage.cs:24-92`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Chat/LogMessage.cs#L24-L92) exposes row ID, parameter count and typed access. Parameters and the event object are callback-scoped. The adapter copies at most eight integer values and retains no API object.
- [`ChatGui.cs:524-580`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Gui/ChatGui.cs#L524-L580) raises `LogMessage` while draining the native queue and suppresses a repeated callback for queue entries surviving the prior update. This reduces one internal duplicate mechanism but does not establish cross-surface uniqueness or ordering.
- [`ChatGui.cs:385-424`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Gui/ChatGui.cs#L385-L424) raises `ChatMessage` on the print path. There is no public ordering contract between that path and `LogMessage` queue processing, so DDCore records callback order only and does not infer transition order.
- Dalamud catches subscriber exceptions around both dispatch paths. DDCore now also catches its own extraction/context failures, faults only the opt-in diagnostic recorder, and stores a fixed reason plus exception type rather than an exception message.
- [`DalamudChatFloorDiagnosticSource.Dispose`](../src/DDCore.Plugin/Observation/DalamudChatFloorDiagnosticSource.cs) removes both subscriptions idempotently. Plugin cleanup is LIFO, so the source is detached before the recorder is disposed.

No source establishes that `7248`, `7270`, or `9218` is delivered for any DD. Integer position/type, duplicate delivery across surfaces, and live ordering remain unverified.

## Candidate evidence and safety meaning

The probe recognizes game-data candidates `7248`, `7270`, and `9218`, plus displayed chat kind `57`. These mappings remain live-unverified:

| Candidate | Static evidence | Runtime meaning in 5.5D |
| --- | --- | --- |
| `7248` | Japanese game-data text `転移が実行された！` | Transition-marker candidate only; never a floor |
| `7270` | Japanese text `地下UNKNOWN階` | One plausible integer parameter becomes an unverified floor candidate |
| `9218` | Japanese text `第UNKNOWN層` | One plausible integer parameter becomes an unverified floor candidate |
| chat kind `57` | `SystemMessage` | Exact Japanese displayed-text match becomes an unverified cross-check |

Exactly one integer in `1..200` is required. Missing or ambiguous parameters produce no floor. A candidate matching B1's DD kind and ten-floor range is labelled `ConsistentCandidate`, not verified. A mismatch is `Conflict`; no B1 DD context is never treated as a valid floor.

Duplicate events are deliberately retained as separate diagnostic evidence. They are not collapsed because duplicate behavior is itself a live-test question. A reversed callback order is likewise retained by local sequence and timestamp; neither order promotes a candidate to verified. A stopped, disposed, faulted, or limit-reached recorder ignores later callbacks, and a new explicit start clears the previous in-memory session.

## Privacy and bounded storage

`ChatFloorDiagnosticEntry` contains only:

- UTC observation time and local sequence;
- event surface, candidate row ID, chat kind, total parameter count and at most eight integer index/value pairs;
- candidate floor, quality and reason code;
- B1 DD kind and ten-floor range.

It has no sender, character, account, chat text, SeString payload, entity ID, or upload endpoint. The hardened independent diagnostic document uses schema `2` so integer positions are not lost when non-integer parameters are interleaved. It does not change replay schema `5` or its schema `3`/`4` readers.

Limits are 256 entries, 256 KiB, or 15 minutes. A limit or storage error stops the recorder. Export requires a stopped recording and an explicit preview checkbox. Data stays under the plugin configuration directory:

```text
recordings/chat-floor-diagnostics/
recordings/chat-floor-diagnostics/exports/
```

## Original replay regression

All six schema-5 originals present in the repository-defined export folder on 2026-11-10 were read in place with the production `ReplayCodec`. No file was changed, moved, or committed.

| File suffix / created UTC | Snapshots | Sessions | DD / band | Exact floor | Actor usable / quarantined / missing |
|---|---:|---:|---|---:|---:|
| `131759...` / 13:15:11 | 294 | 1 | EO / 1-10 | 0 | 3,065 / 0 / 0 |
| `132017...` / 13:18:00 | 245 | 1 | EO / 1-10 | 0 | 2,695 / 0 / 0 |
| `132137...` / 13:20:18 | 63 | 1 | EO / 1-10 | 0 | 283 / 18 / 0 |
| `134526...` / 13:44:33 | 96 | 1 | EO / 1-10 | 0 | 946 / 687 / 0 |
| `135219...` / 13:51:21 | 105 | 1 | EO / 1-10 | 0 | 641 / 894 / 0 |
| `143027...` / 14:29:45 | 66 | 1 | EO / 1-10 | 0 | 584 / 412 / 0 |

The user labels the captures as covering EO floors 1-4 and 9-10. The replay payloads themselves contain only EO and the 1-10 band; they contain no exact-floor event. Therefore the files validate replay compatibility, B1 classification, player-pose availability, session consistency and B2 actor-quality decoding, but not the individual floor label or chat-event reception. The previously reported 105-snapshot capture remains unchanged: SHA-256 `A21CDE46C934A7A94D747638B6A2F0262459E53E00E4B1DA89B651872D63B90F`.

## Empty-diagnostic risk and bounded follow-up option

With the current allowlist, zero entries is ambiguous. It can mean no transition occurred, the plugin started after the line, the game used a different row/kind, the localized text did not match, or the public event was not raised. Zero entries must never be reported as proof that the floor notification does not exist.

If the live test yields zero entries, the recommended next separately approved diagnostic is a short-lived **system metadata discovery mode**, not an unconditional filter expansion. It would remain initial-OFF and explicit opt-in; stop after at most 60 seconds or 128 entries; record only callback surface, row ID, numeric log kind, parameter count, integer parameter indices/values, timestamp and B1 context; and retain no text, sender, entities, payload bytes, or identifiers. The preview/export gate and local-only storage remain mandatory. This proposal is not implemented in Phase 5.5D hardening.

## Offline edge-case result

The added tests cover exact range boundaries 1/10/11, missing/ambiguous/repeated integer values, integer index retention and bounds, invalid chat floors, wrong chat kind, different DD context, duplicate and reversed observations, entry/byte/time limits, stop/restart/dispose, source failure sanitization and serialization privacy. A matching range remains `ConsistentCandidate`, never `Verified`. Exact floor still never enters replay, `WorldSnapshot`, strategy or execution.

## Live verification procedure

1. Install/reload `0.5.4.2` manually; Codex does not operate the game or plugin UI.
2. Enter a supported DD band and confirm B1 identifies the DD and ten-floor band.
3. Open DDCore and press **階層診断開始** immediately before a floor transition.
4. Transition one floor, then press **階層診断停止**.
5. Inspect event count, candidate, quality, reason, and the preview privacy notice.
6. Check the preview box and export the diagnostic JSON.
7. Record the visible game floor separately. Do not infer success merely from a candidate value.

For each DD, verify actual callback receipt, row ID, parameter count/index, chat kind, ordering between transition and floor rows, duplicate delivery across both event surfaces, reload behavior, and range conflicts. Palace of the Dead, Heaven-on-High, Eureka Orthos, all candidate IDs, and all language behavior remain live-unverified at implementation time.

### Capture checklist

- [ ] Record the visibly confirmed DD, exact floor, and B1 ten-floor band separately from the diagnostic JSON.
- [ ] Confirm the diagnostic was explicitly started before transition and stopped afterward.
- [ ] Record every received event surface and row ID; do not record ordinary chat text.
- [ ] Record parameter count, each retained integer's callback position, and candidate value.
- [ ] Compare callback sequence and timestamp for transfer marker, structured floor row and displayed chat.
- [ ] Note duplicate rows and cross-surface duplicates rather than deleting them.
- [ ] Confirm the candidate agrees with the independently visible floor and B1 band.
- [ ] Confirm conflicting/out-of-range values remain `Conflict` and exact floor remains Unknown.
- [ ] Confirm the exported JSON contains no chat body, sender, character/account identifier, entity or payload bytes.
- [ ] If the count is zero, report it as ambiguous and do not claim event absence or success.
- [ ] Repeat after plugin reload/diagnostic restart and after DD exit/re-entry; previous entries must not become current floor state.
- [ ] Repeat independently for Palace of the Dead, Heaven-on-High and Eureka Orthos before claiming support for each.

## Acceptance boundary

Automated tests verify parsing, B1 reconciliation, ambiguity/conflict behavior, opt-in recording, limits, storage failure, preview-gated export, deletion, and absence of text/identity field names in serialized output. Only user-run live evidence can promote a candidate mapping in a later approved phase. Phase 5.5D does not do that promotion.

## Offline verification result

- Locked restore and Release build: passed, 0 warnings and 0 errors.
- Tests: Core 197/197 and Observation 141/141, 338 total; three callback-capture regression cases over the 0.5.4.1 suite.
- Format verification: passed.
- Direct/transitive NuGet vulnerability audit: no known vulnerable package reported.
- Dependency/prohibited-operation audit: no new package, BossMod runtime reference, private/native access, movement, combat, targeting, item, navigation or input operation; `DisabledExecutionGateway` remains wired.
- Package: `0.5.4.2`, Dalamud API 15, `src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip`, SHA-256 `6449DDBCFA7749EF3872A7DB164D021724870D7298BB726C6D89A769DC143B79`.
- Live callback receipt and all candidate mappings remain unverified.
