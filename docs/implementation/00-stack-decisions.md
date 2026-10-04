# 00. Stack Decisions & Open Questions Register

Status values: **Decided** (confirmed with the product owner), **Default** (recommended,
veto-able — treat as decided unless overridden), **Open** (must be resolved at the noted
phase; do not improvise earlier).

## 1. Decided

| Area | Decision | Rationale |
|------|----------|-----------|
| Runtime | **.NET 10**, `LangVersion` latest (C# 14) | Product baseline; latest language features (see conventions doc) |
| Test framework | **xUnit v3** | Most agent training data; parallel-by-default; first-class `dotnet test` + Testcontainers |
| Concurrency substrate | **`System.Threading.Channels` + TPL** | Per-instance mailboxes, outbox pump, timer queue — all bounded channels + `Task`. Zero extra dependencies, simple failure model |
| Production durable-store plugins | **PostgreSQL** (`Npgsql`) and **SQL Server** (`Microsoft.Data.SqlClient`) | Both implement the complete split provider contract and run real-storage certification; provider-native SQL remains isolated in its owning package |
| Transport integration | **Application/companion-owned `IWorkflowEventDispatcher` adapters** | OrcaCore owns durable outbox retry/poison semantics but ships no broker SDK package; the isolated sample demonstrates the boundary. |
| First-release workflow surface | **Typed workflow input/output, root `Parallel`/`WhenAll`/`WhenAllOutcomes`, root bounded `ForEach`, root `While`, nested `If`, scoped durable resource acquisition, workflow/step deadlines, and stable step-operation identity** | Smallest coherent surface for the initial ephemeral engine and advanced durable scheduler use case. Of the conditional/loop/fanout operators, only `If` nests; `WaitLong`, `Yield`, public `RunExternalJob`, public `RunChildren`, Saga, `WhenFirst`, nested `Parallel`, nested `While`, and nested `ForEach`/dynamic expansion are not first-release members. |
| DAG packaging | **`OrcaCore.Dag` plus `OrcaCore.Dag.Hosting` are separate first-release projects in `OrcaCore.slnx`** | `OrcaCore.Dag` is the typed compile front-end. `OrcaCore.Dag.Hosting` is the only friend bridge to the versioned internal child start/join seam in `OrcaCore.Durable.Hosting`; no public `RunChildren` member is exposed. |
| External scheduler boundary | **Kubernetes, EKS/AWS, and job-adapter code lives in a separate outward-dependent companion project, which may remain in this solution initially** | `OrcaCore`, `OrcaCore.Dag`, engines, hosting, and provider packages never reference Kubernetes or AWS SDKs. The companion scheduler consumes public OrcaCore contracts. |
| Durable external-call identity | **Runtime-created `StepOperationId` plus diagnostic `AttemptNumber`** | One logical step visit keeps the same operation ID across retry, replay, crash recovery, and host replacement; loop re-entry, each `ForEach` item/branch, and continue-as-new use new IDs. External adapters implement create-or-observe idempotency; OrcaCore does not claim exactly-once external effects. |
| Durable resource lifetime | **Scoped-only `AcquireResources(request, body)`, no author TTL and no renewal** | Release follows scope exit. Pool review deadlines mark/reconcile but time never frees active or ambiguous ownership. Cancellation or deadline keeps capacity quarantined until protected work is proven stopped or fenced. |
| Definition immutability | **A `(DefinitionId, DefinitionVersion)` has one immutable compiled fingerprint** | Changed step or job-construction logic requires a new definition version, so replaying a stable operation ID cannot silently produce different external intent. |

**TPL Dataflow** is *not* used in core or the Phase 2 outbox pump. The pump remains on
bounded channels plus `Task`; batching/backpressure can be revisited inside a future plugin
only if provider-specific pressure proves the dependency worthwhile. Resolved 2026-07-02,
IOQ-2.

## 2. Defaults (veto-able)

| Area | Default | Notes |
|------|---------|-------|
| DI | `Microsoft.Extensions.DependencyInjection.Abstractions` | Core registers against abstractions only; full-container integration belongs only to the role-specific hosting assemblies. `OrcaCore.Hosting` is a shared CLR namespace across approved assemblies, not a PackageId. |
| Logging | `Microsoft.Extensions.Logging.Abstractions` | No logger implementations in core; source-generated `LoggerMessage` for hot paths |
| Options/config | `Microsoft.Extensions.Options` | Hosts construct role-specific options programmatically; registration copies and validates immutable option values. The v1 contract makes no configuration-binder claim and exposes no binder-oriented registration overload. |
| Workflow-state codec | **Fixed certified `System.Text.Json` codec, format `orcacore-json-v1`** | The first-release workflow-state codec is not host-replaceable. Registration rejects unsupported cyclic/polymorphic shapes and certifies deterministic bytes plus detached round trips. Each attempt receives a codec-detached copy of committed state; only a successful winning attempt commits its copy. |
| IDs | **`Guid.CreateVersion7()`** | Time-ordered GUIDs for `InstanceId`, `EventId`, `CommandId`, tickets — index-friendly in Postgres |
| Definition identity | **Factory/constructor-validated immutable reference values** | `DefinitionId` has runtime creation/parse factories; `DefinitionVersion` rejects non-positive values. Neither permits `default(struct)` to bypass invariants. |
| Time | **`TimeProvider`** everywhere | No `DateTime.Now`/`UtcNow`/`Task.Delay(int)` in production code; tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) |
| Observability instrumentation | **BCL diagnostics in product packages; OpenTelemetry SDK/exporters are host-owned** (reconciled 2026-07-19, IOQ-5) | Product projects may use `ILogger`, `System.Diagnostics.ActivitySource`, and `System.Diagnostics.Metrics.Meter` without adding OpenTelemetry package dependencies. The exact first-release manifest exposes no OrcaCore OTel package or registration facade. Source/meter names use `OrcaCore`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Providers.<Name>`; custom tags use the `orca.` prefix. |
| Durable projections | **Same commit boundary as event append** (resolved 2026-07-02, IOQ-3) | Projection writes are included in the provider commit batch with events, checkpoint, inbox, and outbox records so routing/query correctness is immediately consistent after accepted mutations. |
| PostgreSQL event schema | **Single provider-owned `events` table for all instances; engine facts stored as `jsonb`; checkpoint business payloads stored as `bytea` with content type** (resolved 2026-07-02, IOQ-1) | Table-per-definition would leak workflow definitions into provider schema and complicate cross-definition management queries; JSONB keeps engine facts inspectable for projections/history, while checkpoint byte payloads preserve the explicit serialization seam. |
| Outbox pump implementation | **Channels + TPL only** (resolved 2026-07-02, IOQ-2) | Current pump requirements are satisfied without `System.Threading.Tasks.Dataflow`; avoiding a new dependency keeps the pump interface and failure model simple. |
| SQL plugin query helpers | **Provider-native raw clients: Npgsql for PostgreSQL and Microsoft.Data.SqlClient for SQL Server; Dapper remains confined to PostgreSQL's internal migration journal** (amended 2026-08-11, IOQ-4) | Provider transactions, locking, append/projection queries, and SQL Server migration behavior remain explicit; no shared Relational compatibility project or application-query abstraction is restored. |
| Snapshot/approval testing | **No Verify dependency; use behavior-first AwesomeAssertions checks** (resolved 2026-07-02, IOQ-7) | Builder diagnostics and history projections remain asserted through stable codes, fields, and targeted message fragments; snapshot approval testing can be revisited only if broad text/layout churn becomes a real maintenance cost. |
| Lifecycle event durability split | **Ephemeral lifecycle events are in-process/queryable only; durable terminal and significant lifecycle events are outbox-backed in the same commit as state** (resolved 2026-07-02, spec open question 9) | Product lifecycle events are first-class records, not telemetry spans. Durable mode commits terminal, cancellation-request, wait-suspension/resume, timer, and step-failure/completion publications with state. Public pause/resume/retry/archive/purge are deferred. |
| Benchmarks | **BenchmarkDotNet in `benchmarks/OrcaCore.Benchmarks`; PR CI builds only** (resolved 2026-07-02, IOQ-8) | Benchmarks cover the ephemeral execution loop, provider serialization/materialization, management query/projection path, resource pool and timer scheduling, and provider commit path. Normal PR CI builds the benchmark project but does not run benchmarks. |
| Packaging and publishing | **Local package graph, package IDs, packing, and clean-consumer certification ship in v1; external publishing is deferred** (amended 2026-07-18, IOQ-9) | Every documented tier must pack and be consumed from local artifacts before release approval. Signing, SourceLink release configuration, registry publication, semantic-version release automation, and public package-release workflows remain deferred until explicitly reopened. |
| Management query predicates | **No public instance enumeration, filtering, counting, statistics, or bulk management in v1** (superseded 2026-07-19, IOQ-6) | Advanced provider internals expose only exact runtime lookup, active-wait routing, and trusted lease-recovery candidate operations; no generic projection-query or public LINQ-like `Where(...)` facade ships in v1. |
| Internal visibility | **Public-API-first with one exact closed friend graph; ninth runtime-view independently source-approved at `4eb2e8d3a68e0ae7d873bd4f54c53735beefb132`** (amended 2026-08-11, Decision 22; authoring-friend source independently approved 2026-09-28) | The current nine product friends are `OrcaCore`→Core, Ephemeral Engine, Durable Engine, and `OrcaCore.Dag`; Core→both engines; Durable Engine→Durable Hosting; Durable Hosting→DAG Hosting; and Dag→Dag.Hosting for the exact runtime view. The authoring-only `OrcaCore`→`OrcaCore.Dag` grant is constrained to five compiler-created value families and one shared fingerprint operation by exact compiled-type/member evidence; Task 8.2 checkpoint `a9f835f939d683500ca231c7ba491ab8eae2aaae` is independently approved. Durable Hosting→DAG Hosting remains the sole DAG-to-durable runtime bridge. Exact owning white-box test friends cover Core, both engines, Durable Hosting, PostgreSQL, and SQL Server; Durable Engine→ProviderCertification is the sole cross-package test edge. Acceptance, behavior-scenario, compile-fixture, and integration projects receive no internals. |
| Mocking | **Hand-rolled fakes first**, NSubstitute allowed | Fakes of ports live in a shared test-support project and double as executable documentation; NSubstitute only for narrow one-off stubs |
| Assertions | **AwesomeAssertions** (FluentAssertions API, Apache-2.0 community fork) | Same `FluentAssertions` namespace and `Should()` syntax — tests read as classic FluentAssertions; maintained and xUnit v3-aware. Original `FluentAssertions` v8+ is banned (commercial license); pinning original FA **7.x** (last Apache release) is the recorded fallback if the fork ever misbehaves. Plain xUnit `Assert` remains acceptable where clearer (e.g. structural checks) |
| Integration tests | **Testcontainers for .NET** | PostgreSQL and SQL Server are the exact v1 production-provider container lanes; each runs shared certification plus provider-native restart and competing-host tests. |
| Coverage | `coverlet.collector` | Reported in CI; no hard gate before Phase 2 |
| Package management | **Central Package Management** (`Directory.Packages.props`) | One version per package, repo-wide |
| Build props | Shared `Directory.Build.props` | `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `ImplicitUsings=enable` |
| Solution format | `.slnx` | Current dotnet CLI default-capable format |
| DynamoDB provider | **Deferred; preserve provider-port compatibility only** (resolved 2026-07-02, IOQ-10) | No DynamoDB package, task split, or provider implementation is scheduled for this run. Existing provider contracts must remain compatible with a future DynamoDB adapter: `ProviderCommitBatch` stays the atomic unit for expected-version append, events, checkpoint, inbox, outbox, and projection metadata; future DynamoDB work must implement those invariants without changing the shared ports. |
| Authoring concurrency | **Host owns `MaxConcurrentExecutionPathsPerInstance`; no author-global cap** (resolved 2026-07-18) | Fixed root `Parallel` branches are admitted in authored order. A root `ForEach` node may set positive `maxConcurrency`; the effective limit is the lower of the node value and host ceiling. Future DAG scheduling uses its separate host policy. |
| Timeouts | **`CompleteWithin(...)` for workflow deadlines and `WithStepTimeout(...)` for step attempts; no orchestration dependency on Polly** (resolved 2026-07-18) | Durable timeout decisions are persisted runtime state driven by `TimeProvider`. A step timeout bounds an invocation and cannot prove that an ambiguous external side effect stopped. |
| Inline lambda steps | **Ephemeral authoring only in v1** (resolved 2026-07-18) | Inline lambdas improve the in-process quick path without pretending arbitrary closures are durable, replay-safe definitions. Durable workflows use registered typed step implementations. |
| Hosting registration | **Role-specific entry points only** (amended 2026-08-11) | `OrcaCore.Engine.Ephemeral` owns `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`; `OrcaCore.Durable.Hosting` owns `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` and callback-only `AddOrcaCoreDurableEventIngress()`; `OrcaCore.Providers.InMemory` owns development/test `AddOrcaCoreInMemoryDurableProvider()`; `OrcaCore.Providers.PostgreSql` owns production `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`; `OrcaCore.Providers.SqlServer` owns production `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`; and `OrcaCore.Dag.Hosting` owns `AddOrcaCoreDag(DagHostOptions)`. There is no catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, codec replacement hook, or configuration-binder facade. |
| Event routing and deduplication | **Durable self-routing ingress with retained pre-wait ownership** (resolved 2026-07-18; amended by Section 7B) | `IWorkflowEventIngress` accepts a fixed-codec `WorkflowInboundEvent` through direct, correlation, definition-fanout, or start-or-deliver routing. Global `EventId` plus the full normalized-envelope fingerprint is checked before route state; accepted events persist until applied or observably poisoned, including when no wait is active. Fanout snapshots the complete current nonterminal target set once and deduplicates per target. Only `Accepted` or `Duplicate` permits source acknowledgement. |
| Durable resource governance | **One serialized governance aggregate per configured provider partition** (resolved 2026-07-18) | The aggregate owns every pool, atomic multi-pool queue, ticket, review mark, resize, confirmation, and tombstone. `OrcaCore.Runtime.Protocol` owns records; `OrcaCore.Provider.Abstractions` owns the expected-version load/atomic-append store SPI. There is no force release or time-only reclaim. |

