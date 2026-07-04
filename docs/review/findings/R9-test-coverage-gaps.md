# R9 — Test Coverage & Quality Gaps

> Scope: `v3-gpt/tests/**` meta-review against `docs/specs/12-acceptance-criteria.md`,
> `docs/specs/14-driving-scenario-eks-job-scheduler.md` (JS-AC), `docs/specs/15-requirements-observability-otel.md` (OB),
> `docs/implementation/03-tdd-workflow.md`, and cross-references from R3–R8 code-review findings.
> This document records **what tests exist, what they actually prove, and what is missing** —
> not code defects (those live in R3–R8).

**Baseline (2026-07-03):**

| Project | Approx. tests | Notes |
|---------|---------------|-------|
| `OrcaCore.Core.Tests` | ~55 | Contracts, builders, primitives; includes `RepositoryGuardTests` |
| `OrcaCore.Engine.Ephemeral.Tests` | ~100 | Interpreter, waits, parallel, timers, governance |
| `OrcaCore.Engine.Durable.Tests` | ~90+ | Aggregates, outbox, composition, saga, pools, `R4DurableEngineFindingsTests` |
| `OrcaCore.Acceptance.Tests` | ~70+ | End-to-end AC coverage across engines |
| `OrcaCore.ProviderCertification` | shared suite | InMemory reference; reused by provider projects |
| `OrcaCore.Providers.*.Tests` | varies | PostgreSQL (Testcontainers), SqlServer, RabbitMQ, Redis, ZeroMQ |
| `OrcaCore.Hosting.Tests` | 4 | DI registration + sample-host smoke only |

**Trait coverage:** `RepositoryGuardTests.AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver`
enforces that every `AC-xxx` / `JS-AC-xxx` in docs 12 and 14 has ≥1 `[Trait("AC",…)]` test or an
explicit waiver (`AC-315`, `JS-AC-008`). **Trait presence ≠ behavioral proof.**

**Local run:** `OrcaCore.Engine.Ephemeral.Tests` reported **10 failures** (parallel/when-first branch
disambiguation — tests ahead of or behind implementation). `OrcaCore.Engine.Durable.Tests` did not
build (aggregate constructor drift). Full-solution run aborted when PostgreSQL test host locked DLLs.

---

## 1. Executive summary

The suite is **broad and well-structured** for a workflow engine at this maturity:

- Acceptance tests cover most catalog ACs with `[Trait("AC",…)]` tags.
- Provider certification reuses a shared harness (event store, inbox, outbox, timers, pools, retention).
- `R4DurableEngineFindingsTests` is a good pattern: regression tests tied to review findings.

Gaps cluster into five themes:

1. **Shallow AC coverage** — tagged tests that assert metadata or stream events, not end-state behavior.
2. **Known-bug blind spots** — tests that codify incorrect behavior or miss branches R3/R4/R6 flagged.
3. **Missing integration surfaces** — hosting hosted services, multi-node, OTel observability.
4. **Provider false confidence** — certification passes against in-memory stubs (SqlServer store, Redis projection).
5. **TDD discipline drift** — `Task.Delay` in tests despite `RepositoryGuardTests`; flaky concurrent tests.

---

## 2. AC coverage matrix (behavioral depth)

Legend: **Tagged** = has `[Trait("AC",…)]`; **Behavioral** = test exercises public API to terminal/snapshot outcome;
**Shallow** = asserts events/metadata only; **Missing** = no tagged test (waiver or gap).

### 2.1 Core & events (AC-0xx, AC-1xx)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| AC-001…012 | Yes | Behavioral | Strong in acceptance + unit |
| AC-013 Yield | Yes | Behavioral | Durable crash-survival not in same test as ephemeral |
| AC-014 Graceful cancel | Yes | **Shallow** | Only waiting instance; **no in-flight step + `CancellationToken`** (R3 P1) |
| AC-015 Terminate | Yes | Behavioral | — |
| AC-101…109 | Yes | Behavioral | — |
| AC-110 Parallel waits | Yes | **In flux** | New branch-aware tests in `ParallelTests` failing; durable side still correlation-only (R3/R4) |
| AC-111…113 | Yes | Behavioral | — |
| AC-114 Crash between match/commit | Yes | Provider + inbox | Ephemeral path not mirrored |
| AC-115 Bulk retrieval | Yes | Unit | No load/scale assertion |

