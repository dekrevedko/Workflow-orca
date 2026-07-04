# R8 — .NET 10 / C# Standards & Feature Usage — Findings

> Specialty review (outside the R0–R7 phase table). Scope: all `v3-gpt/src/**`,
> `v3-gpt/tests/**` (TDD/async discipline only), `Directory.Build.props`, `.editorconfig`,
> `global.json`, and `benchmarks/OrcaCore.Benchmarks`. Authority:
> [02-engineering-conventions](../implementation/02-engineering-conventions.md) §1–§6,
> [00-stack-decisions](../implementation/00-stack-decisions.md) §1–§3 (IDs, serialization,
> Channels, observability), [03-tdd-workflow](../implementation/03-tdd-workflow.md) §4.
> Review only — no code changes.

## Baseline

| Check | Result |
|-------|--------|
| `dotnet build v3-gpt/OrcaCore.slnx` (from `v3-gpt/`) | **Succeeded**, 0 warnings, 0 errors |
| `dotnet build` from repo root | **Fails** — root `global.json` pins SDK `10.0.200`; installed `10.0.301` |
| `v3-gpt/global.json` | Pins `10.0.301` (overrides root when cwd is `v3-gpt/`) |

## Findings

### [P1] PostgreSQL store validates connection string after `NpgsqlDataSource.Create` — `PostgreSqlWorkflowStore.cs:68`
- **Requirement/convention:** 02 §2 / BCL constructor guard discipline
- **Evidence:**
  ```csharp
  public PostgreSqlWorkflowStore(string connectionString)
      : this(NpgsqlDataSource.Create(connectionString))
  {
      ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
  }
  ```
  Same pattern in `PostgreSqlResourcePoolStore.cs:23`.
- **Failure scenario:** Caller passes `null` or `""`; data source is constructed before validation runs; failure mode depends on Npgsql internals instead of a deterministic `ArgumentException`.
- **Recommendation:** Validate in a static factory or use a guarded local before `Create`, e.g. `ArgumentException.ThrowIfNullOrWhiteSpace(connectionString); : this(NpgsqlDataSource.Create(connectionString))` is impossible — use a private static `CreateDataSource(string)` that validates first.
- **Confidence:** CONFIRMED

### [P2] Stack decision mandates STJ source generators; implementation uses reflection serializers — `InMemoryWorkflowProvider.cs:372`
- **Requirement/convention:** 00 §2 Serialization / 02 §1
- **Evidence:** No `[JsonSerializable]` / `JsonSerializerContext` in the solution. Payload and event paths use `JsonSerializer.Serialize/Deserialize` with runtime `GetType()` in providers (`PostgreSqlWorkflowStore.cs:1468`, `SqlServerWorkflowStore.cs:909`). `IWorkflowPayloadSerializer` uses bare `JsonSerializer.SerializeToUtf8Bytes(payload)` with no shared options (`InMemoryWorkflowProvider.cs:372`).
- **Failure scenario:** Hot-path allocations and trim/AOT unfriendly code; serializer options drift between providers; harder to enforce PR-016 explicit discriminator policy under trimming.
- **Recommendation:** Introduce a shared `OrcaCoreJsonSerializerContext` (source-generated) for workflow events, snapshots, and checkpoint payloads; route all provider and `IWorkflowPayloadSerializer` calls through it.
- **Confidence:** CONFIRMED

### [P2] Strongly-typed ID JSON converters duplicated per provider — `StronglyTypedIdJsonConverters.cs:7`
- **Requirement/convention:** 02 §1 DRY / 01 §3 module boundaries
- **Evidence:** `InstanceId` already has `[JsonConverter(typeof(InstanceIdJsonConverter))]` in Abstractions (`InstanceId.cs:8`). PostgreSQL redefines `EventIdJsonConverter`, `InstanceIdJsonConverter`, etc. (`StronglyTypedIdJsonConverters.cs`). Redis/SQL Server use `JsonSerializerDefaults.Web` without the full converter set (`SqlServerWorkflowStore.cs:31`, `RedisProjectionStore.cs:17`).
- **Failure scenario:** One provider round-trips `InstanceId` correctly while another silently falls back to struct expansion or fails at runtime after schema change.
- **Recommendation:** Centralize converters in Abstractions (or a small `OrcaCore.Serialization` project) and register once in shared `JsonSerializerOptions` / source context.
- **Confidence:** CONFIRMED