## 3. Library whitelist / banlist

**Core library projects** (application contracts, `Core`, engines, durable hosting,
`OrcaCore.Dag`, `OrcaCore.Dag.Hosting`, `OrcaCore.Runtime.Protocol`,
`OrcaCore.Provider.Abstractions`, and `Providers.InMemory`): BCL +
`Microsoft.Extensions.*` abstractions **only**. Nothing else, ever.

**Plugin projects** — each third-party client is confined to its plugin:

| Plugin | Package |
|--------|---------|
| `OrcaCore.Providers.PostgreSql` | `Npgsql` for provider SQL plus Dapper only inside the internal migration journal runner; no ORM or application-query abstraction |
| `OrcaCore.Providers.SqlServer` | `Microsoft.Data.SqlClient` for provider-native SQL; no ORM, shared Relational project, or application-query abstraction |
| Future durable storage provider beyond PostgreSQL and SQL Server, including DynamoDB | Deferred; no provider project or SDK reference exists until a future amendment certifies the complete current port and greenfield schema |
| Application/companion transport adapter | Its selected broker SDK; never an OrcaCore package dependency |
| Companion Kubernetes scheduler project(s) | Official Kubernetes client and optional AWS SDKs are allowed only in the separate scheduler/application boundary; never in an OrcaCore application, engine, provider, advanced, or DAG package |