### 2.2 Composition (AC-2xx, AC-6xx)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| AC-201…205 | Yes | Behavioral | WhenFirst concurrent winner tests failing locally |
| AC-601…605 ForEach | Yes | Behavioral | — |
| AC-606…616 RunChildren | Yes | **Mixed** | See §3 |

### 2.3 Durable (AC-3xx)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| AC-301…302 | Yes | Provider integration | — |
| AC-303…304 WaitLong | Yes | Unit/aggregate | No full host restart + cold eviction E2E |
| AC-305…311 | Yes | Mixed | AC-311 `StartOrGet` restart: `R4DurableEngineFindingsTests` added; versioning tests still in-process only |
| AC-312 History pressure | Yes | Shallow | Counts only, no threshold/lag |
| AC-313 Continue-as-new | Yes | Behavioral | — |
| AC-314 Retention | Yes | Behavioral | **Timer rows not asserted after purge** (R5 P1) |
| AC-315 Multi-node mutator | **Waived** | Missing | Needs two-host integration harness |
| AC-316 Shutdown hooks | Yes | Unit | — |

### 2.4 Saga (AC-4xx)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| AC-401…405 | Yes | Ephemeral + aggregate | — |
| AC-406 Durable saga resume | Yes | **Shallow** | Aggregate/hand-built events; **no public durable saga interpreter E2E** (R6 P1) |
| AC-407…409 | Yes | Mixed | AC-408 manual recovery: aggregate only |
| Multi-step compensation | Partial | **Missing** | `CompensationFailureTests` single action; R4: terminal after first completion |

### 2.5 Management (AC-5xx)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| AC-501…503 | Yes | Behavioral | — |
| AC-504…506 Eviction | Yes | Unit | No concurrent eviction stress |
| AC-507…508 Stuck | Yes | Behavioral | — |
| AC-509 Lifecycle publication | Yes | Shallow | Does not assert outbox durability guarantees |
| AC-510 Retry | Yes | Behavioral | — |
| AC-511 Concurrency limits | Yes | Ephemeral | Durable pool limits separate (certification) |
| AC-512…517 Pause/resume | Yes | Mixed | **Paused timer replay on resume not tested** (R4 P1) |
| AC-518…522 Pools | Yes | Certification | **No concurrent acquire on last slot** (R5 P1) |

### 2.6 Job scheduler (JS-AC)

| AC | Trait | Depth | Gap |
|----|-------|-------|-----|
| JS-AC-001 Diamond DAG | Yes | **Shallow** | Manual wave dispatch; **per-node definition IDs not asserted** (R6 P1) |
| JS-AC-002 Cycle | Yes | Build validation | — |
| JS-AC-003 Failure policy | Yes | Shallow | — |
| JS-AC-004…007, 009…013 | Yes | Mixed | — |
| JS-AC-005 Restart mid-job | Yes | Observability | Reconstruct DAG only; not full restart + job completion |
| JS-AC-008 Scheduled idempotent | **Waived** | Missing | Host/cron concern — document waiver reason in spec |

### 2.7 Observability (OB — doc 15)

| Requirement | Tests | Gap |
|-------------|-------|-----|
| OB-001…020 (OTel logs/metrics/traces) | **None** | Entire surface untested; no `ActivitySource`/`Meter`/`ILogger` in `src/` yet |
| Product observability (MG-030/031) | Partial | `DagObservabilityAcceptanceTests`, statistics tests; durable vs ephemeral type split untested |

---

## 3. Findings — tests that do not prove what they claim

### [P1] AC-609 tagged but behavior not exercised — `DurableChildThrottlingTests.cs:14`
- **Evidence:** Asserts `InitialDispatchCount`, `NextDispatchIndex`, and initial `child-start` outbox rows after duplicate `RunChildren`. Never completes a child and never asserts a third/fourth dispatch.
- **Recommendation:** Extend: 4 items, `maxConcurrency=2`, complete child 1 → expect exactly one new `child-start` and index advance (R6 P0 throttle continuation).

