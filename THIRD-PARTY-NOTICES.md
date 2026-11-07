# Third-party notices

## Phase 2 foundation

The Phase 2 implementation is original DDCore foundation code. It does not copy or redistribute BossMod source code or resources.

BossMod remains a research reference pinned in the project documentation. If a later phase copies or adapts BossMod code, the relevant BSD-3-Clause notice, source path, commit and transformation record must be added before that code is merged.

The plugin builds against Dalamud API 15 through `Dalamud.NET.Sdk/15.0.0`. Dalamud assemblies are supplied by the user's Dalamud installation and are not committed to this repository.

Test-only NuGet dependencies are listed in `tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj` and restored from NuGet during development or CI.