**Banned everywhere** (agents: do not add these even if they seem convenient):
`Newtonsoft.Json`, `AutoMapper`, `MediatR`, `FluentAssertions` **v8+** (commercial license —
use `AwesomeAssertions`, or pinned FA 7.x as fallback), `Moq`, any ORM
(EF Core, Dapper) in core, any IoC container other than MS DI, reflection-based plugin
discovery/scanning (plugins are registered explicitly).

## 4. Open questions register (implementation-level)

Prefix **`IOQ-`** (implementation open question) — deliberately distinct from the spec's
open questions, which are referenced as "spec open question N" (spec document 13 §13.2).
The two registers never share numbers.

| # | Question | Resolve at | Constraint while open |
|---|----------|-----------|----------------------|
| - | No unresolved implementation-level question currently blocks the first-release planning baseline. | - | New questions must be added here before implementation chooses a contract. |

Spec-level open questions live in [specs/13-phasing-and-open-questions.md](../specs/13-phasing-and-open-questions.md)
(§13.2) and are **not** repeated here; when a task touches one, the task file must say which
resolution it assumes.

## 5. Decision log protocol

When an `IOQ-` item is resolved: move it to Decided/Default with one line of rationale,
note the date, and update any task files that referenced it. When a **spec** open question
is resolved during implementation, log the resolution here (one dated line) **and** mark
the question resolved in spec §13.2 — the spec stays the authority; this file is the
crosswalk. Agents MUST NOT resolve open questions of either register implicitly inside a
task; if a task cannot proceed without a resolution, stop and surface it.