### [P1] AC-610/611 barrier tests are sequential — `ParentResumeTokenTests.cs`, `ChildWorkflowAcceptanceTests.cs`
- **Evidence:** Tests count `WorkflowParentResumeTokenRecordedEvent` in stream; no `ConsumeParentResumeToken` idempotency; no concurrent child completions.
- **Recommendation:** Add concurrent completion test with `RaceCoordinator`; assert parent continuation exactly once; second consume is `NoOp`.

### [P1] AC-406 durable saga — aggregate-only — `SagaAuditTests.cs`, `DurableSagaCommandAdapterTests.cs`
- **Evidence:** Hand-built event lists or adapter unit tests; `SagaAcceptanceTests` / `EphemeralSagaTests` cover ephemeral path only.
- **Recommendation:** `DurableSagaAcceptanceTests`: start saga via public API, run forward steps, crash/rehydrate, assert no duplicate forward/compensation effects.

### [P1] AC-014 cancel in-flight step — not tested — `TerminalCommandTests.cs`, `TerminalAcceptanceTests.cs`
- **Evidence:** Cancel tests use waiting instances; R3 confirms no instance-level `CancellationTokenSource`.
- **Recommendation:** `CancelAsync_CancelsInFlightStep`: step blocks on `TaskCompletionSource`; cancel; assert step observes token and instance reaches `Cancelled`.

### [P1] Durable early-event mailbox — tests in tension — `DurableInboxTests.cs` vs `R4DurableEngineFindingsTests.cs`
- **Evidence:** `DurableInboxTests` includes poison-on-no-wait path; R4 finding says AC-104 requires buffering. `R4_EarlyInboundEvent_IsBufferedAndMatchedWhenWaitRegisters` tests the fix.
- **Recommendation:** Remove or invert poison test once fix lands; add AC-104 acceptance test on durable engine matching ephemeral `MailboxAcceptanceTests`.

### [P2] SqlServer certification against in-memory stub — `SqlServerProviderCertificationTests.cs`
- **Evidence:** R5 P0: `SqlServerWorkflowStore` mutates in-process dictionaries; certification passes against Testcontainers connection only for `InitializeAsync`.
- **Recommendation:** Gate SqlServer certification behind real SQL I/O or rename stub; add restart-survival test that fails today.

### [P2] Redis projection tests never exercise Redis — `RedisProjectionProviderTests.cs`
- **Evidence:** R5 P1: `redisDatabase` unused in `ApplyAsync`/`ListAsync`.
- **Recommendation:** Integration test: write via adapter ctor, new store instance reads same keys from Redis.

### [P2] Hosting tests stop at DI — `OrcaCoreHostingServiceCollectionTests.cs`
- **Evidence:** Three resolve-from-`ServiceCollection` tests; `OrcaCoreOutboxPumpHostedService` is no-op (R7 P0).
- **Recommendation:** `HostingIntegrationTests`: start `WebApplication`/`Host` with in-memory store + fake dispatcher; advance fake clock; assert `PumpOnceAsync` invocations and timer claims.

### [P2] Wall-clock delays violate TDD guard — `ExecutionLaneTests.cs:131`, `ResourceGovernanceTests.cs:237`, `OperationsAcceptanceTests.cs:192`
- **Evidence:** `RepositoryGuardTests.TestSources_DoNotUseWallClockTaskDelay` bans these; guard likely fails in CI.
- **Recommendation:** Replace with `FakeTimeProvider` + `TaskCompletionSource` (pattern in `YieldTests`, `TimerEventRaceTests`).

### [P2] No code-coverage gate — CI collects but does not enforce
- **Evidence:** `.github/workflows/ci.yml` uploads coverlet artifacts; no threshold or report summary.
- **Recommendation:** Add `reportgenerator` step; track line/branch coverage per project; fail PR below baseline on core engines.

