# DDCore repository instructions

## Live verification replay handling

- Before analyzing live DDCore behavior, search `C:\Users\wasur\AppData\Roaming\XIVLauncher\pluginConfigs\DDCore\recordings\exports\` for `*.replay.json`.
- Sort candidates by filesystem modification time, then inspect the JSON `createdAtUtc`, `schemaVersion`, and `producerVersion`. Use the latest valid file unless the task requires an earlier comparison.
- Report the analyzed filename, recording timestamp, schema/producer versions, and snapshot count. Keep facts present in the JSON separate from user-supplied truth labels.
- If the folder or file is absent, inaccessible, malformed, or incompatible, report that fact. Never invent missing values or substitute synthetic data while claiming live validation.
- Read original exports in place and read-only. Never modify, delete, relocate, or commit them.
- Do not include raw exports, player/account identifiers, entity IDs, or unnecessary raw diagnostics in commits, pull requests, or public documentation. Prefer aggregate counts and sanitized fixtures.
- Treat recorded coordinates, identifiers, and diagnostics as evidence only for what was captured. Resolve their semantics from verified code/game-data sources before using them in strategy or safety decisions.
- Compare related older exports from the same folder when version or behavior changes matter, and account for replay schema compatibility explicitly.
- Recompute or verify hashes before and after analysis when preservation of the originals matters.
- The fixed path is the current local default, not a portable API contract. Any production reader or developer tool must accept a configurable export directory and fail closed when it is unavailable.

These rules apply to Phase 5.5B and all later DDCore work.
