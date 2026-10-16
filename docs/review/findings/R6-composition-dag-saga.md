# R6 — Composition, DAG, Saga — Findings

> Phase scope: composition (`ForEach`, `Parallel`/`WhenFirst`/`WhenAll`), durable
> `RunChild`/`RunChildren`, DAG front-end (`WorkflowDagBuilder`), external-job composite,
> resource-pool acquisition (as used by external jobs), saga (`SagaBuilder` + durable
> aggregate saga commands + ephemeral saga runtime) in `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`,
> `OrcaCore.Engine.Durable`, and matching tests under `tests/**`. Reviewed against CP (all),
> SG (all), JS-* (scenario mapping). Primary lenses: correctness, spec conformance. Code review
> only. Cross-reference: R3/R4 findings for shared wait-matching and resource-pool ordering.

## Findings

### [P0] Durable `RunChildren` throttling never dispatches children beyond the initial window — `DurableWorkflowAggregate.cs:457`
- **Requirement/convention:** CP-023 / AC-609 / JS-007
- **Evidence:**
  ```csharp
  var initialDispatchCount = Math.Min(command.MaxConcurrency ?? children.Length, children.Length);
  // ...
  InitialDispatchCount = initialDispatchCount,
  NextDispatchIndex = initialDispatchCount,
  ```
  `NextDispatchIndex` is written once in `DecideRunChildren` and never read or updated elsewhere in the codebase (`grep` shows only event field + tests). `CreateChildStartOutboxRecords` emits outbox rows only for `Children.Take(group.InitialDispatchCount)` (`DurableCommandProcessor.cs:639`). `DecideChildCompleted` does not enqueue the next dispatch window.
- **Failure scenario:** Parent fans out 100 children with `maxConcurrency = 5`. Only the first 5 receive `child-start` outbox records; the remaining 95 never start even as the first 5 complete. EKS scheduler quota/throttle semantics (JS-007) are violated — the DAG can stall permanently after the first wave.
- **Recommendation:** On child completion (and on restart rehydration), compare active count to `maxConcurrency`, advance `NextDispatchIndex`, materialize the next child refs into `activeChildren`, and emit `child-start` outbox records in the same commit. Add an integration test: 4 items, `maxConcurrency=2`, complete one child → exactly one new `child-start` appears.
- **Confidence:** CONFIRMED (field is write-only; no code path dispatches index ≥ `InitialDispatchCount`)

### [P1] DAG batch compiler collapses heterogeneous child definitions to the first runnable node — `WorkflowDagBuilder.cs:226`
- **Requirement/convention:** JS-001 / CP-021
- **Evidence:**
  ```csharp
  var first = runnableNodes[0];
  return new WorkflowDagChildBatch(
      first.ChildDefinitionId,
      first.ChildDefinitionVersion,
      runnableNodes.Select(node => node.NodeId).ToArray(),
      first.FailurePolicy,
      runnableNodes.Count);
  ```
  `WorkflowChildrenScheduledEvent` carries a single `ChildDefinitionId` for the entire group (`DurableWorkflowAggregate.cs:469`). `DagBuilderTests.Diamond()` assigns definitions 1–4 to nodes A–D, but `DagAcceptanceTests` only asserts item snapshot strings, not per-child definition IDs.
- **Failure scenario:** Diamond DAG runs B and C after A completes. Both children are started with B's definition ID; node C (different job type/manifest) executes the wrong workflow — silent mis-scheduling in EKS.
- **Recommendation:** Extend `RunChildren` to support per-item `(DefinitionId, DefinitionVersion)` (or one child-start command per distinct definition), and fix `CreateChildBatch` to group runnable nodes by definition before batching.
- **Confidence:** CONFIRMED