### [P2] No logging or BCL diagnostics despite stack decision — `v3-gpt/src/**`
- **Requirement/convention:** 02 §5 / 00 IOQ-5 observability
- **Evidence:** Zero `ILogger`, `[LoggerMessage]`, `ActivitySource`, or `Meter` usages under `src/`. `DurableOutboxPump`, `InstanceExecutionLane`, and provider commit paths emit no structured diagnostics.
- **Failure scenario:** Production hosts must reinvent observability; pump failures, lane contention, and provider retries are invisible without wrapping every port.
- **Recommendation:** Add source-generated `LoggerMessage` partials per module (`OrcaCore.Engine.Durable`, `OrcaCore.Providers.PostgreSql`, …) and `ActivitySource` spans on commit/dispatch boundaries; keep OpenTelemetry SDK out of core per IOQ-5.
- **Confidence:** CONFIRMED

### [P2] Documented Channels substrate not used — concurrency uses locks and semaphores — `InstanceExecutionLane.cs:8`
- **Requirement/convention:** 00 §1 Concurrency substrate
- **Evidence:** No `System.Threading.Channels` references in `v3-gpt/`. Per-instance serialization uses `ConcurrentDictionary` + `SemaphoreSlim` (`InstanceExecutionLane.cs:8–17`). In-memory stores and interpreter parallel paths use `lock (gate)` with `object` (`InMemoryWorkflowProvider.cs:45`, `Interpreter.cs:503`).
- **Failure scenario:** Architectural drift from documented design; future mailbox/outbox work may duplicate channel patterns ad hoc; `SemaphoreSlim` entries in `InstanceExecutionLane` are never removed (unbounded dictionary growth for long-lived hosts).
- **Recommendation:** Either update 00 to reflect the SemaphoreSlim model, or migrate mailboxes/outbox/timer queues to bounded channels as specified; add lane eviction or weak-reference policy for completed instances.
- **Confidence:** CONFIRMED

### [P2] Ephemeral timer tokens use `Guid.NewGuid()` instead of version-7 IDs — `EphemeralTimerService.cs:19`
- **Requirement/convention:** 00 §2 IDs (`Guid.CreateVersion7()`)
- **Evidence:**
  ```csharp
  var scheduledTimer = new ScheduledTimer(
      Guid.NewGuid(),
      instanceId,
      timeProvider.GetUtcNow().Add(delay),
      fireAsync);
  ```
  Elsewhere, `InstanceId.New()`, `EventId`, `TimerId`, etc. use `Guid.CreateVersion7()` (`InstanceId.cs:29`).
- **Failure scenario:** Inconsistent ID policy; ephemeral timer correlation cannot be time-ordered in logs or tests the same way as durable `TimerId` values.
- **Recommendation:** Replace with `Guid.CreateVersion7()` or use `TimerId.New()` if ephemeral timers are promoted to a typed ID.
- **Confidence:** CONFIRMED

### [P2] Monolithic types exceed maintainability budget — `DurableWorkflowAggregate.cs:1`
- **Requirement/convention:** 02 §4 layout / 01 §3 no god-classes
- **Evidence:** Line counts: `DurableWorkflowAggregate.cs` ~2233, `PostgreSqlWorkflowStore.cs` ~1454, `SqlServerWorkflowStore.cs` ~1330, `Interpreter.cs` ~1115, `DurableCommandProcessor.cs` ~870.
- **Failure scenario:** Review and test churn concentrate in single files; merge conflicts and missed edge cases rise as features land (saga, DAG, pools already share these files).
- **Recommendation:** Extract cohesive sub-handlers (replay appliers, foreach join, pool acquire, saga compensation) into `internal sealed` partial modules by feature area while keeping one aggregate entry point.
- **Confidence:** CONFIRMED

