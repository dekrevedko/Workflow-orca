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
| First durable-store plugin | **PostgreSQL** (`Npgsql`) | Append-only stream + JSONB projections fit natively; free; excellent Testcontainers story |
| First transport plugin | **RabbitMQ** (`RabbitMQ.Client`) | Canonical outbox target; exercises retry/poison paths well |

**TPL Dataflow** is *not* used in core or the Phase 2 outbox pump. The pump remains on
bounded channels plus `Task`; batching/backpressure can be revisited inside a future plugin
only if provider-specific pressure proves the dependency worthwhile. Resolved 2026-07-02,
IOQ-2.

## 2. Defaults (veto-able)

| Area | Default | Notes |
|------|---------|-------|
| DI | `Microsoft.Extensions.DependencyInjection.Abstractions` | Core registers against abstractions only; full container only in `OrcaCore.Hosting` |
| Logging | `Microsoft.Extensions.Logging.Abstractions` | No logger implementations in core; source-generated `LoggerMessage` for hot paths |
| Options/config | `Microsoft.Extensions.Options` | Engine options are plain records bound by the host |
| Serialization | **`System.Text.Json` with source generators** | The only serializer in core; payload/schema seams per spec PR-016; no polymorphic magic — explicit type discriminators |
| IDs | **`Guid.CreateVersion7()`** | Time-ordered GUIDs for `InstanceId`, `EventId`, `CommandId`, tickets — index-friendly in Postgres |
| Time | **`TimeProvider`** everywhere | No `DateTime.Now`/`UtcNow`/`Task.Delay(int)` in production code; tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) |
| Observability instrumentation | **BCL diagnostics in core/engines/providers; OpenTelemetry SDK only in hosting** (resolved 2026-07-02, IOQ-5) | Production projects may use `ILogger`, `System.Diagnostics.ActivitySource`, and `System.Diagnostics.Metrics.Meter` without adding OpenTelemetry package dependencies. Source/meter names use `OrcaCore`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Providers.<Name>`; custom tags use the `orca.` prefix. |
| Durable projections | **Same commit boundary as event append** (resolved 2026-07-02, IOQ-3) | Projection writes are included in the provider commit batch with events, checkpoint, inbox, and outbox records so routing/query correctness is immediately consistent after accepted mutations. |
| PostgreSQL event schema | **Single provider-owned `events` table for all instances; engine facts stored as `jsonb`; checkpoint business payloads stored as `bytea` with content type** (resolved 2026-07-02, IOQ-1) | Table-per-definition would leak workflow definitions into provider schema and complicate cross-definition management queries; JSONB keeps engine facts inspectable for projections/history, while checkpoint byte payloads preserve the explicit serialization seam. |
| Outbox pump implementation | **Channels + TPL only** (resolved 2026-07-02, IOQ-2) | Current pump requirements are satisfied without `System.Threading.Tasks.Dataflow`; avoiding a new dependency keeps the pump interface and failure model simple. |
| SQL plugin query helpers | **Raw Npgsql only; no Dapper** (resolved 2026-07-02, IOQ-4) | The PostgreSQL provider needs full control of SQL, transactions, and append/projection commit boundaries; no read-query complexity currently justifies adding Dapper. |
| Snapshot/approval testing | **No Verify dependency; use behavior-first AwesomeAssertions checks** (resolved 2026-07-02, IOQ-7) | Builder diagnostics and history projections remain asserted through stable codes, fields, and targeted message fragments; snapshot approval testing can be revisited only if broad text/layout churn becomes a real maintenance cost. |
| Lifecycle event durability split | **Ephemeral lifecycle events are in-process/queryable only; durable terminal and significant operator lifecycle events are outbox-backed in the same commit as state** (resolved 2026-07-02, spec open question 9) | Product lifecycle events are first-class records, not telemetry spans. Durable mode must commit terminal, pause/resume, wait-suspension/resume, timer, and step-failure/completion publications with the state transition; ephemeral mode exposes them from the active instance and does not promise restart survival. |
| Benchmarks | **BenchmarkDotNet in `benchmarks/OrcaCore.Benchmarks`; PR CI builds only** (resolved 2026-07-02, IOQ-8) | Benchmarks cover the ephemeral execution loop, provider serialization/materialization, management query/projection path, resource pool and timer scheduling, and provider commit path. Normal PR CI builds the benchmark project but does not run benchmarks. |
| Public packaging | **Deferred for this run** (resolved 2026-07-02, IOQ-9) | Package IDs, signing, SourceLink release configuration, README-per-package publishing work, and package publish workflows are intentionally skipped until the owner reopens packaging. Phase 6 may still complete provider, hosting, benchmark, sample, and readiness documentation work without publishing packages. |
| Management query predicates | **Expression facade over structured internal query model** (resolved 2026-07-02, IOQ-6) | Public APIs expose LINQ-like `Where(...)`; the durable engine translates the supported equality/`&&` subset into `WorkflowProjectionQuery` instead of compiling arbitrary delegates against payloads. |
| Test internals visibility | **Public-API-first; matching unit-test internals only** (resolved 2026-07-02, IOQ-11) | Acceptance tests use public surfaces only. `InternalsVisibleTo` is allowed only for a matching unit-test project when a module boundary requires internal model inspection. |
| Mocking | **Hand-rolled fakes first**, NSubstitute allowed | Fakes of ports live in a shared test-support project and double as executable documentation; NSubstitute only for narrow one-off stubs |
| Assertions | **AwesomeAssertions** (FluentAssertions API, Apache-2.0 community fork) | Same `FluentAssertions` namespace and `Should()` syntax — tests read as classic FluentAssertions; maintained and xUnit v3-aware. Original `FluentAssertions` v8+ is banned (commercial license); pinning original FA **7.x** (last Apache release) is the recorded fallback if the fork ever misbehaves. Plain xUnit `Assert` remains acceptable where clearer (e.g. structural checks) |
| Integration tests | **Testcontainers for .NET** | Postgres, RabbitMQ, later Redis/MSSQL/DynamoDB(-local) |
| Coverage | `coverlet.collector` | Reported in CI; no hard gate before Phase 2 |
| Package management | **Central Package Management** (`Directory.Packages.props`) | One version per package, repo-wide |
| Build props | Shared `Directory.Build.props` | `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `ImplicitUsings=enable` |
| Solution format | `.slnx` | Current dotnet CLI default-capable format |
| DynamoDB provider | **Deferred; preserve provider-port compatibility only** (resolved 2026-07-02, IOQ-10) | No DynamoDB package, task split, or provider implementation is scheduled for this run. Existing provider contracts must remain compatible with a future DynamoDB adapter: `ProviderCommitBatch` stays the atomic unit for expected-version append, events, checkpoint, inbox, outbox, and projection metadata; future DynamoDB work must implement those invariants without changing the shared ports. |

