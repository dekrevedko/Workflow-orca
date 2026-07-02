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
| Durable projections | **Same commit boundary as event append** (resolved 2026-07-02, IOQ-3) | Projection writes are included in the provider commit batch with events, checkpoint, inbox, and outbox records so routing/query correctness is immediately consistent after accepted mutations. |
| PostgreSQL event schema | **Single provider-owned `events` table for all instances; engine facts stored as `jsonb`; checkpoint business payloads stored as `bytea` with content type** (resolved 2026-07-02, IOQ-1) | Table-per-definition would leak workflow definitions into provider schema and complicate cross-definition management queries; JSONB keeps engine facts inspectable for projections/history, while checkpoint byte payloads preserve the explicit serialization seam. |
| Outbox pump implementation | **Channels + TPL only** (resolved 2026-07-02, IOQ-2) | Current pump requirements are satisfied without `System.Threading.Tasks.Dataflow`; avoiding a new dependency keeps the pump interface and failure model simple. |
| SQL plugin query helpers | **Raw Npgsql only; no Dapper** (resolved 2026-07-02, IOQ-4) | The PostgreSQL provider needs full control of SQL, transactions, and append/projection commit boundaries; no read-query complexity currently justifies adding Dapper. |
| Snapshot/approval testing | **No Verify dependency; use behavior-first AwesomeAssertions checks** (resolved 2026-07-02, IOQ-7) | Builder diagnostics and history projections remain asserted through stable codes, fields, and targeted message fragments; snapshot approval testing can be revisited only if broad text/layout churn becomes a real maintenance cost. |
| Mocking | **Hand-rolled fakes first**, NSubstitute allowed | Fakes of ports live in a shared test-support project and double as executable documentation; NSubstitute only for narrow one-off stubs |
| Assertions | **AwesomeAssertions** (FluentAssertions API, Apache-2.0 community fork) | Same `FluentAssertions` namespace and `Should()` syntax — tests read as classic FluentAssertions; maintained and xUnit v3-aware. Original `FluentAssertions` v8+ is banned (commercial license); pinning original FA **7.x** (last Apache release) is the recorded fallback if the fork ever misbehaves. Plain xUnit `Assert` remains acceptable where clearer (e.g. structural checks) |
| Integration tests | **Testcontainers for .NET** | Postgres, RabbitMQ, later Redis/MSSQL/DynamoDB(-local) |
| Coverage | `coverlet.collector` | Reported in CI; no hard gate before Phase 2 |
| Package management | **Central Package Management** (`Directory.Packages.props`) | One version per package, repo-wide |
| Build props | Shared `Directory.Build.props` | `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `ImplicitUsings=enable` |
| Solution format | `.slnx` | Current dotnet CLI default-capable format |

## 3. Library whitelist / banlist

**Core projects** (`Abstractions`, `Core`, engines, `Providers.InMemory`): BCL +
`Microsoft.Extensions.*` abstractions **only**. Nothing else, ever.

**Plugin projects** — each third-party client is confined to its plugin:

| Plugin | Package |
|--------|---------|
| `OrcaCore.Providers.PostgreSql` | `Npgsql` (raw ADO — no ORM, no Dapper: full SQL control for the append/commit boundary; resolved 2026-07-02, IOQ-4) |
| `OrcaCore.Providers.RabbitMq` | `RabbitMQ.Client` |
| Later: Redis / MSSQL / DynamoDB / ZeroMQ | `StackExchange.Redis` / `Microsoft.Data.SqlClient` / `AWSSDK.DynamoDBv2` / `NetMQ` |

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
| IOQ-5 | Observability: OpenTelemetry (`ActivitySource`/`Meter`) naming scheme and what's in core vs hosting | Phase 3 start | Core emits via `ILogger` abstractions only until decided |
| IOQ-6 | Management `Where(...)`: expression-tree subset compiler vs source-generated query model | Phase 1 T1-13 (start simple: structured internal model + expression facade, per spec MG-002) | Public shape is fixed by MG-002; only the translation mechanism is open |
| IOQ-8 | BenchmarkDotNet micro-benchmarks: which hot paths, and CI treatment | Phase 6 | None before Phase 6 (NF-030: correctness first) |
| IOQ-9 | Public packaging: package IDs, signing, SourceLink, README-per-package | Phase 6 | Never publish before the Slice 6 gate (NF-003) |
| IOQ-10 | DynamoDB single-table design for the event stream | When that plugin is scheduled | Not before MSSQL/Redis plugins |
| IOQ-11 | `InternalsVisibleTo` for tests vs public-API-only testing | Phase 0 T0-01 sets the default: **public-API-first**; `InternalsVisibleTo` granted only to the matching unit-test project | Acceptance tests NEVER use internals |
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