### [P1] `WhenAny+CancelRemaining` records residual intent but does not cancel child instances — `DurableCommandProcessor.cs:673`
- **Requirement/convention:** CP-021 / CP-031 / CP-024
- **Evidence:** `ResidualIntentIfNeeded` emits `WorkflowChildResidualIntentRecordedEvent` with child IDs (`DurableWorkflowAggregate.cs:1880`). `CreateResidualOutboxRecords` maps this to outbox kind `"external-message"` with the raw event JSON (`DurableCommandProcessor.cs:673-681`) — no `child-cancel` / `CancelWorkflowCommand` outbox record. Apply removes residual children from `activeChildren` and their waits (`DurableWorkflowAggregate.cs:1660`) but does not signal child instances.
- **Failure scenario:** Parent `WhenAny` group of 3 children; first completes, residual intent recorded, parent resumes. The two losing children keep running to completion — violating `CancelRemaining` and wasting cluster quota.
- **Recommendation:** Emit typed `child-cancel` outbox records (mirroring `child-start`) for each residual child ID before the resume token; certify that cancelled children receive terminal commands.
- **Confidence:** CONFIRMED

### [P1] Parent resume token is recorded but never consumed — `DurableWorkflowAggregate.cs:1674`
- **Requirement/convention:** CP-024 / AC-610 / AC-611
- **Evidence:** `WorkflowParentResumeTokenRecordedEvent` apply is a no-op (`case WorkflowParentResumeTokenRecordedEvent: break;`). No `ConsumeParentResumeTokenCommand` (or equivalent) exists in `DurableCommandProcessor`. Tests (`ParentResumeTokenTests`, `ChildWorkflowAcceptanceTests`) only count token events in the stream.
- **Failure scenario:** Host relies on token event alone to continue parent; duplicate processing of the same completion or restart replays token without a consumption guard → parent join barrier or downstream DAG wave may fire twice.
- **Recommendation:** Add idempotent token consumption (durable fact + command) that gates parent continuation; tests should assert second consume is `NoOp`.
- **Confidence:** CONFIRMED (no consume path in codebase)

### [P1] Durable saga has no authoring/runtime interpreter — only ephemeral `StartSagaAsync` — `EphemeralWorkflowEngine.cs:186`
- **Requirement/convention:** SG-001 / SG-020 / SG-030
- **Evidence:** `SagaBuilder` produces `SagaDefinition<TState>` used by `EphemeralWorkflowEngine.StartSagaAsync`. Durable engine exposes only granular commands (`RecordSagaForwardActionCompletedCommand`, `RequestSagaCompensationCommand`, …) on `DurableCommandProcessor` with aggregate `Decide*` methods — no durable saga start/step interpreter. `SagaAcceptanceTests` and `EphemeralSagaTests` exercise ephemeral path only; durable saga tests are aggregate unit tests with hand-built event lists.
- **Failure scenario:** Production durable saga (SG-020) requires the host to manually translate every forward step into commands and keep compensation order in sync — error-prone, and no end-to-end durable saga AC (e.g. AC-406) is exercised through the public API.
- **Recommendation:** Add durable saga execution (or document/command-adapter contract explicitly as interim) that drives `SagaDefinition` through the durable command pipeline with restart certification.
- **Confidence:** CONFIRMED

### [P1] Parallel-branch wait matching ignores branch identity (carried from R3/R4) — `DurableWorkflowAggregate.cs:2101`
- **Requirement/convention:** CP-001 / EV-020 / AC-110
- **Evidence:** `Matches` uses `EventName` and `CorrelationId` only — `BranchId` on `DurableActiveWait` is stored in projections but not compared (`DurableWorkflowAggregate.cs:2101-2104`). Same pattern in ephemeral `WorkflowInstance.cs` (R3).
- **Failure scenario:** `Parallel` branches both `Wait("Approved", sameCorrelation)` — first event satisfies one branch's wait; the other branch never resumes; `WhenAll` join never fires.
- **Recommendation:** Include `BranchId` in wait match and consumed-wait dedup (both engines); add parallel-branch wait AC test with shared correlation.
- **Confidence:** CONFIRMED (shared with R3/R4; re-verified in durable aggregate)

