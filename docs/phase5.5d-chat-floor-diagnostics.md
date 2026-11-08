# Phase 5.5D — Chat floor event diagnostics

## Result and scope

Phase 5.5D adds an explicit opt-in, read-only diagnostic probe for candidate Deep Dungeon floor notifications. It does not establish the exact current floor and does not feed any candidate into `WorldSnapshot`, `DeepDungeonStrategyState`, or the strategy engine. `DisabledExecutionGateway` is unchanged.

The plugin package is version `0.5.4.0`. Recording is OFF on startup. The user must start and stop it in the Japanese UI. No chat is sent, modified, suppressed, or retained in full.

## Verified API contract

The adapter uses only installed Dalamud API 15 public contracts:

- `IChatGui.LogMessage` supplies callback-scoped `ILogMessage`; the adapter copies only `LogMessageId` and at most eight integer parameters before returning.
- `ILogMessage.TryGetIntParameter` is used instead of formatting the message. `PreventOriginal` and `FormatLogMessageForDebugging` are never called.
- `IChatGui.ChatMessage` supplies `IHandleableChatMessage`; only `XivChatType.SystemMessage` (`57`) is considered.
- `OriginalMessage.ExtractText()` is inspected only inside the callback and discarded immediately. An exact Japanese match of `^地下([1-9][0-9]{0,2})階$` contributes only the parsed integer.
- Both events are unsubscribed during plugin disposal.

Sources are pinned in the Phase 5.5C investigation: installed Dalamud `15.0.3.9`, product commit `3e47d4fa819d794a3213d81ad7a341114b3ba363`, and game-data commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`.

## Candidate evidence and safety meaning

The probe recognizes game-data candidates `7248`, `7270`, and `9218`, plus displayed chat kind `57`. These mappings remain live-unverified:

| Candidate | Static evidence | Runtime meaning in 5.5D |
| --- | --- | --- |
| `7248` | Japanese game-data text `転移が実行された！` | Transition-marker candidate only; never a floor |
| `7270` | Japanese text `地下UNKNOWN階` | One plausible integer parameter becomes an unverified floor candidate |
| `9218` | Japanese text `第UNKNOWN層` | One plausible integer parameter becomes an unverified floor candidate |
| chat kind `57` | `SystemMessage` | Exact Japanese displayed-text match becomes an unverified cross-check |

Exactly one integer in `1..200` is required. Missing or ambiguous parameters produce no floor. A candidate matching B1's DD kind and ten-floor range is labelled `ConsistentCandidate`, not verified. A mismatch is `Conflict`; no B1 DD context is never treated as a valid floor.

## Privacy and bounded storage

`ChatFloorDiagnosticEntry` contains only:

- UTC observation time and local sequence;
- event surface, candidate row ID, chat kind and bounded integer parameters;
- candidate floor, quality and reason code;
- B1 DD kind and ten-floor range.

It has no sender, character, account, chat text, SeString payload, entity ID, or upload endpoint. The independent diagnostic document uses schema `1`; it does not change replay schema `5` or its schema `3`/`4` readers.

Limits are 256 entries, 256 KiB, or 15 minutes. A limit or storage error stops the recorder. Export requires a stopped recording and an explicit preview checkbox. Data stays under the plugin configuration directory:

```text
recordings/chat-floor-diagnostics/
recordings/chat-floor-diagnostics/exports/
```

## Original replay recheck

The original file remained read-only:

```text
ddcore-export-20261108-135219-042a32a4ea474e1582fc987c68bd286e.replay.json
```

Recheck result: schema `5`, producer `0.5.3.0`, 105 snapshots, 1,535 actor observations, 641 usable, 894 quarantined, 0 missing. SHA-256: `A21CDE46C934A7A94D747638B6A2F0262459E53E00E4B1DA89B651872D63B90F`. Those replay observations and the new chat-event diagnostics are different evidence sources.

## Live verification procedure

1. Install/reload `0.5.4.0` manually; Codex does not operate the game or plugin UI.
2. Enter a supported DD band and confirm B1 identifies the DD and ten-floor band.
3. Open DDCore and press **階層診断開始** immediately before a floor transition.
4. Transition one floor, then press **階層診断停止**.
5. Inspect event count, candidate, quality, reason, and the preview privacy notice.
6. Check the preview box and export the diagnostic JSON.
7. Record the visible game floor separately. Do not infer success merely from a candidate value.

For each DD, verify actual callback receipt, row ID, parameter count/index, chat kind, ordering between transition and floor rows, duplicate delivery across both event surfaces, reload behavior, and range conflicts. Palace of the Dead, Heaven-on-High, Eureka Orthos, all candidate IDs, and all language behavior remain live-unverified at implementation time.

## Acceptance boundary

Automated tests verify parsing, B1 reconciliation, ambiguity/conflict behavior, opt-in recording, limits, storage failure, preview-gated export, deletion, and absence of text/identity field names in serialized output. Only user-run live evidence can promote a candidate mapping in a later approved phase. Phase 5.5D does not do that promotion.
