# R10 — Old `src/` → `current implementation` Parity Gaps

> Comparative review of the legacy implementation (`src/` + `tests/`) against the
> from-scratch rewrite (``). Primary lens: **what the old code proved in tests** that
> current implementation has not yet matched in implementation or test coverage.
>
> Cross-references: [R3](R3-ephemeral-engine.md)–[R9](R9-test-coverage-gaps.md) for
> v3-internal defect and test-depth findings. This document is the **src → v3 migration
> parity** view.

**Baseline (2026-07-03):**

| Area | Old (`src/` + `tests/`) | New (``) |
|------|-------------------------|-----------------|
| Source projects | 3 (`Abstractions`, `Runtime`, `EventDrivenPrototype`) | 12+ (split engines, providers, hosting) |
| Test projects | 2 (`OrcaCore.Tests`, `EventDrivenPrototype.Tests`) | 12 + `TestSupport` + `ProviderCertification` |
| Test files | ~55 | ~150 |
| Acceptance naming | `MC_AT_001`…`MC_AT_019`, `DRA-AT-001`…`004` | `AC-xxx` / `JS-AC-xxx` traits |
| Durable API | Monolithic `DurableWorkflowEngine` + `IWorkflowStore` | `DurableCommandProcessor` + provider ports |

---

## 1. Executive summary

current implementation is a **spec-driven rewrite**, not a line-by-line port. It **exceeds** the old
`src/` scope in several areas (sagas, ForEach, child workflows, DAG, external jobs, resource
pools, Continue-as-new, provider certification, hosting). Most **MC_AT-001–019** ephemeral
scenarios have AC-tagged equivalents.

Gaps fall into four buckets:

1. **Behavior regressions** — old tests passed; v3 has known bugs (parallel same-correlation
   waits, durable early-event poison, in-flight cancel, saga multi-step compensation, etc.).
   See R3/R4/R6.
2. **Test depth not ported** — old suite had ~35 `EphemeralNegativeTests`, 27
   `InterpreterEdgeCaseTests`, dedicated frame/serialization suites; v3 covers the happy path
   but not the full edge-case matrix.
3. **API / architecture deltas** — intentional redesign (no `WorkflowEngine.ForDefinition`,
   no monolithic durable engine, event-sourced store vs `StateMapper`) with incomplete public
   surfaces (hosting no-ops, SqlServer/Redis stubs).
4. **Removed experiments** — `EventDrivenPrototype` absorbed conceptually but not as a
   runnable alternate engine.

---

## 2. MC_AT acceptance parity matrix

Old tests: `tests/OrcaCore.Tests/Acceptance/MC_AT_*.cs` (19 files).  
New mapping: `tests/OrcaCore.Acceptance.Tests/` + engine unit tests.

| Old ID | Scenario | v3 equivalent | Parity |
|--------|----------|---------------|--------|
| MC-AT-001 | Straight-line completion | `StraightLineAcceptanceTests` (AC-001) | **Full** |
| MC-AT-002 | If true/false branches | `ControlFlowAcceptanceTests` (AC-002) | **Full** |
| MC-AT-003 | Wait → Waiting status | `WaitAcceptanceTests` (AC-101) | **Full** |
| MC-AT-004 | Match resumes once + payload | `WaitAcceptanceTests` (AC-102), `WaitMatchingTests` | **Full** |
| MC-AT-005 | Non-matching event/correlation | `WaitAcceptanceTests` (AC-103) | **Full** |
| MC-AT-006 | Parallel WhenAll join once | `ParallelAcceptanceTests` (AC-201) | **Full** |
| MC-AT-007 | Parallel wait isolation (different event names, same correlation) | `ParallelAcceptanceTests` (AC-007), `ParallelTests` | **Partial** — v3 adds same-correlation + `BranchId` tests; implementation still under fix (R3) |
| MC-AT-008 | Parallel order invariant | `ParallelAcceptanceTests` (AC-202) | **Full** |
| MC-AT-009 | Failing step + terminal event rejection (4 cases) | `TerminalAcceptanceTests` (AC-005, AC-011) | **Partial** — not all four sub-scenarios explicitly tagged |
| MC-AT-010 | Management query filters + validator | `ManagementAcceptanceTests`, `ManagementQueryTests` | **Partial** — query **validator** unit tests missing (see §4) |
| MC-AT-011 | WaitLong rejected on ephemeral | `DurableWaitTests.EphemeralBuilder_DoesNotExposeWaitLong` | **Inverted** — WaitLong is now durable-only by design (AC-303) |
| MC-AT-012 | Concurrent resume | `WaitAcceptanceTests`, `DurableCommandPipelineTests` | **Full** |
| MC-AT-013 | While loop body count | `ControlFlowAcceptanceTests` (AC-003), `InterpreterControlFlowTests` | **Full** |
| MC-AT-014 | Wait inside while (3 cases) | `LoopWaitAcceptanceTests` (1 case), `LoopWaitTests` (4 cases) | **Partial** — missing multi-iteration walk with `GetActiveWaits` per iteration; payload-to-next-step covered in unit tests |
| MC-AT-015 | Out-of-order event buffered | `MailboxAcceptanceTests` (AC-104) ephemeral | **Partial** — durable poisons early events (R4 P1) |
| MC-AT-016 | Duplicate EventId dedup | `MailboxAcceptanceTests` (AC-105) | **Full** ephemeral |
| MC-AT-017 | Correlation-targeted routing | `RoutingAcceptanceTests` (AC-106, AC-107) | **Full** |
| MC-AT-018 | Definition fanout scoped | `RoutingAcceptanceTests` (AC-108) | **Full** |
| MC-AT-019 | Ambiguous/missing correlation (6 cases) | `RoutingAcceptanceTests`, `RoutingTests` | **Partial** — ambiguity-after-one-instance-completes not explicitly ported |

