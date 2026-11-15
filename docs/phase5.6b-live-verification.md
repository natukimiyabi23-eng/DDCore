# Stage 5.6B Live Verification

## Recorded run

User-reported and locally re-read data: FFXIV, Eureka Orthos B1, Dalamud API 15, DDCore 0.5.7.0, diagnostic schema 2. The local recording folder contains duplicate copies of some exports; counts below are per supplied observation/run, not a sum across duplicate files.

| Check | Recorded result | Evidence boundary |
|---|---|---|
| Treasure BaseId 2007358 | `ddcore-environment-objects-20261115-135052.diagnostic.json` (142 samples) contains 69 consecutive `exactRuntimeMatch` samples for `obj-000005` (sequences 74-142), coordinate quality `usable`. A separate 44-sample run records a `disappeared` transition for a BaseId 2007358 object at different coordinates. | A separate recording's anonymous ID and different coordinates do not establish cross-session identity. User reports manual opening of the selected chest; JSON does not prove that action caused a disappearance. |
| Reproducer | Screenshot shows the selected UI label “再生装置”; the accompanying user report identifies exact match to BaseId `2013286`, anonymous ID `obj-000002`. | Screenshot establishes visible label only. Its association to JSON relies on user-supplied time/action correlation; automated activation-state detection is unverified. |
| Treasure BaseId 2007357 | Screenshot/report label this as the unopened treasure. JSON reports `obj-000001` exact match; targetable is true at sample 1, false at sample 20, and the object is `disappeared` at sample 40. `obj-000003` with the same BaseId remains observed. | Object-level distinction is visible in the diagnostic; screen-to-JSON association relies on user-supplied timing. Targetability/disappearance does not prove opening. |

The local schema-2 exports were re-read from the configured recordings directory. Across the relevant B1 runs, object samples contained BaseIds `2007357`, `2007358`, and `2013286`; selected-object samples included exact-match results. The 42-sample run `ddcore-environment-objects-20261115-135441.diagnostic.json` (created `2026-11-15 22:54:41 JST`) records `obj-000001` targetability changing at sample 20 and a `disappeared` transition at sample 40; same-BaseId `obj-000003` remains through sample 42. No raw JSON or screenshot is added to the repository. Exact-floor values in these records remain null; the displayed B1 is a separate screen/user observation.

## Remaining live checks

Live verification is user-performed; this document does not authorize Codex to operate FF14, Dalamud, or the plugin UI.

1. Load DDCore `0.5.7.0` as a development plugin and open `/ddcore`.
2. Open the environment-object diagnostic section. Confirm it starts `OFF` and reports no selected target before recording.
3. In a Deep Dungeon, explicitly start the diagnostic. Select a visible treasure using normal user input, then check that the UI reports `ExactRuntimeMatch`, the observed ObjectKind/BaseId and session-anonymous object ID. If no exact match appears, keep the exported result as `NoMatch`/`Ambiguous`; do not interpret it as a successful mapping.
4. Confirm the selected coordinate quality is shown independently. A missing or quarantined coordinate must remain unusable even when object identity matches.
5. Observe targetability changes and disappearance/reappearance. Verify transition reason codes include `not-open-confirmed` or `continuity-unknown`; they must never say that the object was opened.
6. Stop recording explicitly, review the privacy preview, and export only after confirmation.
7. Inspect the schema-2 export for anonymous IDs and copied target data. Confirm it contains no `GameObjectId`, `EntityId`, name, chat text, character/account data, or stable identity. Do not share the unredacted file publicly.
8. Repeat after a floor transition and after leaving/re-entering the duty. New observation sessions/floors must not inherit the previous anonymous mapping.

Record the build version, test location, whether the selected-object quality was exact/no-match/ambiguous, coordinate quality, observed transitions, and any fixed reason codes. A screen view and a matching BaseId still do not prove semantic chest identity by themselves; only record the user's observation separately as a label.
