# R5 — Providers & Certification — Findings

> Phase scope: `v3-gpt/src/OrcaCore.Providers.*` (InMemory, PostgreSql, SqlServer,
> RabbitMq, Redis, ZeroMq) and `v3-gpt/tests/OrcaCore.ProviderCertification` plus
> per-provider test projects. Reviewed against PR-020…024, PR-030, PR-050, DU-030…033,
> and `docs/implementation/00-stack-decisions.md` plugin banlist. Primary lenses:
> correctness/concurrency, security, pluggability. Code review only.

## Findings

### [P0] `SqlServerWorkflowStore` is an in-memory stub — no durable SQL persistence — `SqlServerWorkflowStore.cs:23`
- **Requirement/convention:** PR-010 / PR-020 / PR-024 / DU-011
- **Evidence:**
  ```csharp
  private readonly Dictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
  // ...
  public async Task InitializeAsync(CancellationToken cancellationToken)
  {
      await using var connection = new SqlConnection(connectionString);
      await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
  }
  ```
  `AppendAsync`, `LoadTailAsync`, inbox/outbox/checkpoint paths mutate only in-process dictionaries under `lock (gate)`; no `INSERT`/`SELECT` against SQL Server.
- **Failure scenario:** Operator registers `SqlServerWorkflowStore` with a real connection string (as `SqlServerProviderCertificationTests` does via Testcontainers). Certification passes, but process restart loses every event, checkpoint, inbox record, and outbox row — indistinguishable from ephemeral storage while advertising a relational durable backend.
- **Recommendation:** Either implement the T6-08/T6-09 SQL schema and parameterized queries, or rename/split the type (e.g. `SqlServerWorkflowStoreStub`) and exclude it from provider certification until real persistence lands; do not run certification against Testcontainers until SQL I/O exists.
- **Confidence:** CONFIRMED (traced all mutating methods; `InitializeAsync` only opens a connection)

### [P1] `RedisProjectionStore` never reads or writes Redis — `RedisProjectionStore.cs:44`
- **Requirement/convention:** PR-013 / PR-002
- **Evidence:**
  ```csharp
  private readonly IDatabase? redisDatabase;
  // ...
  lock (gate)
  {
      foreach (var operation in operations)
      {
          if (operation.InstanceSnapshot is { } snapshot)
              summaries[operation.InstanceId] = CloneSnapshot(snapshot);
      }
  }
  ```
  `redisDatabase` is stored when the adapter ctor is used (`UsesRedisAdapter == true`) but `ApplyAsync`, `ListAsync`, `CountAsync`, and `GetStatisticsAsync` only touch the in-process `summaries` dictionary.
- **Failure scenario:** Host configures Redis for cross-node projection cache; restart or second node sees empty management queries despite commits on the primary — routing (EV-011) and management (DU-070) break under multi-node deployment.
- **Recommendation:** Persist snapshot metadata (and active-wait index fields) to Redis keys on `ApplyAsync`; read from Redis in `ListAsync`/`CountAsync`, keeping the in-memory ctor for unit tests only.
- **Confidence:** CONFIRMED

### [P1] Instance purge does not remove durable timer rows — `PostgreSqlWorkflowStore.cs:1266`
- **Requirement/convention:** PR-022 / AC-314
- **Evidence:** `DeleteInstanceDataAsync` deletes active-wait projections, instance projections, checkpoints, outbox, and events — but not `orcacore_timers`. Same gap in `InMemoryWorkflowProvider.DeleteInstanceData` (`InMemoryWorkflowProvider.cs:517`) — `timers` dictionary is untouched.
- **Failure scenario:** Terminal instance is purged after retention policy. A previously scheduled timer remains in the timer table (PostgreSQL) or in-memory timer map; `ClaimDueAsync` later emits a `FireTimerCommand` for a purged instance, causing ghost wake-ups or processor errors.
- **Recommendation:** Delete timers by `instance_id` in the purge transaction alongside other instance-scoped tables; add certification test that purge + `ClaimDueAsync` returns empty.
- **Confidence:** CONFIRMED

### [P1] RabbitMQ publisher reports success without awaiting publisher confirms — `RabbitMqClientPublisher.cs:33`
- **Requirement/convention:** PR-015 / DU-032
- **Evidence:**
  ```csharp
  await channel.BasicPublishAsync(..., cancellationToken).ConfigureAwait(false);
  return RabbitMqPublishOutcome.Confirmed;
  ```
  Channel is created with `publisherConfirmationsEnabled: true`, but no `WaitForConfirmsAsync` (or equivalent) runs before returning `Confirmed`.