### [P3] Benchmarks have no regression tests — `benchmarks/OrcaCore.Benchmarks`
- **Evidence:** Skeleton only (R7).
- **Recommendation:** Optional: `[Trait("Category","Benchmark")]` smoke that benchmark project builds and one benchmark runs under CI nightly.

---

## 4. Untagged but important unit coverage (add traits)

These tests prove AC-adjacent behavior but lack `[Trait("AC",…)]`, breaking traceability:

| File | Behavior | Suggested trait |
|------|----------|-----------------|
| `MailboxTests.cs` | Buffering, dedup internals | AC-104, AC-105 |
| `WaitMatchingTests.cs` | Wait match rules | AC-102, AC-103 |
| `LoopWaitTests.cs` | Wait-in-loop isolation | AC-109 |
| `InterpreterTests.cs` | Step execution | AC-001, AC-004 |
| `ExecutionLaneTests.cs` | Serialized execution | AC-006, AC-007 |
| `RoutingTests.cs` | Correlation routing | AC-106, AC-107 |
| `EphemeralTimerTests.cs` | Timer scheduling | AC-111 |
| `RetryPolicyTests.cs` | Retry bounds | AC-510 |
| `StuckDetectionTests.cs` | Stuck signals | AC-507, AC-508 |
| `DurableAggregateTests.cs` | Aggregate invariants | DU-020 (structural) |
| `OrcaCore.Hosting.Tests/*` | — | Add when hosting behavior exists |

---

## 5. Suggested new tests (prioritized)

### P0 — Correctness gaps tied to known defects

1. **`ParallelTests` / durable parallel waits (AC-110)**
   - Two parallel waits, same `(EventName, CorrelationId)`, deliver with distinct `BranchId` → both branches resume, join completes.
   - Durable mirror in `DurableWaitTests` or new `DurableParallelWaitTests`.

2. **`DurableChildThrottlingTests` extension (AC-609, CP-023)**
   - 4 children, `maxConcurrency=2`: complete one → exactly one new dispatch; restart mid-run → no duplicate children, throttle holds.

3. **`CompensationFailureTests` multi-action (SG-010, R4 P1)**
   - Two `SagaCompensationStarted` → complete both → single `Compensated` terminal; failing second → `CompensationFailed`.

4. **`DurableInboxTests` AC-104 alignment**
   - Early event before wait registers → buffered → matched on wait registration (not poisoned).

5. **`StartOrGet` restart (AC-311)** — extend `DurableVersioningTests` or keep in `R4DurableEngineFindingsTests` with PostgreSQL provider, not only InMemory.

### P1 — Integration & provider truth

6. **`PostgreSqlRetentionTests.Purge_RemovesTimerRows` (AC-314)**
   - Schedule timer, purge instance, `ClaimDueAsync` returns empty.

7. **`ResourcePoolStoreCertificationTests.ConcurrentAcquire_LastSlot` (AC-518, JS-AC-007)**
   - N+1 parallel `AcquireAsync` on capacity N; exactly N grants; rest wait or fail per policy.

8. **`RabbitMqDispatcherIntegrationTests.Publish_AwaitsPublisherConfirm` (DU-032)**
   - Assert confirm-wait before `Confirmed` outcome.

9. **`SqlServerWorkflowStore_RestartSurvivesEvents`**
   - New process, same connection string, events visible — **expect fail until stub replaced**.

10. **`RedisProjectionStore_CrossProcessRead`**
    - Write snapshot, new provider instance reads from Redis.

### P1 — Hosting & operations

11. **`HostingOutboxPumpIntegrationTests`**
    - Register hosted services, commit outbox record, run one pump cycle, fake dispatcher receives message.

12. **`HostingTimerIntegrationTests`**
    - Due timer claimed by hosted service → `FireTimerCommand` processed.

13. **`DurableManagementTests.PausedTimer_ReplayOnResume` (AC-513, AC-514)**
    - Pause → timer fires → resume with Replay → timeout/event race outcome deterministic.

14. **`TerminalAcceptanceTests.CancelAsync_InFlightStep` (AC-014)**
    - Cooperative cancellation of running step.

### P2 — Concurrency, multi-node, observability

15. **`EvictionConcurrencyTests` (AC-506)**
    - Concurrent eviction + resume on same instance; at most one mutator.