## 3. Library whitelist / banlist

**Core projects** (`Abstractions`, `Core`, engines, `Providers.InMemory`): BCL +
`Microsoft.Extensions.*` abstractions **only**. Nothing else, ever.

**Plugin projects** — each third-party client is confined to its plugin:

| Plugin | Package |
|--------|---------|
| `OrcaCore.Providers.PostgreSql` | `Npgsql` (raw ADO — no ORM, no Dapper: full SQL control for the append/commit boundary; resolved 2026-07-02, IOQ-4) |
| `OrcaCore.Providers.RabbitMq` | `RabbitMQ.Client` |
| `OrcaCore.Providers.Redis` / `OrcaCore.Providers.SqlServer` / `OrcaCore.Providers.ZeroMq` | `StackExchange.Redis` / `Microsoft.Data.SqlClient` / `NetMQ` |
| Future DynamoDB adapter | Deferred for this run; preserve provider-port compatibility only, no AWS package reference |

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
| IOQ-12 | K8s/EKS dispatcher plugin: official `KubernetesClient` (k8s-dotnet) vs raw HTTP | Phase 6 / scheduler app | Out of library scope until JS adapters are scheduled |

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

- 2026-07-02: **Spec open question 15 resolved** - DAG definitions compile each node as a
  durable child workflow instance using `RunChildren`-style orchestration. Rationale:
  child instances preserve per-node identity, lineage, retry isolation, restart-safe joins,
  and the Phase 4 resume-token/outbox model while avoiding a second in-instance DAG
  runtime. The DAG builder remains a compile front-end; it does not introduce new runtime
  semantics.