### [P2] `WorkflowBuilder` has no `RunChild` / `RunChildren` authoring surface — `WorkflowBuilder.cs` (absent)
- **Requirement/convention:** CP-021
- **Evidence:** `grep` for `RunChild`/`RunChildren` in `OrcaCore.Core/Building` returns no matches. Child orchestration is only reachable via imperative `DurableRunChildCommand` / `DurableRunChildrenCommand` (e.g. `ChildWorkflowAcceptanceTests`).
- **Failure scenario:** Authors cannot declare child fanout in the fluent builder; DAG front-end compiles to commands externally — higher integration burden and no build-time validation of join/failure policies on the tree builder path.
- **Recommendation:** Add durable-only builder methods (or document CP-021 as command-only intentionally) with validation mirroring `ForEach` policies.
- **Confidence:** CONFIRMED

### [P2] `AllSagaCompensationsCompleteAfter` uses fragile key-match shortcut — `DurableWorkflowAggregate.cs:1992`
- **Requirement/convention:** SG-010 / SG-013
- **Evidence:**
  ```csharp
  return actions.Length > 0 && actions.All(action =>
      action.Status == SagaCompensationActionStatus.Completed ||
      string.Equals(action.ActionKey, completedActionKey, StringComparison.Ordinal));
  ```
  No integration test completes two compensations sequentially before terminal (`CompensationFailureTests` uses a single `CompensationStarted`).
- **Failure scenario:** Logic change could mark saga `Compensated` while a compensation action is still `Started` if the OR branch is misread during refactor; multi-step compensation completion order is unverified.
- **Recommendation:** Replace with explicit count of `Started`/`Completed`; add test: two `SagaCompensationStarted` events → complete both → single `Compensated` terminal.
- **Confidence:** PLAUSIBLE (current logic appears to work for 2+ actions when traced; test gap confirmed)

### [P2] AC-609 tests verify persisted throttle metadata only, not behavior — `DurableChildThrottlingTests.cs:14`
- **Requirement/convention:** AC-609 / CP-023 / 03 §2
- **Evidence:** Test asserts `InitialDispatchCount == 2`, `NextDispatchIndex == 2`, and 2 `child-start` outbox rows after duplicate `RunChildren` — never completes a child and never asserts a third/fourth start.
- **Failure scenario:** Regression of P0 throttling bug remains green in CI.
- **Recommendation:** Extend test: after first child completion, expect next `child-start` and `NextDispatchIndex` advance (once P0 is fixed).
- **Confidence:** CONFIRMED

### [P2] `CreateChildBatch` sets `MaxConcurrency` to runnable node count — `WorkflowDagBuilder.cs:232`
- **Requirement/convention:** CP-023 / JS-001
- **Evidence:** `MaxConcurrency` parameter is `runnableNodes.Count`, ignoring any global throttle the scheduler host might need when multiple nodes become runnable simultaneously.
- **Failure scenario:** Scheduler configured for cluster-wide `maxConcurrency = 1` still submits B and C in one batch with `MaxConcurrency = 2` after A completes.
- **Recommendation:** Accept optional throttle cap on `CreateChildBatch` / DAG driver; clamp batch size.
- **Confidence:** CONFIRMED

### [P2] DAG orchestration is manual in tests — no engine-integrated DAG runner — `DagAcceptanceTests.cs:25`
- **Requirement/convention:** JS-001
- **Evidence:** Acceptance test manually calls `GetRunnableNodes` and issues separate `DurableRunChildrenCommand` per wave — no hosted service or engine API drives the DAG to completion.
- **Failure scenario:** Integrators must reimplement wave logic; divergence from spec's "compile front-end lowers to standard semantics" without a reference driver.
- **Recommendation:** Provide a small `WorkflowDagRunner` (library or hosting) that closes the loop, or document as application responsibility with a reference implementation.
- **Confidence:** CONFIRMED

### [P3] Barrier/resume tests are sequential, not concurrent — `ParentResumeTokenTests.cs:23`
- **Requirement/convention:** AC-610 / NF-020
- **Evidence:** `foreach (var child in scheduled.Children) { await processor.ProcessAsync(...) }` — no `Task.WhenAll` race on last completions.
- **Failure scenario:** Latent race in resume-token emission under true concurrent child completions may go undetected (mitigated by per-instance serialization in processor, but spec calls for concurrent completions).
- **Recommendation:** Fire concurrent `ProcessAsync` for the last two children (different command processors OK if provider OCC serializes) and assert single token.
- **Confidence:** PLAUSIBLE