### [P2] `.editorconfig` enforces naming only — modern C# analyzer rules absent — `.editorconfig:1`
- **Requirement/convention:** 02 §1 / `AnalysisLevel=latest`
- **Evidence:** `v3-gpt/.editorconfig` sets file-scoped namespaces and interface naming; no `dotnet_diagnostic` entries for async (`CA2007`), exception quality, or JSON source-gen (`CA1869`). Build is clean with `TreatWarningsAsErrors=true`, so gaps are policy gaps, not latent warnings.
- **Failure scenario:** Regressions (missing `ConfigureAwait`, new reflection serializers, `Task.Delay` in `src/`) compile without friction.
- **Recommendation:** Add `EnableNETAnalyzers` + targeted warnings as errors for CA2007 in `src/`, ban `Task.Delay` in production projects via analyzer or arch test, and document allowed exceptions.
- **Confidence:** CONFIRMED

### [P2] Tests still use wall-clock `Task.Delay` — `ExecutionLaneTests.cs:131`
- **Requirement/convention:** 03 §4 / NF-020
- **Evidence:** `Task.Delay(timeout, cancellationToken)` in `ExecutionLaneTests`; `Task.Delay(10, …)` in `ResourceGovernanceTests.cs:237` and `OperationsAcceptanceTests.cs:192`. Production `src/` is clean (no `Task.Delay`, no `DateTime.Now`).
- **Failure scenario:** CI flakes on slow agents; contradicts documented determinism discipline while production code follows `TimeProvider`.
- **Recommendation:** Gate on `TaskCompletionSource` + `FakeTimeProvider` (already used in `YieldTests`, `TimerEventRaceTests`).
- **Confidence:** CONFIRMED

### [P3] Exhaustive `switch` arms use `InvalidOperationException` not `UnreachableException` — `DurableOutboxPump.cs:35`
- **Requirement/convention:** 02 §2 closed hierarchies
- **Evidence:** `_ => throw new InvalidOperationException($"Unknown dispatch result '{result}'.")` in `DurableOutboxPump`; similar in `WorkflowBuilder.cs:534`, `RabbitMqMessageDispatcher.cs:24`. Convention prefers `UnreachableException` when the compiler cannot prove exhaustiveness over closed enums/records.
- **Failure scenario:** None functionally; slightly weaker signal that the branch is logically impossible vs a new enum member.
- **Recommendation:** Use `throw new UnreachableException()` in default arms for closed `DispatchResult` / builder node switches.
- **Confidence:** CONFIRMED

### [P3] Primary constructors adopted unevenly — `EphemeralWorkflowEngine.cs:42`
- **Requirement/convention:** 02 §1 “use freely: primary constructors”
- **Evidence:** Newer types use primary constructors (`DurableCommandProcessor`, `DurableOutboxPump`, `RabbitMqClientPublisher`, `EphemeralTimerService`). Larger entry types keep classic constructors and many private fields (`EphemeralWorkflowEngine.cs:42`, `PostgreSqlWorkflowStore.cs:68`, `Interpreter` implicit ctor).
- **Failure scenario:** Style inconsistency only; no runtime impact.
- **Recommendation:** Adopt primary constructors when touching large types for DI-injected dependencies; leave complex initialization in body methods.
- **Confidence:** CONFIRMED

### [P3] `ExecutionPointer` hand-rolls equality — `ExecutionPointer.cs:39`
- **Requirement/convention:** 02 §1 records / immutability
- **Evidence:** 69 lines of manual `IEquatable`, `GetHashCode`, and operators; `ExecutionFrame` is already `readonly record struct` (`ExecutionPointer.cs:71`).
- **Failure scenario:** Maintenance cost if equality rules change; risk of divergence from record semantics.
- **Recommendation:** Consider `internal sealed record ExecutionPointer` with a private frame list factory, matching `ExecutionFrame` style.
- **Confidence:** PLAUSIBLE

