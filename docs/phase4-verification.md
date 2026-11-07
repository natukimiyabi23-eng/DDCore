# Phase 4 verification

## Scope

Phase 4 is limited to source analysis, dependency measurement and one pure-C# navigation algorithm extraction. It does not implement a decision engine, movement, combat, target changes, items, interaction, input injection or game-memory writes.

## Verification matrix

| Check | Expected evidence | Status |
|---|---|---|
| Fixed source | All measurements use BossMod `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0` | Verified by commit-qualified Git reads and measurement output |
| Corpus measurement | 67 files / 28,609 physical lines with explicit scope | Verified by `Measure-Phase4BossMod.ps1` |
| Core independence | No BossMod, Dalamud, Lumina or FFXIVClientStructs runtime/project reference in `DDCore.Core` | Passed; Core has no project references. Text matches are attribution/comments only. |
| Route equivalence | Same output as source-faithful oracle on fixtures and all room pairs | Passed |
| Invalid graph behavior | Invalid size/index/boundary/non-reciprocal edges rejected | Passed |
| Provenance/license | Phase 4 manifest, source headers and binary notice | Passed; both phase manifests and notice are in the ZIP |
| Release build | Full solution, warnings as errors | Passed; 0 warnings, 0 errors |
| Unit tests | Existing tests plus Phase 4 tests | Passed; Core 139 + Observation 34 = 173 |
| Format | `dotnet format --verify-no-changes` | Passed |
| NuGet audit | No known vulnerable package reported | Passed for all five projects |
| GitHub Actions | Push and Draft PR full build/test jobs | Passed; runs `42388673278` and `42388678876` |
| Live Dalamud/FFXIV | Not part of this phase | **Not performed** |

## Comparison limitation

The original `FloorPathfind` class is internal and depends on the native FFXIVClientStructs room-flags type. The BossMod assembly itself was not invoked. A test-only source-faithful transcription with only the flags enum substituted is the comparison oracle. Therefore the evidence covers deterministic route semantics for the test corpus, not binary identity or live-game behavior.

## Residual risks

- The public API 15 adapter does not supply the 25-room graph, walls, passage/resource state or trap data.
- The extracted algorithm cannot be used for live decisions until those inputs have an authoritative, fail-closed provider.
- Lexical dependency counts can undercount dynamic/reflection use and overcount comments or type names; they are inventory evidence, not semantic proof.
- BossMod policies are tuned to its actor, hint and execution architecture. Reimplementation can diverge unless replay/differential fixtures are added per policy.
- Phase 5 operation remains prohibited until separately approved.

## Artifact

- Package version: `0.4.0.0`
- Dalamud API: `15`
- SHA-256: `2EDB2C5F48E0BC15992379CC0B1DEFAA83B9EB02FE7F7A44E7EF1E39D03CDB5F`
- Package contents include `DDCore.Core`, `DDCore.Observation`, the plugin, `THIRD-PARTY-NOTICES.md`, `provenance/bossmod-phase3.json` and `provenance/bossmod-phase4.json`.

No package was deployed and no live Dalamud/FFXIV test was performed.

Draft PR: <https://github.com/natukimiyabi23-eng/DDCore/pull/5> targeting `phase3/diagnostics-and-recording`. It remains unmerged.