- **Failure scenario:** Broker accepts the frame then fails before confirm (channel close, resource alarm). Outbox pump marks the record dispatched while the message never reached the exchange — silent message loss despite “confirmed” dispatch semantics.
- **Recommendation:** Await publisher confirms (or use the async confirm API) and map nack/timeout to `RetryableFailure`; extend `RabbitMqDispatcherIntegrationTests` to assert confirm-wait behavior.
- **Confidence:** CONFIRMED (code path; integration test only checks queue delivery, not confirm handling)

### [P1] PostgreSQL unique-violation conflict reports incorrect actual version — `PostgreSqlWorkflowStore.cs:299`
- **Requirement/convention:** PR-010 / PR-021
- **Evidence:**
  ```csharp
  catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
  {
      return EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, batch.ExpectedVersion);
  }
  ```
  Both expected and actual are set to `batch.ExpectedVersion`.
- **Failure scenario:** Concurrent appends race on version insert; loser receives a conflict result claiming actual version equals expected, so retry logic cannot compute the correct next version and may spin or append to the wrong version.
- **Recommendation:** Re-read `max(version)` inside the catch (or use `ON CONFLICT` returning) and return the true `actualVersion` in `EventStoreConflict`.
- **Confidence:** CONFIRMED

### [P1] Provider certification suite does not cover all shipped adapters — `SqlServerProviderCertificationTests.cs:9`
- **Requirement/convention:** PR-024
- **Evidence:** Shared suite coverage by adapter:
  - **InMemory:** `ContinueAsNewCertificationTests`, `RetentionCertificationTests`, `TimerSchedulerCertificationTests`, `ResourcePoolStoreCertificationTests` (via dedicated test classes).
  - **PostgreSQL:** full event-store + continue-as-new + retention + timer + resource-pool suites in `OrcaCore.Providers.PostgreSql.Tests`.
  - **SqlServer:** only `EventStoreCertificationTests` (`SqlServerProviderCertificationTests.cs:9`); no `ContinueAsNewCertificationTests`, `RetentionCertificationTests`, or resource-pool certification despite `SqlServerWorkflowStore` implementing those ports.
  - **RabbitMQ / ZeroMQ:** stub-based unit tests and RabbitMQ integration for happy-path publish only — no reusable dispatcher certification for outcome mapping under failure modes.
- **Failure scenario:** Regression in SQL Server retention or continue-as-new semantics ships undetected; dispatcher adapters diverge on retryable vs permanent classification without a shared contract test.
- **Recommendation:** Make every shipped durable adapter inherit the full applicable certification base classes; add `MessageDispatcherCertificationTests` (success / retryable / permanent) parallel to event-store certification.
- **Confidence:** CONFIRMED (inventory of test class inheritance)

### [P1] Durable timer scheduling is outside the provider commit boundary — `PostgreSqlWorkflowStore.cs:603`
- **Requirement/convention:** PR-020 / PR-014 / EV-050
- **Evidence:** `AppendAsync` atomically commits events, checkpoint, inbox, outbox, and projection operations in one transaction (`PostgreSqlWorkflowStore.cs:257-296`). `ScheduleAsync` performs a standalone `INSERT` into `orcacore_timers` (`603-638`) with no linkage to `ProviderCommitBatch`. `ITimerScheduler.ScheduleAsync` is not invoked from `OrcaCore.Engine.Durable` anywhere in the codebase (only tests/benchmarks call it).
- **Failure scenario:** Once engine wiring lands, crash between successful event append (`WorkflowTimerScheduledEvent`) and `ScheduleAsync` loses the wake-up; conversely, schedule-without-commit leaves orphan timers. Either ordering violates PR-020’s single-mutation boundary.
- **Recommendation:** Include timer writes in `ProviderCommitBatch` (or a provider callback inside the append transaction); schedule only after commit via outbox-style two-phase pattern; add certification for commit+timer atomicity.
- **Confidence:** CONFIRMED (provider split; engine wiring absent)

### [P2] PostgreSQL projection list performs N+1 active-wait queries — `PostgreSqlWorkflowStore.cs:516`
- **Requirement/convention:** NF-030 / PR-023
- **Evidence:** Inside the `ListAsync` reader loop: `ActiveWaits = await LoadActiveWaitsAsync(instanceId, cancellationToken)` opens a new connection and query per returned instance.
- **Failure scenario:** Management query for 1,000 instances issues 1,001 round-trips; cold-start dashboards time out under production instance counts.
- **Recommendation:** Batch-load waits for all instance IDs in one query (or join in the main SQL) and group in memory.
- **Confidence:** CONFIRMED

