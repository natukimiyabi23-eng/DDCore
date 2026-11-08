# Phase 5.5B B2 actor spatial quality and schema 5 verification

## Result

B2.1-B2.4 implement source-neutral actor spatial qualification and schema 5 while keeping all gameplay execution disabled. Actor coordinates are now explicitly `Usable`, `Quarantined`, or `Missing`. A finite exact origin is preserved as `(0,0)` but quarantined unless future independent evidence validates it. No production rule refers to BaseId 6388.

The normalized actor identifier field is `baseId`. Schema 3 and 4 readers map legacy JSON `objectId` to `BaseId`; new output is schema 5 and writes only `baseId`. Legacy positions are preserved but receive `LegacyUnqualified` metadata and remain quarantined.

## Safety propagation

- `CastHazardTracker` accepts only a usable cast target location or usable actor position and also requires usable rotation evidence.
- A missing or quarantined origin produces an uncertainty diagnostic and no hazard. The mechanic safety result remains unknown/unsupported rather than safe.
- `WorldSnapshotStrategyAdapter` reports aggregate usable/quarantined/missing counts and reason codes. It does not create rooms, exits, distances, targets or routes from actor coordinates.
- Recording retains sanitized enemy/event-object evidence while the existing privacy filter continues to remove player/party actors and references.
- The Japanese diagnostic UI shows only aggregate spatial counts and the latest reason; it does not display actor identifiers or BaseIds.
- `DisabledExecutionGateway`, emergency stop and the Phase 5 fail-closed strategy behavior are unchanged.

## Replay compatibility evidence

The production `ReplayCodec` was run read-only against every original `*.replay.json` present in the repository-defined export folder on 2026-11-08. No file was rewritten, moved or committed.

| Input schema / producer | Files | Snapshots | Actors | Normalized result |
|---|---:|---:|---:|---|
| 3 / 0.4.5.0 | 2 | 412 | 4,965 | all legacy positions quarantined |
| 4 / 0.5.1.0 | 2 | 64 | 479 | all legacy positions quarantined |
| 4 / 0.5.2.0 | 2 | 822 | 8,915 | all legacy positions quarantined |
| **Total** | **6** | **1,298** | **14,359** | normalized to schema 5 in memory |

The latest analyzed file was `ddcore-export-20261108-125627-779b8c58802c4b1b8065237330f9b651.replay.json`, created at 2026-11-08 21:52:20 JST, schema 4, producer 0.5.2.0, 453 snapshots. Its pre/post verification SHA-256 is `7CA85FA6FEC6520D0D21F0DFEA75A6260CBD635EC5F392897CE1814296AC39B6`.

Compatibility means the files decode, validate and normalize without treating unqualified legacy positions as usable. It does not prove the recorded coordinates are correct.

## Automated verification

- Locked restore: passed.
- Release build: passed with 0 warnings and 0 errors.
- Core tests: 197 passed.
- Observation tests: 104 passed.
- Total: 301 passed, including 17 net-new B2 tests.
- `dotnet format --verify-no-changes`: passed.
- NuGet vulnerability audit: no known vulnerable direct or transitive packages.
- Dependency audit: no BossMod runtime/package reference and no new package dependency.
- Prohibited-operation audit: no FFXIVClientStructs, targeting, action, input or navigation operation reference; `DisabledExecutionGateway` remains wired in `Plugin.cs`.
- Package: version 0.5.3.0, Dalamud API 15, SHA-256 `322C89CB24E5AF94DDFC9CCE06CC39D8E10438363D091A6577FDCDB0E9A0ABD9`.
- GitHub Actions: pending Draft PR publication.

The B2 cases cover exact-origin preservation/quarantine for BaseId 6388 and unrelated IDs, missing versus origin, public acquisition failure, invalid/stale/dead observations, usable non-origin observations, quarantined target derivation, hazard suppression, schema 3/4 migration, schema 5 round-trip/output names, mixed schema pairs, duplicate properties, legacy aliases in schema 5 and malformed spatial pairs.

## User-run live verification

The user performs plugin installation/reload and live checks. Codex did not operate FF14 or Dalamud.

1. Load package version 0.5.3.0 as a development plugin and open `/ddcore`.
2. Enter Eureka Orthos 1-10 and confirm the existing DD display still reads `オルト・エウレカ 1～10階帯（正確な階層は未取得）`.
3. Start recording explicitly, observe visible enemies, stop, review and export explicitly.
4. Confirm the UI reports non-zero `隔離` for repeated exact-origin actors and does not count them as `使用可` or `欠損`.
5. Confirm visible finite non-origin live enemies can appear as `使用可`.
6. Confirm uncertain actors do not add AoEs, rooms or routes and that the safety display remains unknown/unsupported when required evidence is absent.
7. Stop the plugin test if capture errors, schema errors or unexpected hazard output occurs; keep the exported file for read-only diagnosis.

Palace of the Dead, Heaven-on-High, EO 11-100 and every other floor band remain unverified until separately observed.

## Not implemented

- no native/internal structure access;
- no exact-floor or room/topology acquisition;
- no movement, combat, targeting, item use or input injection;
- no special handling for BaseId 6388;
- no deployment, plugin activation or PR merge.