### [P3] Custom `ReadOnlyList<T>` wrapper — `ReadOnlyList.cs:5`
- **Requirement/convention:** 02 §1 minimize bespoke types
- **Evidence:** Internal `ReadOnlyList<T>` wraps `T[]` with `IReadOnlyList<T>`; BCL `ReadOnlyCollection<T>` or returning `T[]` / `IReadOnlyList<T>` from `ToArray()` would suffice.
- **Failure scenario:** None material; extra type to maintain.
- **Recommendation:** Replace with `ReadOnlyCollection<T>` or expose arrays directly from definition builders.
- **Confidence:** CONFIRMED

### [P3] Root vs `v3-gpt` SDK pins diverge — `global.json` (repo root) vs `v3-gpt/global.json`
- **Requirement/convention:** NF-001 / developer ergonomics
- **Evidence:** Root: `"version": "10.0.200", "rollForward": "latestPatch"`. `v3-gpt/`: `"version": "10.0.301"` only. Build from root fails on machines with 10.0.301 but not 10.0.200.
- **Failure scenario:** Contributors and agents run `dotnet` from repo root and get SDK resolution errors (seen in R7).
- **Recommendation:** Single `global.json` at repo root aligned with CI and local SDK, or document that all commands must run from `v3-gpt/`.
- **Confidence:** CONFIRMED

## Strengths (what is already in good shape)

| Area | Observation |
|------|-------------|
| Toolchain | `net10.0`, `LangVersion latest`, nullable enable, `TreatWarningsAsErrors`, `AnalysisLevel latest` — clean full-solution build |
| Modern syntax | Universal file-scoped namespaces; widespread collection expressions (`[]`, `[..]`); `required` on durable events; `FrozenDictionary`/`FrozenSet` in `LifecycleMachine.cs` |
| IDs | Strongly typed `readonly record struct` IDs with `Guid.CreateVersion7()` factories |
| Async discipline (src) | No `async void`, `.Result`, `.Wait()`, or `Task.Run` in library code; `ConfigureAwait(false)` on provider/engine awaits; `CancellationToken` on public async APIs |
| Time | `TimeProvider` injected; no `DateTime.Now`/`UtcNow` or `Task.Delay` in `src/` |
| Closed hierarchies | `StepResult`, `WorkflowEvent`, `WorkflowCommand` modeled as `abstract record` + sealed variants |
| Tests | xUnit v3 + AwesomeAssertions per stack decision; `FakeTimeProvider` in TestSupport |
| DI / hosting | `TimeProvider.System` registered; primary constructors on several new services |
| Banlist | No Newtonsoft, EF, Dapper in core; no `unsafe`/`dynamic`; no `#pragma`/`#region` suppression culture |

## Coverage note

**Reviewed:** All 15 `src` projects (grep + targeted reads of largest files and serialization/async paths), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, both `global.json` files, benchmark entrypoint, and test async patterns.

**02 §1 language features — spot check:**

| Feature | Verdict |
|---------|---------|
| File-scoped namespaces | Pass |
| Records / `required` / `init` | Pass on contracts |
| Primary constructors | Partial — newer code only |
| Collection expressions | Pass |
| Pattern matching / switches | Pass; `UnreachableException` not adopted |
| `field` keyword | Not used |
| `params` collections | Not observed |
| Source-generated JSON | **Fail** |
| Source-generated logging | **Fail** |
| `System.Threading.Channels` | **Not used** (vs 00 decision) |

**03 §4 determinism:** Production pass; tests partial (`Task.Delay` in 5 locations).

**Not reached:** Full line-by-line XML-doc audit on every public member; AOT/trim publish profile analysis; BenchmarkDotNet scenario methodology review (skeleton only).

**Relation to R0–R7:** Functional defects remain in phase findings (R3–R7). This review is additive — code style and .NET 10 feature adoption — and does not replace spec-conformance audits.