### DRA-AT durable acceptance (old only)

| Old ID | Scenario | v3 equivalent | Parity |
|--------|----------|---------------|--------|
| DRA-AT-001 | Concurrent resume on cold instance + OCC | `DurableCommandPipelineTests`, `EventStoreCertificationTests`, `R4DurableEngineFindingsTests` | **Partial** — cold-load serialization not full engine E2E |
| DRA-AT-002 | History accumulation / inspectable | `DurableManagementTests`, AC-312 | **Partial** — shallow counts |
| DRA-AT-003 | Identity stable across restart | `DurableRecoveryTests` (AC-301), provider IT | **Full** via PostgreSQL |
| DRA-AT-004 | Purge does not remove active waiting instance | `RetentionAcceptanceTests`, `RetentionCertificationTests` | **Partial** — timer rows not purged (R5 P1) |

---

## 3. Old test suites without v3 equivalents

### 3.1 High-value missing coverage

| Old test file | What it proved | v3 status |
|---------------|----------------|-----------|
| `EphemeralNegativeTests.cs` (~35 tests) | Builder order violations, unknown instance ops, duplicate registration, fanout-to-completed throws, disposed engine, empty queries, parallel all-fail, exception step id capture | **Mostly missing** — scattered partial coverage in builder tests only |
| `InterpreterEdgeCaseTests.cs` (~27 tests) | Empty if/while branches, nested control flow, 3 sequential waits, parallel fail/succeed, yield, dynamic `WaitForEvent`, `ResumedEvent` null vs set, first-step-only payload | **Partial** — `InterpreterControlFlowTests` (~5), `YieldTests`, `WaitMatchingTests` cover subset |
| `NestedWaitNoDoubleExecutionTests.cs` | Steps before wait in while **not re-executed** on resume | **Missing** — critical invariant; only iteration isolation tested |
| `NestedWaitResumeTests.cs` | Wait-in-while across 3 iterations, if-in-while, buffered event in while | **Partial** — `LoopWaitTests` covers iteration isolation |
| `WorkflowRuntimeFrameTests.cs` | Frame stack preserved through wait/fail/parallel resume | **Missing** — `ExecutionPointer` tested in Core only, not runtime observability |
| `SerializedExecutionEdgeCaseTests.cs` | Concurrent parallel resumes, cross-instance correlation stability | **Partial** — `ExecutionLaneTests`, `ParallelTests` |
| `WorkflowQueryValidatorTests.cs` | Expression whitelist (reject method calls, arithmetic, indexers, ternary, `new`) | **Missing** — validator exists in `EphemeralManagement` but **no dedicated tests** |
| `InstanceScopeTests.cs` | Snapshot fields, dedup, buffering, terminal raise, active-waits filter | **Partial** — `ManagementQueryTests` |
| `CorrelationIndexTests.cs` | `ResolveExactlyOne`, `TryResolveSingle`, remove semantics | **Partial** — logic inlined in engine; no isolated unit tests |
| `EventMatcherTests.cs` | Match ordering, skip matched/cancelled | **Partial** — `WaitMatchingTests` |
| `ResumeRouterTests.cs` | Top-level resume delivers payload to next step | **Covered** — `WaitMatchingTests.ResumedEvent_*` |