- 2026-07-02: **Spec open question 13 resolved** - Ephemeral saga support is in-process
  only and must be described as a limited mode in public XML documentation and implementation
  docs. It provides compensation semantics within one process lifetime, but no durable
  recovery, durable audit, or post-restart operator remediation guarantees.
- 2026-07-02: **Spec open question 1 resolved** - `Wait` and `WaitLong` remain separate
  public concepts. `WaitLong` is durable-only and represents cold-evictable, restart-safe
  waits. Implementations may share internals, but public surfaces keep the durable residency
  distinction explicit.
- 2026-07-02: **Spec open question 2 resolved** - Durable history is governed by explicit
  retention policy. Providers are not required to keep complete stream history forever, but
  must document retention behavior and preserve DU-071 inspection for the active retention
  window plus essential operational facts required for management, recovery, audit, and
  compliance-oriented queries.
- 2026-07-02: **Spec open question 4 resolved** - Fanout semantics are library-defined, but
  providers may use native fanout mechanics when available, such as RabbitMQ publish/routing,
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
- 2026-07-02: **Spec open question 12 resolved** - Phase 5 proceeds with the
  compensation-heavy saga track first. A process-manager-style, message-driven saga without
  compensation is not a prerequisite track for the current implementation program.
- 2026-07-02: **Spec open question 10 resolved** - Continue-as-new belongs with the durable
  core and AC-313 gates Slice 2. Since the current root implementation reached Phase 6
  without DU-042, T6-04 is a corrective backfill of an early durable requirement, not a
  decision to defer continue-as-new to production readiness.
- 2026-07-02: **IOQ-6 resolved** - Management `Where(...)` uses a public expression facade
  over a structured internal projection query model. The supported predicate subset is
  equality and `&&` over metadata fields; business payload predicate evaluation remains out
  of the hot query path.
- 2026-07-02: **IOQ-11 resolved** - Tests are public-API-first. Acceptance tests never use
  internals; `InternalsVisibleTo` is allowed only for matching unit-test projects where an
  internal module boundary would otherwise force public surface leakage.
- 2026-07-02: **IOQ-8 resolved** - BenchmarkDotNet coverage is limited to the ephemeral
  execution loop, provider serialization/materialization, management query/projection path,
  resource pool and timer scheduling, and provider commit path. The benchmark project lives
  at `benchmarks/OrcaCore.Benchmarks`. Normal PR CI builds it but does not run it.
- 2026-07-02: **IOQ-9 resolved** - Public package publishing is skipped for now by owner
  decision. Do not create package IDs, signing policy, package publish workflows, or
  per-package release README work unless the owner reopens packaging explicitly.
- 2026-07-02: **IOQ-10 resolved** - DynamoDB implementation is deferred for this run. Do not
  add AWS packages or DynamoDB task files now. Preserve compatibility by keeping the
  existing provider ports as the future DynamoDB adapter boundary: expected-version append,
  tail loading, checkpoint reads/writes, inbox, outbox, and projection metadata stay inside
  `ProviderCommitBatch` and related provider contracts. Future DynamoDB work must choose its
  table design when reopened and prove those same invariants through provider certification.
