# Stage 5.6B Live Verification

Live verification is a user-performed check; this document does not authorize Codex to operate FF14, Dalamud, or the plugin UI.

1. Load DDCore `0.5.7.0` as a development plugin and open `/ddcore`.
2. Open the environment-object diagnostic section. Confirm it starts `OFF` and reports no selected target before recording.
3. In a Deep Dungeon, explicitly start the diagnostic. Select a visible treasure using normal user input, then check that the UI reports `ExactRuntimeMatch`, the observed ObjectKind/BaseId and session-anonymous object ID. If no exact match appears, keep the exported result as `NoMatch`/`Ambiguous`; do not interpret it as a successful mapping.
4. Confirm the selected coordinate quality is shown independently. A missing or quarantined coordinate must remain unusable even when object identity matches.
5. Observe targetability changes and disappearance/reappearance. Verify transition reason codes include `not-open-confirmed` or `continuity-unknown`; they must never say that the object was opened.
6. Stop recording explicitly, review the privacy preview, and export only after confirmation.
7. Inspect the schema-2 export for anonymous IDs and copied target data. Confirm it contains no `GameObjectId`, `EntityId`, name, chat text, character/account data, or stable identity. Do not share the unredacted file publicly.
8. Repeat after a floor transition and after leaving/re-entering the duty. New observation sessions/floors must not inherit the previous anonymous mapping.

Record the build version, test location, whether the selected-object quality was exact/no-match/ambiguous, coordinate quality, observed transitions, and any fixed reason codes. A screen view and a matching BaseId still do not prove semantic chest identity by themselves; only record the user's observation separately as a label.