## Coverage note

**Reviewed source:**
- `OrcaCore.Core`: `WorkflowBuilder`, `WorkflowDagBuilder`, `SagaBuilder`, `Nodes`, ForEach policies, partitioners
- `OrcaCore.Engine.Ephemeral`: `Interpreter` (ForEach, Parallel, WhenFirst), `EphemeralWorkflowEngine` (saga), `WorkflowInstance` (ForEach group state)
- `OrcaCore.Engine.Durable`: `DurableWorkflowAggregate` (RunChild/RunChildren, compensation, external job, saga), `DurableCommandProcessor` (outbox mapping)
- Tests: `ForEachTests`, `ForEachPolicyTests`, `ParallelTests`, `RunChildrenTests`, `DurableChildThrottlingTests`, `ParentResumeTokenTests`, `ChildResidualPolicyTests`, `ChildLineageTests`, `ChildCompensationTests`, saga tests, `DagAcceptanceTests`, `DagBuilderTests`, `ChildWorkflowAcceptanceTests`, `RunExternalJobTests`, `EphemeralSagaTests`, `SagaAcceptanceTests`

**Requirements / ACs verified:**
| ID | Verdict |
|----|---------|
| CP-001 | **Fail** — branch-blind wait matching (cross-phase) |
| CP-002…005 | Partial — ephemeral Parallel/WhenAll/WhenFirst tested; durable parallel waits inherit CP-001 gap |
| CP-010…013 | Pass — ephemeral ForEach (AC-601…605) with behavioral tests |
| CP-020 | Pass — lineage metadata + AC-614 query |
| CP-021 | Partial — command shape exists; builder missing; heterogeneous DAG definitions broken |
| CP-022 | Partial — child-start outbox on initial window; throttle continuation missing |
| CP-023 | **Fail** — `NextDispatchIndex` never advanced (P0) |
| CP-024 | Partial — token recorded; consume idempotency missing; residual cancel incomplete |
| CP-025…026 | Pass — deterministic child IDs (AC-607/608), failure policy (AC-615) |
| CP-030 | Pass — partitioner + snapshot stability tests |
| CP-035 | Pass — explicit child compensation (AC-616), cancel does not compensate |
| CP-040 | Pass — DAG cycle validation (JS-AC-002); nesting via builder validation surveyed in Core |
| SG-001…003 | Pass — separate `SagaBuilder` kind; scopes in builder |
| SG-010…014 | Partial — compensation order/decision aggregate tests; durable interpreter missing |
| SG-020…024 | Partial — durable facts + audit projection (AC-407 PostgreSQL test); no end-to-end durable saga run |
| SG-030 | Pass — ephemeral saga labeled limited; docs test in `EphemeralSagaTests` |
| JS-001 | Partial — DAG builder validates/compiles; heterogeneous definitions + runner gaps |
| JS-002 | Pass — `RunExternalJob` composite (JS-AC-010/012/004/006/011/013) |
| JS-003 | Out of scope — K8s adapter not in library (per spec) |
| AC-401…409 | Ephemeral + aggregate durable saga tests present |
| AC-601…605 | Ephemeral ForEach — verified behavioral |
| AC-606…616 | Partial — AC-609/610 behavior gaps noted above |
| JS-AC-001…003 | DAG acceptance + builder tests; JS-AC-001 does not verify per-node definitions |

**Deferred / cross-phase:** Resource-pool acquire-before-commit (R4 P1) affects `RunExternalJob` JS-AC-010 path. Provider timer/host pump gaps (R5/R7) affect durable timer races on external-job timeout.

**Stopped at:** natural R6 boundary (composition, DAG front-end, saga, external-job composite). Hosting DAG driver and K8s adapters belong to R7 / application layer.