### [P2] PostgreSQL `CountAsync` materializes full snapshot list — `PostgreSqlWorkflowStore.cs:525`
- **Requirement/convention:** NF-030 / DU-070
- **Evidence:** `return (await ListAsync(query, cancellationToken).ConfigureAwait(false)).Count;`
- **Failure scenario:** `CountAsync` for a broad filter deserializes every matching row and loads all active waits — O(n) memory and CPU for a scalar count.
- **Recommendation:** Implement `SELECT COUNT(*)` with the same filter predicates as `ListAsync`.
- **Confidence:** CONFIRMED

### [P2] Projection history operations are silently dropped — `PostgreSqlWorkflowStore.cs:975`
- **Requirement/convention:** PR-013 / DU-071
- **Evidence:** `case ProjectionOperationKind.AppendHistory: break;` — no persistence for history append operations in PostgreSQL or InMemory providers.
- **Failure scenario:** Engine emits history projection writes for operator timeline (DU-071); provider discards them — history inspection APIs cannot be backed by the store.
- **Recommendation:** Add `orcacore_history_projections` (or JSON append column) and implement `AppendHistory`; certify round-trip.
- **Confidence:** CONFIRMED

### [P2] `SqlServerWorkflowStore` rejects projection operations in commit batch — `SqlServerWorkflowStore.cs:80`
- **Requirement/convention:** PR-020 / IOQ-3 (`ProjectionCommitMode.SameCommitBoundary`)
- **Evidence:**
  ```csharp
  if (batch.ProjectionOperations.Count > 0)
      return Task.FromResult(Result<AppendEventsResult>.Failure(
          new OrcaCoreException("SQL Server projections are outside the T6-08 event-store slice.")));
  ```
- **Failure scenario:** Any engine path that includes projection writes in the same batch (the norm per `ProviderCommitPolicy.ProjectionMode`) cannot use the SQL Server store without splitting commits — reintroducing crash windows between event and projection state.
- **Recommendation:** Complete T6-09 projections slice so SQL Server can apply projection ops inside the same transaction as events, or document and enforce a single transactional facade across slices.
- **Confidence:** CONFIRMED

### [P2] Timer scheduler certification lacks AC tags and concurrency coverage — `TimerSchedulerCertificationTests.cs:14`
- **Requirement/convention:** PR-024 / EV-050 / NF-020
- **Evidence:** Tests `ScheduleAsync_DueTimer_IsClaimableOnce` and `ScheduleAsync_NotDueTimer_IsNotClaimed` have no `[Trait("AC",…)]`. No concurrent `ClaimDueAsync` test (contrast PostgreSQL outbox test `OutboxClaim_ConcurrentWorkers_DoNotClaimSameRecord` in `PostgreSqlProviderCertificationTests.cs:124`).
- **Failure scenario:** PostgreSQL timer claim regression (double fire under two hosts) ships without detection; AC traceability matrix shows gap for durable timer invariants.
- **Recommendation:** Tag with the relevant EV/AC IDs; add `Task.WhenAll` concurrent claim test for PostgreSQL (and any future SQL timer backend).
- **Confidence:** CONFIRMED

### [P2] Resource-pool certification has no concurrent acquire stress test — `ResourcePoolStoreCertificationTests.cs:14`
- **Requirement/convention:** PR-024 / AC-518 / AC-519
- **Evidence:** AC-518/519 tests are sequential `await` calls. PostgreSQL store uses `IsolationLevel.Serializable` (`PostgreSqlResourcePoolStore.cs:136`) but no test exercises concurrent `AcquireAsync` on the last available slot.
- **Failure scenario:** Serializable deadlock handling or lost wake-up under concurrent acquire is untested; capacity could be exceeded in a provider bug.
- **Recommendation:** Add certification test: pool capacity 1, two parallel acquires — exactly one granted, one queued; verify held count never exceeds capacity.
- **Confidence:** PLAUSIBLE (PostgreSQL locking looks correct; test gap confirmed)