16. **`MultiNodeMutatorTests` (AC-315)** — two in-process hosts sharing PostgreSQL store; racing commands on one instance.

17. **`ObservabilityTests` (OB-xxx)** — once OTel wired: span on `ProcessAsync`, metric for outbox backlog, log correlation fields present.

18. **`DagAcceptanceTests.HeterogeneousDefinitions` (JS-AC-001)**
    - Diamond DAG with definitions 1–4 on nodes A–D; assert each child-start carries correct `DefinitionId`.

19. **`DagRunnerAcceptanceTests`**
    - Engine-integrated DAG to completion (not manual per-wave commands) — may be blocked on JS-001 runner API.

20. **`YieldAfterTimerTests` (CR-017, R3 P2)**
    - Timer resume → step yields → continuation drains without unrelated command.

### P2 — Test infrastructure

21. **Arch test: engines do not reference hosting** (extend `RepositoryGuardTests`).

22. **Arch test: production `src/` has no `Task.Delay`** (R8 — mirror test guard).

23. **AC depth linter** (optional): custom analyzer or script flagging tests where `[Trait("AC",…)]` exists but no `WorkflowStatus`/`Should()` on outcome.

---

## 6. Test quality improvements

| Issue | Current | Target |
|-------|---------|--------|
| Time | `Task.Delay(10)` in 3 files | `FakeTimeProvider.Advance` |
| Concurrency | Sequential child/barrier tests | `RaceCoordinator` + parallel `Task.WhenAll` |
| Provider isolation | PostgreSQL tests can lock DLLs on Windows | `--no-build` per project in parallel CI; `dotnet test --filter` splits |
| False greens | SqlServer/Redis certification | Skip or `[Trait("Category","Stub")]` until real I/O |
| Findings regression | `R4DurableEngineFindingsTests` only | Add `R3EphemeralFindingsTests`, `R6CompositionFindingsTests` as fixes land |

---

## 7. Coverage note

**Verified present:** Trait catalog guard (`RepositoryGuardTests`); ~90% of doc-12/14 AC IDs tagged;
acceptance suite for core flows; provider certification harness; ephemeral ForEach/Parallel/Wait;
durable outbox/inbox/versioning/pause; saga ephemeral + aggregate; external job composite;
resource pool certification (sequential).

**Verified shallow or missing behavior:** AC-609 throttle continuation, AC-610/611 concurrent barrier,
AC-406 durable saga E2E, AC-014 in-flight cancel, AC-315 multi-node, OB observability, hosting pump/timer,
purge timer cleanup, concurrent pool acquire, SqlServer/Redis real persistence.

**Not executed successfully this session:** Full `OrcaCore.slnx` (DLL lock + durable build errors);
PostgreSQL Testcontainers on local Windows.

**Cross-references:** Implementation gaps in R3 (ephemeral waits/cancel/timers), R4 (durable mailbox/pools/saga),
R5 (provider stubs), R6 (composition/DAG/saga interpreter), R7 (hosting/CI/meta), R8 (TDD delays).

---

## 8. Negative tests & edge-case catalog

Detailed scenario IDs (`NEG-*`, `EDGE-*`) by domain live in
[`docs/review/test-scenarios/`](../test-scenarios/README.md) — one file per category (9 files).

## 9. Integration testing catalog

Cross-boundary scenarios (`INT-*`), fixture plan, CI strategy, and phased rollout live in
[`docs/review/integration-tests/`](../integration-tests/README.md) — 7 category files + harness guide.

## 10. Recommended next steps

1. Fix ephemeral parallel branch tests and durable build so the suite is green.
2. Make `RepositoryGuardTests` pass (remove `Task.Delay` violations).
3. Implement P0 suggested tests alongside R3/R4/R6 defect fixes — tests should fail first.
4. Add `R6CompositionFindingsTests` for throttle continuation and resume-token consume.
5. Waive `JS-AC-008` in spec doc 14 with rationale, or add host-level scheduled-start test.
6. When OTel lands (doc 15), add `OrcaCore.Observability.Tests` with contract tests for span/metric names.
