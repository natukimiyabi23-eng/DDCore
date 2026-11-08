# Phase 5.5B B1 public classification verification

## Scope and result

B1 classifies the Deep Dungeon kind and ten-floor band from public Dalamud API 15 observations plus a pinned game-data manifest. It does not infer the exact floor and does not add native reads, actor-coordinate quality work, gameplay operation or an execution gateway.

The live adapter records these independent facts:

- `ICondition[ConditionFlag.InDeepDungeon]`;
- `IDutyState.ContentFinderCondition.RowId` even when `IsDutyStarted` is false;
- the CFC row's referenced `TerritoryType`, untyped `Content` row and `ShortCode`;
- `IClientState.TerritoryType`.

All values must match one manifest entry. A missing or unknown ID, unavailable row, observed/referenced territory mismatch, content mismatch or short-code mismatch yields an unsupported context and stable reason code. Exact `DeepDungeonContext.Floor` remains `null`; only `FloorRange` is populated.

Implementation evidence after formatting:

- `src/DDCore.Plugin/Observation/DalamudObservationSource.cs:99-139` reads the CFC row and explicit DD condition through public services;
- `src/DDCore.Observation/DeepDungeonContentRegistry.cs:19-129` contains the pinned manifest and full-match resolver;
- `src/DDCore.Observation/LiveWorldSnapshotProvider.cs:274-326` applies fail-closed classification without actor/floor inference;
- `src/DDCore.Core/World/WorldSnapshot.cs:211-260,335-336` defines and validates the band/direct-condition evidence and safe `IsDeepDungeon` semantics;
- `src/DDCore.Core/Decision/WorldSnapshotStrategyAdapter.cs:7-90` preserves the band as evidence while leaving exact floor Unknown;
- `src/DDCore.Observation/ObservationRuntime.cs:23-204` and `src/DDCore.Plugin/Windows/MainWindow.cs:160-177` expose the range without calling it an exact floor.

## Evidence and mapping method

### Public API 15

The local API 15.0.3.9 XML documents `ConditionFlag.InDeepDungeon`, `ICondition`'s condition indexer, `IClientState.TerritoryType`, and `IDutyState.ContentFinderCondition`. The implementation uses only those public plugin services and the public Lumina row exposed by `IDutyState`.

- <https://dalamud.dev/api/Dalamud.Game.ClientState.Conditions/Enums/ConditionFlag/>
- <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/ICondition/>
- <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/>
- <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDutyState/>

### Checked game data

Manifest revision: `xivapi/ffxiv-datamining` commit `44948e4cda5846eab0baa9872122a888fcc4f1b1`, [`csv/en/ContentFinderCondition.csv`](https://github.com/xivapi/ffxiv-datamining/blob/44948e4cda5846eab0baa9872122a888fcc4f1b1/csv/en/ContentFinderCondition.csv).

| DD | CFC rows | Territory rows | Data-content rows | Bands |
|---|---:|---:|---:|---:|
| Palace of the Dead | 174-178, 204-218 | 561-565, 593-607 | 60001-60020 | 1-10 through 191-200 |
| Heaven-on-High | 540-549 | 770, 771, 772, 782, 773, 783, 774, 784, 775, 785 | 60021-60030 | 1-10 through 91-100 |
| Eureka Orthos | 897-906 | 1099-1108 | 60031-60040 | 1-10 through 91-100 |

The registry stores each CFC/territory/content/short-code tuple explicitly. Runtime code does not derive non-sequential Heaven-on-High territories or use localized duty names.

## Live replay regression evidence

The latest valid original export under the repository-defined fixed folder rule was read without modification:

- file: `ddcore-export-20261108-114807-8e655d1ad023455796a6a0beeba0421d.replay.json`;
- `createdAtUtc`: 2026-11-08 20:47:47 +09:00;
- schema 4, producer 0.5.1.0, 34 snapshots;
- SHA-256: `7890DDB45657AF6BAD9EDD2A9CEBFD4F336B8CB7D9EE68B8E38CBE3D361B39D2`;
- recorded public values: territory 1099 and CFC/content ID 897;
- user-provided label: Eureka Orthos;
- checked game-data mapping: EO floors 1-10.

The raw export predates B1 and remains classified as unsupported when replayed as-is. B1's regression test feeds its confirmed public IDs and checked CFC row evidence through the new classifier. This is not a claim of live B1 verification.

## Compatibility and safety

- `DeepDungeonFloorRange` and `IsDeepDungeonObserved` are optional additive schema-4 fields. Schema 3 and older schema-4 JSON without them remains readable and Unknown for those fields.
- A legacy `Unsupported` duty without direct DD-condition evidence no longer makes `WorldSnapshot.IsDeepDungeon` true.
- Strategy conversion exposes the band only as evidence. It leaves exact floor Unknown, and the strategy engine continues to return `Stop` / `Risk=Unknown` when required inputs are absent.
- The old encounter-actor inference of EO floor 10 was removed. Public B1 data never activates the exact-floor Gancanagh analyzer by itself.
- `DisabledExecutionGateway` is unchanged.

## Verification

Local verification:

- locked restore: passed;
- Release build: passed, 0 warnings and 0 errors;
- Core tests: 189 passed;
- Observation tests: 95 passed;
- total tests: 284 passed, 0 failed, 0 skipped;
- net new test cases from the 223-test B1 base: 61;
- format verification: passed;
- NuGet transitive vulnerability audit: no known vulnerable packages;
- dependency audit: Core has no project reference; Observation references Core only; Plugin references Core/Observation and Dalamud SDK only;
- prohibited-operation diff audit: no native/client-struct access, movement, combat, targeting, item use, input injection or execution-gateway change;
- original replay SHA-256 remained `7890DDB45657AF6BAD9EDD2A9CEBFD4F336B8CB7D9EE68B8E38CBE3D361B39D2`; no `*.replay.json` is tracked;
- package: version 0.5.2.0, Dalamud API 15, SHA-256 `CD1034425FFE5E9288661AAC33E4263987586191E053B32E7D68EA711BE6AC76`.

GitHub verification:

- implementation commit: `cf54cad`;
- CI-filter commit: `3aedc12`;
- stacked Draft PR #10 targets `phase5.5b/implementation-plan` and remains Draft/mergeable;
- push run `42480251741`: passed;
- Draft PR run `42480255922`: passed;
- the existing `actions/upload-artifact@v4` Node.js 20 deprecation annotation did not fail either run.

Live Dalamud loading and captures from PotD, HoH and EO remain user-run verification items.

## Unimplemented and unverified

- exact floor;
- B2 actor-coordinate quality/schema changes;
- rooms, passages, exits, traps, pomanders and timer;
- native/internal structures or memory reads;
- movement, combat, targeting, item use and input injection;
- live behavior of `ConditionFlag.InDeepDungeon` and CFC row availability during entry, reconnect and floor transitions.
