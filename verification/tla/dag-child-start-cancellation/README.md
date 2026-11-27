# DAG child start during parent cancellation

A owns the only node admission slot. Its workflow stream is still empty, and independent sibling B is ready. The parent commits cancellation. That transaction marks B cancelled and publishes a cancellation notice for A. To deliver the notice, a worker must first create or reattach A. Creating A commits both its workflow start and a runnable continuation. The worker dies before its next command can record cancellation in A's stream.

The parent barrier closes admission to new nodes. CP-028 requires the runtime to request cancellation of admitted children and wait for their terminal outcomes. The child start and child cancellation are separate commits. An already-admitted child can enter its first step in between. [CP-023 through CP-025 and CP-028](../../../docs/specs/08-requirements-composition.md#cp-023-one-durable-child-instance-per-node-occurrence) / [Parent cancellation](../../../src/OrcaCore.Durable.Hosting/Dag/DurableDagRunCancellation.cs) / [Child start and cancellation commands](../../../src/OrcaCore.Engine.Durable/Dag/DurableDagChildEngine.cs)

## Recovery orders

| First action after restart | First-step body | A's terminal fact and joined node | Parent after the join |
|---|---|---|---|
| Drive A through successful completion, then deliver child cancellation | Entered | Completed joins as Succeeded | Cancelled |
| Deliver child cancellation, then drive the stale continuation | Never entered | Cancelled by the owning DAG joins as Cancelled | Cancelled |

In either order, the parent must remain `CancellationRequested` until the authoritative join, even if A is already terminal. B must have no admission and no child stream. A late copy of A's original start notice must reattach to the same child. [Authoritative join](../../../src/OrcaCore.Durable.Hosting/Dag/DurableDagChildCoordinator.cs)

All four process-restart cases passed locally on PostgreSQL 17 and SQL Server 2022. The controller killed the seed process inside the successful child-start append, with the cancellation notice still claimed and a continuation persisted. A replacement reclaimed the same notice on attempt 2. Continuation-first recorded one entry in the marker file and returned output 8 from input 7. Cancellation-first recorded zero entries even after the original continuation was dispatched. In every case the committed parent history contained one A admission, the cancellation barrier, one authoritative A join, and the terminal record. B's deterministic workflow stream stayed empty. Redelivery of the three child notices preserved both journals and the marker count. [SQL restart scenario](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.ChildStartCancellation.cs) / [Process controller](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.ProcessRestart.cs)

## Cancellation during an admitted attempt

The overlap tests keep the continuation driver alive while a separate process delivers the owning DAG cancellation. The cancellation worker reads A's committed attempt-admission event and checkpoint from SQL. It verifies that the first attempt is in flight and that the driver holds a continuation claim for A. The test pauses the driver at one of two points.

- **After attempt admission.** Pause in the named step's service factory, before the driver receives the step object. The authored body has yet to enter.
- **After body entry.** Pause inside the authored body immediately after it records entry. The body has yet to change the proposed state or return a result.

The cancellation worker releases the pause only after it reads A's cancelled terminal event. An entry marker and a separate marker immediately before the step returns `Completed` identify physical execution. The child journal, checkpoint and parent join independently identify what the runtime committed. [Overlap schedules](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.ChildStartOverlap.cs)

| Pause point | Body entries when SQL cancellation committed | Body entries after release | Durable result |
|---|---:|---:|---|
| After attempt admission, before body entry | 0 | 1 | A remained cancelled |
| Inside the body | 1 | 1 | A remained cancelled |

On PostgreSQL 17 and SQL Server 2022, the step reached its success return in both cases. Its proposed state was 8. A fresh read after the driver exited still showed state 7, no output, zero committed step completions, and the same ordered child event IDs and identical checkpoint payload bytes observed after cancellation. B had no child stream or checkpoint. The parent waited for A's single cancelled join. Replaying the original notices preserved the result. Physical entry is observable even when the workflow records no successful step. [Step invocation](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs) / [Version-checked completion](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs)

## Model bounds and checks

The model holds the original start notice until recovery, so the *cancellation handler* creates A. It forces one crash at that commit boundary. It abstracts lease expiry and reclaim as an enabled recovery action and collapses provider retries, internal driver yields and delivery acknowledgements where they do not change the race. The model separates durable attempt admission, physical entry into the first-step body, and successful completion. A child cancellation that wins the durable race closes new attempt admission. A worker holding an earlier admission may still enter the body, but its stale success cannot replace the cancelled outcome. The drive notice represents continuation work for A and can stand for several wake records. [Attempt admission](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs) · [Body invocation](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs)

| TLC profiles | Evidence |
|---|---|
| `Source` | Full bounded safety search and eventual settlement under weakly fair progress. Checks one A start, B's exclusion, A's admission until join, an authoritative join before parent termination, and no new attempt admission after child cancellation, and preservation of the winning cancellation against stale success. |
| `BodyAfterBarrier`, `ContinuationFirst`, `CancellationFirst` | Reachability traces for entry after the parent barrier and both fully acknowledged recovery orders. |
| `CancellationInBody` | The witness reaches settlement with A cancelled after the body entered. The SQL case also observes the late `Completed` return. |
| `LateBodyEntry` | The attempt is durably admitted, child cancellation wins, and the worker then enters the body. |
| `EarlyParent`, `StaleAdmission`, `DuplicateChild`, `DriveCancelled`, `StaleSuccess` | Injected faults must violate the named invariant for premature parent terminality, admission of B after closure, a second child start, a new attempt after child cancellation, or a stale success overwriting cancellation. |

The source search exhausted 174 distinct states. Five reachability probes and five injected faults produced their designated counterexamples. All advancing actions are bounded. Progress assumes a replacement with the definition installed can eventually reclaim notices, an eligible action can run, and the test body can settle. The model has no repeated crashes, external side-effect ledger, or unbounded business step. [TLA+ module](DagChildStartCancellation.tla) / [TLC profiles](profiles.json)

## Reproduction

From the repository root, run the model with the pinned private JRE. The explicit SQL restart suites require Docker.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/dag-child-start-cancellation -ModuleName DagChildStartCancellation -CheckLivenessAtEnd
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.DagChildRecoveryTests.PostgreSqlChildStartCancellationProcessRestart -showLiveOutput -xml postgres.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.DagChildRecoveryTests.SqlServerChildStartCancellationProcessRestart -showLiveOutput -xml sqlserver.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.DagChildRecoveryTests.PostgreSqlChildStartCancellationOverlap -showLiveOutput -xml postgres-overlap.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.DagChildRecoveryTests.SqlServerChildStartCancellationOverlap -showLiveOutput -xml sqlserver-overlap.xml
```
