# Phase 3.7 live test procedure

## Status

This is a user-run procedure. Codex did not enable/reload DDCore, operate FFXIV, or perform the checks below. Record each result as pass, fail or not tested; do not infer success from unit tests.

## Preconditions

1. Build and deploy the reviewed Phase 3.7 package through the normal development-plugin process.
2. Confirm the package version is `0.3.1.0` and API level is 15.
3. Keep DDCore in Observer mode. Confirm `ゲーム操作機能` remains `未実装・無効`.
4. Use a non-sensitive test session. Do not include chat text or external logs in a shared report without reviewing them.

## Load and idle checks

1. Enable/reload DDCore manually.
2. Open `/ddcore` and verify the Japanese Phase 3.7 status/recording section renders.
3. Leave `記録状態` at `OFF` for at least one minute.
4. Verify no replay file is created while OFF and no repeated per-frame log flood appears.
5. Confirm movement, target, action and item state are not changed by DDCore.

## Start and stop

1. Press `記録開始` once and verify `記録中` appears.
2. Press it again only if the button is unexpectedly enabled; it must not create a second session.
3. Observe for 30-60 seconds in a supported or unsupported area.
4. Verify count and size increase at a bounded rate, and the save path stays under the plugin configuration directory.
5. Press `記録停止`; verify the state becomes `停止・保存済み` and the file remains local.

## Privacy and preview

1. Expand `保存内容を確認` before export.
2. Verify time range, counts and unsupported reason are plausible.
3. Inspect the JSON locally. It must contain no character names, chat text, account identifiers, player actors or party-member actors.
4. Enemy/event-object numeric IDs, positions, rotations, casts and reason codes are expected.

## Export and delete

1. Confirm `エクスポート` is disabled until recording stops and `この保存内容を確認しました` is checked.
2. Check the confirmation, export, and verify one valid replay appears under `recordings/exports`.
3. Confirm there is no network request or automatic upload.
4. Check `削除を確認`, press `記録データ削除`, and verify the current recording and its export are removed.

## Session transitions

Repeat recording separately for each case:

1. Territory/zone change.
2. Duty/content change.
3. Logout.

The recorder must stop with `recording.session-changed` before a new session is mixed into the file. Restarting recording requires another explicit click.

## Gancanagh diagnostic check

1. In Eureka Orthos floor 10, start recording before the encounter.
2. Observe Mandrashock Single (`31478`), Mandrashock Titan (`32700`) and Mandrastorm (`31479`) when naturally available.
3. Stop recording after the encounter.
4. Load the replay in the test harness and compare enemy IDs, cast action/timing, positions and regenerated hazards with the visible encounter and pinned BossMod behavior.
5. Treat cast ground location, cast-specific rotation, authoritative floor and inferred completion/interruption as partial/unverified where public API 15 cannot supply them.

## Failure checks

Use a disposable test copy or controlled permission-denied directory only if safe:

1. Cause a local save failure.
2. Verify the recorder changes to `保存エラー・停止`, shows the error and stops accepting samples.
3. Verify the observer remains fail-closed and DDCore does not crash or enable game operation.

## Evidence to retain

- DDCore package version and SHA-256.
- Dalamud version and API level.
- Test date, content, floor and observed action IDs.
- Redacted diagnostic reason codes and local replay hash.
- Pass/fail/not-tested result for each section.

Do not publish a replay until its local preview and privacy contents have been reviewed.
