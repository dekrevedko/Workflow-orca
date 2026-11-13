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
| Physically deleted production paths | 134 | union of recovery/target trees compared with the current filesystem |
| Production `<Compile Remove>` entries | 1 | every `src/` and `samples/` project parsed as XML |
| Orphaned production roots | 0 | source/SQL roots with no project file |
| Retired/deferred package artifacts | 5 | deleted project identities absent from the exact v1 manifest |
| Retired public/member symbols | 122 | exact current or historical qualified owners, ordered inventory, and SHA-256 pinned by harmonization task 7.6 |
| Removed internal result placeholders | 4 | ordered inventory and SHA-256 pinned |
| Forbidden internal bridge types | 9 | ordered inventory and SHA-256 pinned |
| Removed hosting and codec types | 3 | manifest-wide ordered inventory and SHA-256 pinned |
| Unresolved entries | **0** | the token is rejected case-insensitively by the guard |

The symbol inventories intentionally remain in `PublicApiBaselineGuards.cs`, where source and
metadata absence are executable. The ledger pins their field names, counts, ordered content hashes,
owners, and test methods so they cannot drift independently.

Every declaration contained by a physically deleted `.cs` path inherits that path's one family
disposition; this is enforced by the exact 134-path recovery/target comparison. The four ordered symbol
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
| RabbitMQ adapter project/package | Remove | application-owned dispatcher adapters | The provisional broker-SDK package and inactive tests are deleted; transport mapping stays outward through `IWorkflowEventDispatcher` and the isolated broker-adapter sample. |
| Redis adapter project/package | Remove | `docs/specs/13-phasing-and-open-questions.md` §13.4 | The obsolete projection-only adapter and inactive tests are deleted. Any future Redis-backed durable provider must re-enter through the provider registry and certify the complete current port. |
| Shared relational project/package | Replace/relocate | `OrcaCore.Providers.PostgreSql/Internal` | PostgreSQL owns the migration helpers; the duplicate orphan diagnostic root is deleted. |
| Provisional SQL Server project/package and migration | Replace/relocate | `OrcaCore.Providers.SqlServer` | The incomplete provisional provider and inactive tests remain deleted. The replacement provider implements the complete current split ports, owns one SQL Server-native greenfield schema, and carries current-release package/shared/real-storage certification; no deleted provisional source was restored. |
| ZeroMQ adapter project/package | Remove | application-owned dispatcher adapters | The provisional broker-SDK package and inactive tests are deleted; transport mapping stays outward through `IWorkflowEventDispatcher` and the isolated broker-adapter sample. |
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

The four still-retired provider trees remain classified outside the current manifest. The deleted
provisional SQL Server tree is separately classified `Replace/relocate`: its old source stays
deleted while the new complete current-port provider owns the active replacement. Both ledger
representations and their retired-root/package assertions changed atomically. Task 8.5
remains load-bearing for the remaining DAG capability gap.