### 3.2 Durable store / persistence (old `tests/OrcaCore.Tests/Durable/`)

| Old test file | What it proved | v3 status |
|---------------|----------------|-----------|
| `DurableWorkflowEngineTests.cs` (37 tests) | WaitLong restart/eviction, fanout mixed results, inbox/outbox/history, dedup after restart, commit failure rollback, outbox dispatch manual/auto/poison, delete/purge, cold-load concurrency | **Redistributed** across `Durable*Tests`, provider cert, acceptance — **no single parity file**; several scenarios weaker (see R4) |
| `InMemoryWorkflowStoreTests.cs` (28 tests) | Full `IWorkflowStore` contract: atomic commit, OCC token, outbox lease ordering, purge cutoffs, projection work | **Replaced** by `EventStoreCertificationTests` + `InMemoryProviderCertificationTests` — different API (`IWorkflowEventStore` + ports) |
| `StateMapperRoundTripTests.cs` (8 tests) | Persist/restore waits, if/while/parallel frames, custom payload, unknown type/path failures | **Missing equivalent** — v3 uses event stream + checkpoint; no round-trip mapper tests for interpreter position |
| `DurableOutboxPumpTests.cs` (10 tests) | Retry, poison, observer, field mapping | **Partial** — `DurableOutboxTests`, cert suite |
| `DefinitionPathIndexTests.cs`, `ExecutionFrameNodePathTests.cs` | Deterministic node paths for durable frames | **Partial** — `DefinitionModelTests` for `ExecutionPointer` only |
| `DefinitionVersionMismatchTests.cs` | Version registration rules for waiting instances | **Partial** — `DurableVersioningTests` |
| `PayloadEnvelopeSerializerTests.cs` | JSON envelope round-trip | **Missing** — different serialization model (workflow events) |
| `MessagingContractTests.cs`, `OutboxRecordContractTests.cs` | Contract invariants | **Partial** — `ProviderPortContractTests`, cert |
| `ExceptionHierarchyTests.cs` | Exception inheritance stability | **Missing** |
| `DurableSelectionScopeTests.cs` | Hot+cold query, bulk delete | **Partial** — `DurableManagementTests`, `DurableQueryTests` |

### 3.3 Event-driven prototype (removed)

| Old | v3 status |
|-----|-----------|
| `OrcaCore.EventDrivenPrototype` + 3 test files (14 tests) | **Not ported** as separate project. Concepts (event stream, checkpoint commit, correlation projection) live in `DurableCommandProcessor` + providers. No prototype-specific regression tests. |

---

## 4. Implementation gaps (old behavior → v3)

### 4.1 Ephemeral engine

| Old capability | Old location | v3 status | Gap |
|----------------|--------------|-----------|-----|
| Typed `WorkflowEngine.ForDefinition(def)` | `WorkflowEngine.cs` | `RegisterDefinition` + `StartAsync(definitionId)` | API change — no compile-time typed engine wrapper |
| `InstanceScope` / `SelectionScope` | `Querying/*` | `EphemeralManagement` | Equivalent; naming changed |
| `CorrelationIndex` component | `Routing/CorrelationIndex.cs` | Inline `waitIndex` on engine | Behavior similar; no O(1) index at scale (R3 P2) |
| `StagedCorrelationMutationSink` | `Routing/` | Not found | Correlation mutations during execution may differ |
| `InstanceCommandLane` + `DisposeAsync` on engine | `Storage/` | `InstanceExecutionLane` | No engine-level `IAsyncDisposable` semaphore cleanup tests |
| `GetState` JSON deep copy | `InstanceScope` | `GetState<T>` copy semantics | **Tested** in `ManagementQueryTests` |
| Dynamic `StepResult.WaitForEvent` | Interpreter | `Interpreter.cs` | **Implemented**; tested in `WaitMatchingTests`, ForEach tests |
| `ResumedEvent` first-step-only | Interpreter | Same contract | **Tested** in `WaitMatchingTests` |
| Parallel waits same correlation | `ParallelNode` + `EventMatcher` | Branch-aware delivery **designed** in tests; implementation **buggy** (R3 P1) | **Regression vs old MC-AT-007 intent** when same event name + correlation |
| No re-execution on wait resume | `WorkflowRuntime` | Interpreter | **Risk** — no `NestedWaitNoDoubleExecutionTests` equivalent |
| `FanoutDispatchResult` per-instance mixed results | Durable + ephemeral | `RaiseEventByDefinitionAsync` | Ephemeral covered; durable fanout less exercised |
| `WorkflowQueryValidator` expression restrictions | `WorkflowQueryValidator.cs` | `QueryPredicateValidator` in management | **Validator untested** |

