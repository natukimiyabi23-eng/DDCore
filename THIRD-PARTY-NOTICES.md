# Third-party notices

## Phase 2 foundation

The Phase 2 implementation is original DDCore foundation code. It does not copy or redistribute BossMod source code or resources.

BossMod remains a research reference pinned in the project documentation. If a later phase copies or adapts BossMod code, the relevant BSD-3-Clause notice, source path, commit and transformation record must be added before that code is merged.

The plugin builds against Dalamud API 15 through `Dalamud.NET.Sdk/15.0.0`. Dalamud assemblies are supplied by the user's Dalamud installation and are not committed to this repository.

Test-only NuGet dependencies are listed in `tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj` and restored from NuGet during development or CI.

## Phase 3 BossMod-derived data

The Phase 3 Gancanagh analyzer reimplements observed behavior and uses identifiers and numeric encounter data from BossMod commit `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. File-level provenance is recorded in `provenance/bossmod-phase3.json`.

BossMod is distributed under the BSD 3-Clause License:

Copyright (c) 2022-2024, Andrew Gilewsky

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
