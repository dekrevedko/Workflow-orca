# Preview.3 publication verification — 2026-10-29

Version **0.1.0-preview.3** is published on NuGet.org and authenticated GitHub
Packages, with all twelve packages verified. [GitHub release](https://github.com/dekrevedko/Workflow-orca/releases/tag/v0.1.0-preview.3).
The release tag remains at `6b4ab952376de366a102e236b5e2b5b1ddfade41`, the verified two-parent merge of PR #15.
The approved 25-path checkpoint is bd27af4; its sole verdict child is 8983a33.
No package file was rebuilt/replaced and the tag was not moved during recovery.

[Publication run 40997094707](https://github.com/dekrevedko/Workflow-orca/actions/runs/40997094707)
passed CI, package identity/checksum/source checks, both registry publishes and
both fresh external consumers (result 42). GitHub source mapping and downloaded
package hashes prevent a fallback restore from satisfying its check. Independent
NuGet.org lookup confirms all twelve listed versions and exact repository commit.
Original GitHub asset hashes are retained in [the JSON receipt](published-2026-10-29.json);
NuGet.org adds a repository signature, so its package bytes need not equal those hashes.

## Native-gate incident and recovery

The initial release's optional CI job was skipped. Its downstream native matrix
and aggregate also skipped, but the reusable workflow returned success and the
publisher proceeded. That was an orchestration failure, not evidence of native
execution. The original run is preserved honestly with that limitation.

The reviewed correction at 211f745, with verdict-only child ce882e8, overrides
skip propagation explicitly and requires successful direct dependencies. Only a
validated six-cell aggregate emits `qualified=true`. Initial publishing requires
that output and separately rejects an actual job inventory with any missing,
duplicated, unsuccessful or uncompleted native cell or aggregate. Thirteen
positive/negative tests include the real skipped-run reproduction; actionlint passes.

[Exact-asset qualification 40998543857](https://github.com/dekrevedko/Workflow-orca/actions/runs/40998543857)
downloaded these published assets, verified their source against the release tag,
and rejected source/build-input differences. It exercised the same skipped optional
CI path successfully. Independent replay verifies all six reports, **384/384** cases,
the identical twelve asset hashes, and all **360** product/build input hashes
against `6b4ab952376de366a102e236b5e2b5b1ddfade41`. Harness commit: `ce882e8c2caa346ed12174819c73bf1c2c316bce`.

| Native host | JIT | Full trim | Native AOT |
|---|---:|---:|---:|
| Windows Server 2025 / win-x64 | 82/82 | 55/55 | 55/55 |
| Ubuntu 24.04 / linux-x64 | 82/82 | 55/55 | 55/55 |

The 24 deliberately rejected handwritten-framework diagnostic occurrences retain
individual reviewed classifications. Reflection remains SDK-default disabled for
trimmed/AOT and dynamic code is unavailable under AOT. Existing source evidence
40762764617 and all earlier frozen records remain unchanged. Only the selected
ephemeral closure is qualified; no other engines/RIDs, all-package badge, live event
delivery/resumption or outgoing publishing credit is implied.

## NuGet.org package versions

| Package | Verified version |
|---|---|
| OrcaCore | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore/0.1.0-preview.3) |
| OrcaCore.Core | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Core/0.1.0-preview.3) |
| OrcaCore.Engine.Ephemeral | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Engine.Ephemeral/0.1.0-preview.3) |
| OrcaCore.Runtime.Protocol | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Runtime.Protocol/0.1.0-preview.3) |
| OrcaCore.Provider.Abstractions | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Provider.Abstractions/0.1.0-preview.3) |
| OrcaCore.Engine.Durable | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Engine.Durable/0.1.0-preview.3) |
| OrcaCore.Durable.Hosting | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Durable.Hosting/0.1.0-preview.3) |
| OrcaCore.Providers.InMemory | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Providers.InMemory/0.1.0-preview.3) |
| OrcaCore.Providers.PostgreSql | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Providers.PostgreSql/0.1.0-preview.3) |
| OrcaCore.Providers.SqlServer | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Providers.SqlServer/0.1.0-preview.3) |
| OrcaCore.Dag | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Dag/0.1.0-preview.3) |
| OrcaCore.Dag.Hosting | [0.1.0-preview.3](https://www.nuget.org/packages/OrcaCore.Dag.Hosting/0.1.0-preview.3) |

The same package IDs/version are verified in
`https://nuget.pkg.github.com/dekrevedko/index.json`; local consumers need the
authentication/source mapping documented in [the installation guide](../../packages.md).