### 4.2 Durable engine

| Old capability | Old location | v3 status | Gap |
|----------------|--------------|-----------|-----|
| Monolithic `DurableWorkflowEngine` | `Durable/Engine/` | `DurableCommandProcessor` + `DurableStartService` + `DurableManagement` | **No single public durable interpreter** — saga/child E2E gaps (R6) |
| `IWorkflowStore` atomic commit | `Durable/Persistence/` | `IWorkflowEventStore` + `ProviderCommitBatch` | Redesigned; cert covers core invariants |
| `StateMapper` round-trip | `StateMapper.cs` | Event-sourced facts + checkpoint | **No frame/wait round-trip test suite** |
| `DurableInstanceManager` hot/cold eviction | `Durable/Execution/` | `EvictAfterCommit` on aggregate | **Partial** — no full WaitLong cold eviction E2E (AC-304) |
| `WaitLong` + immediate eviction | Durable builder | `DurableWorkflowBuilder.WaitLong` | Builder exists; full restart+eviction path thin |
| Inbox dedup across restart | Store + engine | Provider cert AC-305 | **Covered** for InMemory/PostgreSQL |
| Outbox lease ordering (stream version + sequence) | `InMemoryWorkflowStore` | Provider outbox cert | **Covered** |
| Commit failure restores in-memory state | `DurableWorkflowEngine` | `EventStoreCertificationTests.CommitFailure_*` | **Covered** at provider layer |
| `DurableArtifactRetentionPolicy` | Management | `RetentionPolicy` + cert | **Covered**; timer purge gap (R5) |
| `GetStateAsync` on durable instance scope | `DurableInstanceScope` | Checkpoint / projection reads | Different API — state via events not direct mapper |
| Definition version mismatch on re-register | `DefinitionVersionMismatchException` | `DurableVersioningTests` | **Partial** |
| Deterministic outbox IDs | Outbox records | Strongly-typed `OutboxRecordId` | **Covered** in cert |

### 4.3 Providers & hosting (new in v3, gaps vs old + spec)

| Item | Gap |
|------|-----|
| `SqlServerWorkflowStore` | In-memory stub behind SQL connection — **false certification** (R5 P0) |
| `RedisProjectionStore` | Never reads/writes Redis (R5 P1) |
| Hosted outbox pump / timer / sweep | Registered but **no-op** (R7 P0) |
| OpenTelemetry | Not implemented (doc 15) |
| RabbitMQ publisher confirms | Not awaited (R5 P1) |

---

## 5. What v3 adds (not in old `src/`)

These are **not gaps** — intentional expansion beyond the old baseline:

- Sagas (`SagaBuilder`, compensation lifecycle) — AC-401+
- `ForEach` with failure policies — AC-601+
- Durable child workflows (`RunChild` / `RunChildren`) — AC-606+
- DAG builder/runner (EKS scenario) — JS-AC-*
- External jobs + resource pools — JS-AC-004+, AC-518+
- `ContinueAsNew` — AC-313
- `WhenFirst` parallel join — AC-204/205
- Step retry/timeout policies — AC-510, AC-113
- Lifecycle machine (`Paused`, `Cancelled`, `Terminated`, compensation states)
- Strongly-typed IDs (`InstanceId`, `EventId`, `WaitId`, …)
- Provider certification harness + PostgreSQL real persistence
- `RepositoryGuardTests` AC catalog enforcement

---

## 6. Known bugs: old tests would fail on v3

These are cases where **old tests encoded correct behavior** and v3 implementation is
**deficient** (documented in R3–R7). v3 may have added tests that currently fail or tests
that codify wrong behavior.

| Symptom | Old test anchor | v3 finding |
|---------|-----------------|------------|
| Parallel branches, same `(EventName, CorrelationId)` — second branch stuck | MC-AT-007 style (different events; same correlation is stricter) | R3 P1, R4 P1 — branch-blind `FirstOrDefault` matching |
| Early event before wait registers → buffered | MC-AT-015 | R4 P1 — durable poisons instead of buffers |
| `CancelAsync` during in-flight step | (spec AC-014; old untested) | R3 P1 — no instance CTS |
| Wait timeout then stale mailbox resumes wrong wait | (old partial) | R3 P1 — timeout path skips `consumedWaits` |
| Shared timer list across instances | (old untested) | R3 P1 — unsynchronized `List<>` |
| `StartOrGet` after process restart creates duplicate | (old in-process only) | R4 P0 — process-local idempotency map |
| Multi-step saga compensation terminal after first action | (old N/A) | R4 P1 |
| `RunChildren` throttle never dispatches slot 3–4 | (old N/A) | R6 P0 |
| Parent resume token recorded but not consumed | (old N/A) | R6 P1 |