- 2026-07-18: **Spec open question 15 final** - `OrcaCore.Dag` compiles each node to an internal
  durable child start/join protocol. The separate `OrcaCore.Dag.Hosting` package is the only friend
  bridge to that protocol; no public child-workflow authoring member ships in v1.
- 2026-07-18: **Spec open question 13 final** - Saga is deferred from both first-release modes and
  remains recorded in the future-capability registry. Re-entry requires one approved typed
  authoring, compensation, recovery, audit, cancellation, and remediation contract.
- 2026-07-18: **Spec open question 1 final** - V1 has one ordinary `Wait`. Durable residency is a
  runtime and host-policy choice; authors do not select a second duration-based wait concept.
- 2026-07-02: **Spec open question 2 resolved** - Durable history is governed by explicit
  retention policy. Providers are not required to keep complete stream history forever, but
  must document retention behavior and preserve DU-071 inspection for the active retention
  window plus essential operational facts required for management, recovery, audit, and
  compliance-oriented queries.
- 2026-07-02: **Spec open question 4 resolved** - Fanout semantics are library-defined, but
  storage providers may use native set/snapshot mechanics when available,
  if OrcaCore delivery, deduplication, batching, correlation, and observability contracts are
  preserved. Providers without native fanout use projection-driven command emission with
  pagination.
