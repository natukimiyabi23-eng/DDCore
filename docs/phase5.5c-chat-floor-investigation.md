# Phase 5.5C: chat floor investigation

## Conclusion

Exact-floor observation through public Dalamud API 15 is technically plausible, but it is not yet live-verified. The strongest candidate is `IChatGui.LogMessage`, not free-form text matching: the pinned game-data dump contains `LogMessage` row `7270` as `地下UNKNOWN階` in Japanese and `Floor UNKNOWN` in English, with `LogKind` `57`. API 15 exposes the row ID and typed integer parameters during the `LogMessage` callback.

The user-observed Eureka Orthos sequence `転移が実行された！` then `地下9階` is accepted as live visual evidence. It does **not** prove that DDCore can receive either line, that row `7270` produced it, what parameter index contains `9`, or what event order Dalamud exposes. Those points require the live verification in [phase5.5c-live-verification-plan.md](phase5.5c-live-verification-plan.md).

## Evidence baseline

### Installed API 15

The local SDK inspected on 2026-11-08 was `Dalamud.dll` `15.0.3.9`, product commit `3e47d4fa819d794a3213d81ad7a341114b3ba363`. Its XML contract and the matching public source agree on these points:

- [`IChatGui.cs:13-57`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Plugin/Services/IChatGui.cs#L13-L57) exposes `ChatMessage` with `IHandleableChatMessage` and `LogMessage` with `ILogMessage`.
- [`ChatMessage.cs:17-52`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Chat/ChatMessage.cs#L17-L52) exposes `LogKind`, original/current sender and message, timestamp and handled state. `OriginalMessage` is the appropriate read-only candidate because another plugin may modify the current message.
- [`LogMessage.cs:24-92`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Chat/LogMessage.cs#L24-L92) exposes `LogMessageId`, `GameData`, parameter count, typed integer/string access and handled state. The callback object and parameter list are valid only during the callback, so only normalized primitive values may leave it.
- [`ChatGui.cs:531-568`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Gui/ChatGui.cs#L531-L568) invokes `LogMessage` while processing the native log-message queue before calling the original update. This establishes the implementation path, not the exact live order relative to DDCore snapshots.
- [`XivChatType.cs:211-225`](https://github.com/goatcorp/Dalamud/blob/3e47d4fa819d794a3213d81ad7a341114b3ba363/Dalamud/Game/Text/XivChatType.cs#L211-L225) names row `57` `SystemMessage`. Dalamud v15 explicitly changed `XivChatType` to the `LogKind` sheet row IDs and recommends `LogMessage` for system messages ([v15 notes](https://dalamud.dev/versions/v15/)).

No implementation should call `PreventOriginal`, modify an `SeString`, print chat, or retain an `ILogMessage` reference.

### Game-data rows

The same community game-data revision already pinned by B1, `xivapi/ffxiv-datamining` commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`, contains:

| Row | Japanese | English | LogKind | Confirmed meaning |
|---:|---|---|---:|---|
| 7248 | `転移が実行された！` | `Transference initiated!` | 57 | Text and row metadata only; live DD callback not verified |
| 7270 | `地下UNKNOWN階` | `Floor UNKNOWN` | 57 | Text has one unresolved value candidate; its runtime type/index is not yet verified |
| 9218 | `第UNKNOWN層` | `Floor UNKNOWN` | 57 | A second floor-shaped row; content ownership and use are unknown |

Sources: pinned [`csv/ja/LogMessage.csv`](https://github.com/xivapi/ffxiv-datamining/blob/44948e4cda5846eab0baa9872122a888fcc4f1b1/csv/ja/LogMessage.csv) and [`csv/en/LogMessage.csv`](https://github.com/xivapi/ffxiv-datamining/blob/44948e4cda5846eab0baa9872122a888fcc4f1b1/csv/en/LogMessage.csv). This is reproducible community-maintained data, not an official Square Enix API. `UNKNOWN` in the dump indicates unresolved formatted data; it does not prove that parameter zero is an integer.

### Current DDCore behavior

- [`WorldSnapshot.cs:354-404`](../src/DDCore.Core/World/WorldSnapshot.cs) validates ten-floor ranges and rejects an exact floor outside that range.
- [`LiveWorldSnapshotProvider.cs:321-367`](../src/DDCore.Observation/LiveWorldSnapshotProvider.cs) emits B1 kind/range evidence while leaving `Floor` null.
- [`LiveWorldSnapshotProvider.cs:396-410`](../src/DDCore.Observation/LiveWorldSnapshotProvider.cs) resets its session only on login, territory, content ID or DD-condition changes. A normal same-content floor transition is not currently a session boundary.
- [`ObservationRuntime.cs:108-120`](../src/DDCore.Observation/ObservationRuntime.cs) resets analysis when the snapshot session changes.
- [`SchemaVersions.cs:3-14`](../src/DDCore.Core/Schema/SchemaVersions.cs) currently writes schema 5; [`ReplayCodec.cs:45-84`](../src/DDCore.Core/Replay/ReplayCodec.cs) reads schema 3, 4 and 5.
- [`Plugin.cs:24-31`](../src/DDCore.Plugin/Plugin.cs) does not inject `IChatGui` today. [`Plugin.cs:289-308`](../src/DDCore.Plugin/Plugin.cs) already provides a LIFO cleanup pattern suitable for a future subscription/unsubscription pair.

## Candidate comparison

| Candidate | Strength | Limitation | Recommendation |
|---|---|---|---|
| `IChatGui.LogMessage` row ID + typed parameter | Locale-independent ID, typed values, system-message origin | Actual row, parameter index/type and DD coverage unverified | Primary live probe and preferred eventual source |
| `IChatGui.ChatMessage` + `LogKind` + original structured text | Represents the displayed chat and has `LogKind`/timestamp | Localized text, payload normalization, other-plugin ordering and false positives | Diagnostic cross-check/fallback only after live confirmation |
| Plain text alone | Easy to prototype | Player chat spoofing, localization, punctuation and patch changes | Never sufficient for `Verified` |
| UI scraping | Could observe visible text | Pointer-level UI access, locale/lifecycle fragility | Not approved and not recommended for this slice |
| Native DD structure | Precise-looking floor field | Non-public, patch-sensitive, separately approval-gated | Out of scope |

## Per-dungeon status

| Dungeon | Visible exact-floor log | Dalamud event | Row/parameter mapping | Status |
|---|---|---|---|---|
| Eureka Orthos | User confirmed `地下9階` | Not tested | Candidate row 7270 only | Visual evidence only |
| Palace of the Dead | Not supplied | Not tested | Not established | Unverified |
| Heaven-on-High | Not supplied | Not tested | Not established | Unverified |

The two generic floor-shaped rows must not be assigned to a dungeon by row-number age or wording. Each dungeon needs an independently labelled capture.

## Open questions

1. Does EO generate `LogMessage` row 7270, and is the floor an integer parameter?
2. Are rows 7248 and 7270 delivered once, in that order, before or after B1 snapshot updates?
3. Does `ChatMessage` also fire, with `LogKind.SystemMessage`, empty/system sender and an `OriginalMessage` equivalent to the visible text?
4. Which rows and payloads are used by Palace of the Dead and Heaven-on-High?
5. Can a transition complete without either candidate event, and can plugin reload occur after the line has already been lost?

Until these are answered, exact floor remains Unknown and Phase 5 keeps its existing fail-closed stop behavior.