---

## 7. Test infrastructure comparison

| Aspect | Old | v3 |
|--------|-----|-----|
| Acceptance ID scheme | `MC_AT_xxx` | `AC-xxx` traits + catalog guard |
| Findings regression tests | None | `R4DurableEngineFindingsTests` only |
| Provider integration | InMemory only in main suite | PostgreSQL Testcontainers, partial SqlServer/Redis |
| Negative / edge-case density | High (`EphemeralNegativeTests`, `InterpreterEdgeCaseTests`) | Lower — breadth over depth |
| Wall-clock in tests | Some | `Task.Delay` in 3 files — violates own guard (R9) |
| CI coverage gate | No | Collects coverlet; no threshold (R9) |

---

## 8. Prioritized remediation (parity-focused)

### P0 — Restore old-proven behavior

1. Fix parallel branch wait matching + `HasConsumedWait` branch scope (R3/R4) — add failing test from `ParallelTests` same-correlation suite.
2. Durable early-event mailbox buffering (AC-104 / MC-AT-015 durable path).
3. Port `NestedWaitNoDoubleExecutionTests` scenario — steps before wait in while must not re-run on resume.
4. `StartOrGet` durable idempotency across restart (AC-311).

### P1 — Port high-value old test matrices

5. `WorkflowQueryValidatorTests` equivalent for `QueryPredicateValidator`.
6. `EphemeralNegativeTests` critical subset: unknown instance, builder order, terminal raise rejection, fanout-to-completed.
7. `InterpreterEdgeCaseTests` subset: empty branches, parallel one-fail-all-fail, three sequential waits.
8. Durable WaitLong cold eviction E2E (AC-304) — mirrors old `DurableWorkflowEngineTests` WaitLong cases.
9. `R3EphemeralEngineFindingsTests` mirroring R4 pattern.

### P2 — Architecture / provider truth

10. Replace SqlServer/Redis stubs or exclude from certification.
11. Wire hosting pump/timer hosted services + integration test.
12. State/checkpoint round-trip tests replacing `StateMapperRoundTripTests` intent.
13. MC_AT-019 ambiguity-after-complete scenario.
14. MC_AT-014 multi-iteration `GetActiveWaits` walk in acceptance.

---

## 9. Traceability quick reference

```
Old ephemeral acceptance     → tests/OrcaCore.Acceptance.Tests/
Old execution edge cases     → tests/OrcaCore.Engine.Ephemeral.Tests/Execution/
Old durable engine/store     → tests/OrcaCore.Engine.Durable.Tests/ + ProviderCertification/
Old prototype                → (no direct successor)
Old MC_AT IDs                → docs/specs/12-acceptance-criteria.md AC-xxx
Implementation defects       → docs/review/findings/R3–R8
Test depth gaps (v3-internal)→ docs/review/findings/R9-test-coverage-gaps.md
```

---

## 10. Summary scorecard

| Category | Old test count (approx.) | v3 coverage | Gap severity |
|----------|--------------------------|-------------|--------------|
| MC_AT ephemeral acceptance | 35 methods / 19 files | ~90% scenarios tagged | Low–medium |
| Interpreter edge cases | ~27 | ~40% | **High** |
| Negative / error paths | ~35 | ~15% | **High** |
| Durable engine integration | ~37 | Redistributed, ~70% depth | Medium |
| Store contract | ~28 | Cert suite (new API) | Low (different model) |
| StateMapper / frames | ~9 | ~10% | **High** |
| Prototype | 14 | 0% | N/A (removed) |
| New spec features (saga, DAG, etc.) | 0 | Extensive | v3 ahead |

**Bottom line:** current implementation **matches or exceeds** the old `src/` feature set at the spec level and
covers most MC_AT acceptance scenarios under AC tags. The largest **parity risk** is not
missing acceptance files — it is **shallow edge-case and negative testing**, **known
implementation bugs** in wait/routing/durable paths the old suite would have caught, and
**incomplete provider/hosting surfaces** that old code did not have but v3 advertises.
