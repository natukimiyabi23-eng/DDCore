# Dependencies

## Verified baseline

Analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`.

| Dependency | Evidence | DDCore treatment |
|---|---|---|
| .NET 10 SDK | BossMod `global.json`; local build used 10.0.302 | Required for API 15 baseline |
| `Dalamud.NET.Sdk/15.0.0` | `BossMod/BossMod.csproj`; `src/DDCore.Plugin/DDCore.Plugin.csproj` | Used by the Phase 2 plugin shell |
| Dalamud API | BossMod SDK, local `Dalamud.dll` 15.0.3.9/XML and official API 15 docs | Only Plugin uses public read-only services; Core/Observation have no reference |
| Dalamud API 16 preview | Official v16 page marks it not finalized and under development for Patch 8.0 | Track as migration risk; do not target preview APIs |
| FFXIVClientStructs | `DeepDungeonState`, `WorldStateGameSync`, `FloorPathfind` | Isolate unsafe/native reads in `DDCore.World` adapter |
| Lumina | `DeepDungeonState`, `Rooms`, config/UI | Use for game sheets and local game files |
| BossMod core | `ZoneModule`, `WorldState`, `AIHints`, `ActionQueue`, pathfinding, AoE types | Port only necessary concepts with attribution |
| BossMod.Modules | 63 DD files/resources | Source of dungeon and mechanic definitions |
| BossMod.Autorotation | `DeepDungeonAI`, action execution and rotation infrastructure | Optional reference; do not make core decisions depend on it |
| Palantir IPC | `Palantir.Traps` subscriber in `AutoClear`/`Traps` | Optional capability with explicit unavailable state |
| Dalamud ImGui/Windowing | DD config/minimap and future UI | Use Windowing API for regular windows |

## BossMod package graph observed during build

The pinned module build restored, among others, DalaMock 6.2.1, DalamudPackager 15.0.0, Pictomancy 1.0.10, CsvHelper 33.1.0, Microsoft.Extensions 10.x, and transitive `SixLabors.ImageSharp 3.1.12`.

The build emitted NU1902/NU1903 advisories for ImageSharp 3.1.12. This is a fact about the pinned dependency graph, not proof that DD code itself invokes a vulnerable path. DDCore should avoid inheriting the full BossMod package graph and must run dependency auditing on its own minimal set.

Phase 3.6 uses `IFramework`, `IClientState`, `IDutyState`, `IObjectTable`, `IGameObject`, `ICharacter`, `IBattleNpc`, `IBattleChara` and public status wrappers. It does not use FFXIVClientStructs, signatures, interop hooks or object addresses. See `docs/phase3-live-observation.md` for field-level availability and official links.

## Deep Dungeon source inventory

| Area | Files/resources | Notes |
|---|---:|---|
| Global shared DD | 10 | AutoClear, room pathfinding, geometry, traps, minimap, config, resources |
| Palace of the Dead | 20 | Floor module plus boss modules through floor 190; floor 200 zone registration exists |
| Heaven-on-High | 10 | Floor module plus floor 10-90 boss files |
| Eureka Orthos | 10 | Floor module plus floor 10-90 boss files |
| Pilgrim's Traverse | 13 | Floor module, floor 10-90 bosses, floor 99 module and two JSON resources |
| Total | 63 | Counted from paths containing `DeepDungeon` under `BossMod.Modules` |

Of these 63 paths, 59 are C# files and 4 are JSON resource files. The current BossMod synchronization boundary reads native Deep Dungeon state through FFXIVClientStructs; the proposed Phase 3 Core model therefore owns its enums and immutable records and accepts only normalized adapter output. See `docs/phase3-mechanics-design.md` for the dependency boundary and replay schema.

## License obligations

BossMod uses BSD-3-Clause. Derived source and binary distribution must preserve its copyright notice, license conditions, and disclaimer. DDCore-specific code should be kept in separate namespaces/files, and later implementation should add a `THIRD-PARTY-NOTICES.md` plus source headers or a provenance manifest for copied/modified files.

No BossMod code or resources were copied into DDCore through Phase 2. Phase 3 records behavior/data reimplementation in `provenance/bossmod-phase3.json`. Phase 4 adapts the small `FloorPathfind` room BFS with explicit source headers, a BSD notice and `provenance/bossmod-phase4.json`; it does not add a BossMod assembly/package reference.

The reproducible Phase 4 lexical dependency inventory is implemented in `tools/Measure-Phase4BossMod.ps1`. It reports 67 scoped files and 28,609 physical lines at the pinned commit. Its dependency families are lexical evidence rather than a semantic compiler graph; see `docs/phase4-reuse-analysis.md` for the exact definition and results.

## Distribution constraint

Official Dalamud restrictions say plugins must not automate server interaction or combat/gameplay and list several automated behaviors as non-starters. Therefore:

- Observer, Advisor, and Simulation modes may be designed for informational use, subject to the information-visibility rules.
- Autonomous mode should be treated as ineligible for the official repository unless the approval team explicitly says otherwise.
- A private/custom repository is not an official endorsement and does not remove game-account or policy risk.
- Visible markers for non-telegraphed AoEs are explicitly listed as not allowed. Phase 3 must not enable such in-world presentation by default.
- Official repository acceptance remains a subjective review decision; combat-related presentation should be discussed with the approval team before submission.

## Primary sources

- BossMod pinned tree: <https://github.com/awgil/ffxiv_bossmod/tree/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0>
- BossMod license: <https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/LICENSE>
- Dalamud v15: <https://dalamud.dev/versions/v15/>
- Dalamud v16 preview: <https://dalamud.dev/versions/v16/>
- Dalamud API index: <https://dalamud.dev/api/>
- Plugin restrictions: <https://dalamud.dev/plugin-publishing/restrictions/>
- Plugin technical considerations: <https://dalamud.dev/plugin-development/technical-considerations/>
- Official repository criteria: <https://github.com/goatcorp/DalamudPluginsD17/blob/main/README.md>