- 2026-07-02: **Spec open question 6 resolved** - Policy decorators use fluent builder
  calls plus explicit metadata objects. Attributes and reflection-based policy discovery are
  out of scope for this implementation track.
- 2026-07-02: **Spec open question 7 resolved** - Durable/ephemeral mode separation should
  be enforced at compile time as far as practical without doubling every abstraction.
  Durable-only and ephemeral-only features use distinct public authoring/runtime entry
  points when that keeps APIs clear; genuinely shared contracts stay shared, with runtime
  validation allowed where separate abstraction trees would add noise rather than safety.
- 2026-07-18: **Spec open question 12 final** - No Saga implementation track is part of the first
  release. A future amendment must approve typed action/results, reverse progression, restart
  recovery, cancellation, audit, and remediation before adding a public builder, definition,
  adapter, or source task.
- 2026-07-02: **Spec open question 10 resolved** - Continue-as-new belongs with the durable
  core and AC-313 gates Slice 2. Since the current root implementation reached Phase 6
  without DU-042, T6-04 is a corrective backfill of an early durable requirement, not a
  decision to defer continue-as-new to production readiness.
- 2026-07-02: **IOQ-6 originally resolved, superseded 2026-07-19** - the proposed public
  management `Where(...)` expression facade is deferred with all public instance enumeration,
  filtering, counting, statistics, and bulk management. Advanced provider internals retain only
  exact runtime lookup, active-wait routing, and trusted lease-recovery candidate operations;
  these are not application APIs.
- 2026-08-01: **IOQ-11 amended by Decision 22** - Tests remain public-API-first. The exact product,
  owning-white-box-test, DAG-host, and ProviderCertification friend graph prevents implementation
  types from becoming public; acceptance, behavior-scenario, compile-fixture, and integration
  projects receive no internals.
- 2026-09-27: **Decision 22 authoring-friend amendment proposed, not yet approved** -
  `admit-dag-authoring-friend-boundary` would add only `OrcaCore -> OrcaCore.Dag` for guarded
  DAG build-value construction. The current product friends are exactly
  `OrcaCore -> OrcaCore.Core`, `OrcaCore -> OrcaCore.Engine.Ephemeral`,
  `OrcaCore -> OrcaCore.Engine.Durable`, `OrcaCore.Core -> OrcaCore.Engine.Ephemeral`,
  `OrcaCore.Core -> OrcaCore.Engine.Durable`,
  `OrcaCore.Engine.Durable -> OrcaCore.Durable.Hosting`, and
  `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting`. That final edge remains the sole
  DAG-to-durable runtime bridge. This entry records the proposal state before independent review.
- 2026-09-27: **Decision 22 authoring-friend contract independently approved** - the
  `OrcaCore -> OrcaCore.Dag` authoring edge is approved as a contract but remains absent from
  compiled metadata pending its separately reviewed implementation and exact member-reference guard.
- 2026-09-28: **Decision 22 authoring-friend source candidate** - the separately frozen Task 8.2
  candidate compiles the approved eighth edge and exact member-reference guard. This dated
  entry does not claim independent source approval or a committed implementation checkpoint.
- 2026-09-29: **Decision 22 authoring-friend source approved and checkpointed** - Task 8.2
  checkpoint `a9f835f939d683500ca231c7ba491ab8eae2aaae` retains the independently approved
  tree `cf11b3f6732f11250ba3a0255114bcdc4ae00060`; direct-child evidence
  `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8` adds the 2026-09-28 ZZZ-remediation
  `APPROVE` verdict byte-exact. This supersedes the candidate status, not the dated entry's
  historical bytes. The authoring-only grant provides no codec, runtime, or child-start access.
