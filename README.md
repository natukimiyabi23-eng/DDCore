# DDCore

Standalone and extensible FFXIV Deep Dungeon plugin based on BossMod architecture.

## Status

Research and architecture phase only. No plugin implementation has started.

The initial goal is to separate the Deep Dungeon state model, mechanic analysis, navigation advice, decision engine, and optional execution layer from BossMod while retaining clear attribution for derived BSD-3-Clause code.

Automation is disabled by default in the design. The planned modes are:

1. Observer
2. Advisor
3. Simulation
4. Assisted
5. Autonomous

## Research baseline

- BossMod repository: <https://github.com/awgil/ffxiv_bossmod>
- Pinned analysis commit: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- Dalamud baseline: API 15 / .NET 10
- License baseline: BossMod BSD-3-Clause

See [the master plan](docs/DDCore_Codex_MasterPlan.md) and [research report](docs/research-report.md).

## Important distribution note

The official Dalamud plugin restrictions do not permit automated combat/gameplay plugins. DDCore's Autonomous mode therefore cannot be assumed eligible for the official plugin repository. This repository remains private during research, and distribution policy requires an explicit decision before implementation.