### [P2] `InMemoryProviderCertificationTests` assignability checks add no invariant coverage — `InMemoryProviderCertificationTests.cs:10`
- **Requirement/convention:** PR-024 / 03 §2 (TDD discipline)
- **Evidence:** Four tests assert `BeAssignableTo<IWorkflowEventStore>()` etc., while real invariant tests live in inherited `ContinueAsNewCertificationTests` → `EventStoreCertificationTests`.
- **Failure scenario:** None functional — noise in CI obscures which tests are load-bearing certification vs structural boilerplate.
- **Recommendation:** Remove redundant assignability tests; rely on inherited certification fixtures only.
- **Confidence:** CONFIRMED

### [P3] PostgreSQL claimed timers are never deleted — `PostgreSqlWorkflowStore.cs:655`
- **Requirement/convention:** none — general maintainability
- **Evidence:** `ClaimDueAsync` sets `claimed = true` but never deletes rows; only re-scheduling the same `timer_id` resets `claimed = false` via upsert.
- **Failure scenario:** Long-running deployments accumulate dead timer rows; index bloat on `ix_orcacore_timers_due` slows claims.
- **Recommendation:** Delete on claim or add periodic cleanup for claimed timers tied to fired/terminal instances.
- **Confidence:** CONFIRMED

### [P3] ZeroMQ has no broker integration test — `ZeroMqMessageDispatcherTests.cs:11`
- **Requirement/convention:** PR-024
- **Evidence:** Tests use `RecordingPublisher` stub only; `NetMqPublisher` is never exercised in CI. RabbitMQ counterpart has `RabbitMqDispatcherIntegrationTests` with Testcontainers.
- **Failure scenario:** `NetMqPublisher` socket/connect regressions ship unnoticed.
- **Recommendation:** Add optional in-process push/pull integration test (no external container required).
- **Confidence:** CONFIRMED

## Coverage note

**Reviewed source:** all files under `OrcaCore.Providers.InMemory`, `.PostgreSql`, `.SqlServer`, `.RabbitMq`, `.Redis`, `.ZeroMq`; certification bases in `OrcaCore.ProviderCertification`; concrete certification tests in `OrcaCore.Providers.PostgreSql.Tests`, `.SqlServer.Tests`, `.RabbitMq.Tests`, `.Redis.Tests`, `.ZeroMq.Tests`.

**Requirements / ACs verified (with tests examined):**
| ID | Verdict |
|----|---------|
| PR-001…003 | Pass — ports are interface-first; engines depend on abstractions only |
| PR-010…016 | Partial — ports exist; timer/history/redis/sql-server implementations incomplete |
| PR-020 | Partial — PostgreSQL/InMemory atomic append+inbox+outbox+projection in one TX; timers and standalone `ApplyAsync` break boundary |
| PR-021 | Partial — `AC-309` concurrent append tested (InMemory, PostgreSQL, SqlServer stub); PostgreSQL unique-violation path defective |
| PR-022 | Partial — `AC-314` retention tests (InMemory, PostgreSQL); purge timer leak |
| PR-023 | Partial — PostgreSQL metadata queries work; N+1 and count materialization hurt queryability at scale |
| PR-024 | **Gap** — certification not uniform across adapters; dispatcher/timer gaps |
| PR-030 | Pass — InMemory reference implements all durable ports for tests |
| PR-050 | Pass — providers return `Result`/`Option` at boundaries |
| AC-305 | Verified — inbox dedup (base + PostgreSQL restart test) |
| AC-309 | Verified — concurrent append one winner |
| AC-310 | Verified — outbox not visible on failed commit |
| AC-313 | Verified — InMemory + PostgreSQL continue-as-new |
| AC-314 | Verified — InMemory + PostgreSQL retention; timer cleanup on purge not tested |
| AC-518…522 | Verified — InMemory + PostgreSQL resource pool (sequential only) |
| Banlist (00 §3) | Pass — sampled `.csproj` files use only whitelisted packages (`Npgsql`, `Microsoft.Data.SqlClient`, `RabbitMQ.Client`, `StackExchange.Redis`, `NetMQ`) |

**Not reached / out of phase boundary:**
- `OrcaCore.Hosting` timer pump (`OrcaCoreTimerHostedService` is a no-op stub) — engine/provider wiring for `ITimerScheduler` belongs to R7; noted because it affects when timer port certification matters in production.
- DynamoDB provider (deferred per IOQ-10).
- Full SQL Server T6-08/T6-09 implementation (only stub reviewed here).

**Stopped at:** natural phase boundary (all `OrcaCore.Providers.*` projects and certification suite). Composition/saga/hosting cross-cuts deferred to R6/R7.