- 2026-10-02: **Decision 22 runtime-view contract proposed, not approved** -
  `admit-dag-hosting-runtime-view` proposes `OrcaCore.Dag -> OrcaCore.Dag.Hosting` for
  three closed internal types and fifteen exact method/getter signatures: immutable node
  descriptors, one mapping evaluator and a mapped-input result. The current eight friends
  and six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist remain unchanged. No
  mapper delegate, draft, constructor, test friend, codec or child-start access is added
  to Dag. The durable bridge decodes successful committed outputs before mapping and owns
  codec normalization, committed-byte fingerprints and child start. This supersedes no
  historical approval; contract, atomic sync and source require their own reviewed gates.
- 2026-07-02: **IOQ-8 resolved** - BenchmarkDotNet coverage is limited to the ephemeral
  execution loop, provider serialization/materialization, management query/projection path,
  resource pool and timer scheduling, and provider commit path. The benchmark project lives
  at `benchmarks/OrcaCore.Benchmarks`. Normal PR CI builds it but does not run it.
- 2026-07-02: **IOQ-9 resolved** - Public package publishing was initially skipped by owner
  decision. **Amended 2026-07-18:** v1 still defines package IDs and must locally pack every
  documented tier and certify clean application/provider/custom-host/DAG consumers. Signing,
  SourceLink release configuration, registry publication, and release automation remain deferred.
- 2026-07-02: **IOQ-10 resolved** - DynamoDB implementation is deferred for this run. Do not
  add AWS packages or DynamoDB task files now. Preserve compatibility by keeping the
  existing provider ports as the future DynamoDB adapter boundary: expected-version append,
  tail loading, checkpoint reads/writes, inbox, outbox, and projection metadata stay inside
  `ProviderCommitBatch` and related provider contracts. Future DynamoDB work must choose its
  table design when reopened and prove those same invariants through provider certification.
- 2026-07-18: **IOQ-12 closed as outside OrcaCore.** Kubernetes client selection, EKS
  credentials, manifests, watchers, and stop reconciliation belong to the separate companion
  scheduler project. No Kubernetes or AWS package is added to OrcaCore. That project may use
  the official Kubernetes client or raw HTTP according to its own acceptance evidence without
  changing the OrcaCore public contract.

- 2026-10-03: **Decision 22 runtime-view contract approved; canonical transition prepared, source pending** - contract checkpoint `43d869fc29e7daa3ec567d4602960f458eb98492`, sole-parent evidence `1ea7f44b5a32d058e04b913f387317c21747add5`, and checkbox-only activation `64d97c644475f6dfbe183540bf546c7ab48eab46` retain the immutable 2026-10-02 APPROVE. The two exact canonical headings now contain that approved contract in the uncommitted atomic 1.3/1.4 target. Current compiled friends remain eight; only the separately reviewed source target may compile the ninth `OrcaCore.Dag -> OrcaCore.Dag.Hosting` view. Codec/materialization and child work remain on the durable bridge. The 2026-10-02 proposal entry is retained verbatim as history.

- 2026-10-04: **Decision 22 runtime-view source candidate after canonical approval** - independently approved atomic checkpoint `a0da21ba9597e3864a3d4134120fbb0138417bd7`, evidence `6c8bcd02bf6c747800c17dbc27afce53d24efbc1`, and checkbox-only activation `13fe5b996e4758e383ea6ac68d86bdaf10b940b8` authorize preparing this source target. The candidate compiles the ninth `OrcaCore.Dag -> OrcaCore.Dag.Hosting` grant, three internal runtime-view types and fifteen exact consumed signatures. It awaits its own independent source approval/checkpoint; codec, committed input and child-start/join remain later bridge work. Earlier dated proposal and prepared-transition entries remain byte-exact history.

- 2026-10-04: **Decision 22 runtime-view source approval and closeout preparation** - checkpoint `4eb2e8d3a68e0ae7d873bd4f54c53735beefb132` has the independently approved tree `bd378abbadc5ba6989831814be1e67db0c9dcfa9`; direct-child evidence `2fdfa59a747a5d1d1667abd63a8def88168a172b` adds the byte-exact 2026-10-04 APPROVE, and activation `1d8941b7c4e4a96752b33684075d152494057c49` checks Task 2.4 only. All nine product grants are current and source-approved. The Complete registry promotion is prepared for its own independent closeout review; durable bridge, codec/commit and child-start/join work remain later reviewed slices. Earlier dated entries remain unchanged.
