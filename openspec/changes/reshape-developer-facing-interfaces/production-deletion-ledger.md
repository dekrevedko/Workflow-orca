# Production deletion ledger

## Status and authority

This is the human-readable companion to the machine-checked ledger at
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/production-deletion-ledger.json`.
The JSON ledger is authoritative for exact path, exclusion, package, recovery, owner, normative,
symbol-inventory, and executable-evidence coordinates. This document explains the reviewed
dispositions; it does not replace those exact coordinates.

The inventory begins at recovery checkpoint
`d76192f089dd07f68e310c21fe4e5a38dd93cf7f` and describes production target
`76d6b4b91456177db44dabd6e7ee519e17ae8d2d`. The deletion inventory is the union of production
paths present at the recovery checkpoint and at this reviewed target, so later-created bridges cannot
escape the older baseline. Ledger-only commits after that target do not alter the
production inventory. Any later change to `src/`, `samples/`, a production `<Compile Remove>`, an
orphaned source root, a retired symbol inventory, or the v1 package manifest must update the ledger
in the same target or the infrastructure guards fail.

## Exact accounting

| Inventory | Exact count | Enforcement |
|---|---:|---|
| Disposition families | 24 | unique family IDs and one closed disposition each |
| Physically deleted production paths | 89 | union of recovery/target trees compared with the current filesystem |
| Production `<Compile Remove>` entries | 1 | every `src/` and `samples/` project parsed as XML |
| Orphaned production roots | 5 | source/SQL roots with no project file |
| Retired/deferred package artifacts | 6 | deleted project identities absent from the exact v1 manifest |
| Retired public/member symbols | 120 | ordered inventory and SHA-256 pinned |
| Removed internal result placeholders | 4 | ordered inventory and SHA-256 pinned |
| Forbidden internal bridge types | 9 | ordered inventory and SHA-256 pinned |
| Unresolved entries | **0** | the token is rejected case-insensitively by the guard |

The symbol inventories intentionally remain in `PublicApiBaselineGuards.cs`, where source and
metadata absence are executable. The ledger pins their field names, counts, ordered content hashes,
owners, and test methods so they cannot drift independently.

Every declaration contained by a physically deleted `.cs` path inherits that path's one family
disposition; this is enforced by the exact 89-path recovery/target comparison. The three ordered symbol
inventories separately cover public/member and internal removals that are not safely represented by
file accounting alone, including in-place member removal and forbidden compatibility bridges.

## Reviewed family dispositions

| Family | Disposition | Current or future owner | Reason |
|---|---|---|---|
| Runtime commands, facts, continuation envelope, park reason, wait mode | Replace/relocate | `OrcaCore.Runtime.Protocol` | Runtime protocol moved out of the application assembly; application APIs remain protocol-free. |
| Application project identity | Replace/relocate | `OrcaCore` | `OrcaCore.Abstractions.csproj` became the exact `OrcaCore` package/assembly project. |
| Provider ports, commit/resource records, protocol JSON framing | Replace/relocate | `OrcaCore.Provider.Abstractions` and `OrcaCore.Runtime.Protocol` | Provider-author and runtime-protocol ownership is explicit. |
| Archive/purge vocabulary and provider retention behavior | Replace / relocate | `OrcaCore.Provider.Abstractions` + certified providers | Public archive/purge stays absent. Provider maintenance owns inspection, archival, and reference-safe cleanup; accepted-event and monotonic route-revision tombstones remain durable. |
| Saga authoring, protocol, state, and audit source | Defer | task 9.6 registry | Saga is future/non-v1; recovery source remains addressable and earns no current capability credit. |
| Public children and generic external jobs | Defer | task 9.6 registry | The public concepts are future/non-v1; excluded runtime source is retained only for recovery. |
| Monolithic workflow builder | Replace/relocate | `OrcaCore.Core` typed authoring | Mode-specific staged authoring replaces the former builder. |
| DAG builder and durable runner | Defer | task 8.5, `OrcaCore.Dag*` | Package ownership exists, but runnable DAG semantics remain Section 8 work and receive no replacement credit yet. |
| Content-type serializer and raw runtime event-name catalog | Replace/relocate | fixed codec and event descriptors | One fixed codec and typed descriptors replace selectable serialization and raw names. |
| Broad management/query and legacy instance/statistics projections | Replace / relocate | provider operational store + ephemeral diagnostics | Forbidden application enumeration stays absent. Provider-authoritative durable pressure and internal ephemeral grouped statistics supply host/operator views and BCL gauges. |
| Application-replaceable ephemeral snapshotter | Remove | `OrcaCore.Engine.Ephemeral` | The fixed runtime-owned snapshot/codec path has no application serializer hook. |
| Legacy validation helper | Replace/relocate | `OrcaCore.Core` and `OrcaCore` strong values | The internal helper remains load-bearing in Core and was physically relocated; public strong values own their exact validation. |
| Reflection/name-dispatched construction bridges | Replace/relocate | typed application/Core/engine contracts | A closed generic authoring boundary plus exact application friends replace name dispatch and non-public constructor/property reflection. |
| Catch-all hosting package, registration, SDK facade, serializer options | Remove | mode-specific hosting | Explicit mode registration and host-owned SDK configuration replace the catch-all package. |
| Durable hosted loops and failure boundary | Replace/relocate | `OrcaCore.Durable.Hosting` | Durable progression moved outward into its owning hosting package. |
| BCL telemetry instruments, gauges, observer, logging | Replace / relocate | engine diagnostics + durable host observer | The SDK facade stays removed. Runtime owners emit the complete `orca.*` BCL catalog, structured logs, and exact spans; the host refreshes gauges from the same provider snapshot used for inspection. |
| PostgreSQL resource-ownership migration | Replace/relocate | PostgreSQL greenfield schema | `007_resource_governance.sql` is the first-create replacement, not compatibility DDL. |
| PostgreSQL registration extension | Replace/relocate | PostgreSQL provider package | The role-named provider registration extension is the supported owner. |
| RabbitMQ adapter project/package | Defer | task 7.17c | Source is orphaned and the package is outside v1; task 7.17c must restore/certify or approve deletion. |
| Redis adapter project/package | Defer | task 7.17c | Source is orphaned and the package is outside v1; task 7.17c must restore/certify or approve deletion. |
| Shared relational project/package | Defer | task 7.17c | PostgreSQL owns relocated helpers, while the orphaned diagnostic/project family still needs explicit disposition. |
| SQL Server project/package and migration | Defer | tasks 7.17c and 10.2 | Source/tests and the future certification obligation remain; no deletion credit is taken. |
| ZeroMQ adapter project/package | Defer | task 7.17c | Source is orphaned and the package is outside v1; task 7.17c must restore/certify or approve deletion. |
| Broker-adapter sample source | Replace/relocate | isolated broker-adapter sample project | The parent sample excludes the files because a package-isolated project compiles and tests them. |

## Interpretation rules

- **Remove** means normative absence plus executable source/metadata/package absence, without loss of
  retained runtime/provider/operator behavior.
- **Replace/relocate** names both the surviving owner and executable evidence. A similarly named
  method or a green build alone is not equivalence.
- **Defer** names an exact future task or registry, retains a recovery path, and earns no v1
  completion credit.
- **Dead/duplicate** requires a surviving exact owner and negative bridge evidence.
- `<Compile Remove>`, project/solution absence, compile failure, and aggregate passing counts are
  inventory facts only. None is proof of a valid disposition by itself.

The five provider trees are therefore classified, but not resolved as products. Task 7.17c remains
responsible for converting each `Defer` row into a certified active owner or an approved removal.
Likewise, the telemetry/statistics/retention rows make the current capability gap explicit and keep
task 8.5 load-bearing for the remaining DAG capability gap.
