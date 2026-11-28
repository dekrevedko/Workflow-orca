# Competing resource grant wakes

A queued workflow B already owns one blocked resource obligation and one active `ResourcePoolGranted` wait. After A releases the pool's only unit, B can observe its promotion in a fresh host or receive a public event correlated to B's holder key. Both paths can prepare work against the same queued stream version. The model follows that single occurrence through its first durable body-attempt admission.

## Persistence boundaries

| Transition | Durable effect |
|---|---|
| Promote B | One governance append removes B's provider waiter and grants B the successor ticket. B's workflow wait stays active. The public event has a separate publisher. |
| Accept the public event | Correlation ingress persists one `Received` inbox record. A publisher can submit even after the wait has been cancelled. |
| Match the active wait | One version-guarded workflow batch appends `WorkflowWaitMatchedEvent` and updates the inbox to `Applied`. Replay removes the active wait and creates its pending resume. |
| Acquire directly | A fresh driver loads the queued checkpoint, finds the promoted ticket, and calls scoped acquire. One guarded batch appends `WorkflowWaitCancelledEvent` and `WorkflowResourcePoolAcquiredEvent`. If this batch wins, the event stays available in the inbox as `Received`. The occurrence has zero matches, pending resumes and consume events. |
| Acquire after a match | Either driver can load the matched occurrence. Scoped acquire returns the same ticket. One guarded batch appends `WorkflowResumeConsumedEvent` and `WorkflowResourcePoolAcquiredEvent`. |
| Activate and admit | A subsequent guarded commit records the held lease and advances the fiber. A separate guarded attempt-admission checkpoint records the logical operation key, attempt number and in-flight flag. |

The hosted governance aggregate checks the partition version and the scoped waiter's checkpoint before a promotion. A repeated acquire with sufficient units under the same holder returns the existing ticket. The model fixes one holder, wait ID, fiber, scope, protection token and one-unit requirement. Its ticket stands for B's promoted ID and generation. [Hosted governance aggregate](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs), [PostgreSQL governance store](../../../src/OrcaCore.Providers.PostgreSql/PostgreSqlResourceGovernanceStore.cs), [SQL Server governance store](../../../src/OrcaCore.Providers.SqlServer/Internal/SqlServerResourceGovernanceStore.cs)

`IWorkflowEventIngress` accepts the correlated message first. With no active wait, acceptance alone creates no resume. A match prepared on an older version can conflict. A subsequent delivery against the current state finds no active wait and leaves the accepted event available. Inbox retention or matching by a future occurrence lies outside this run. [Public ingress](../../../src/OrcaCore.Engine.Durable/Facade/DurableWorkflowEventIngressCore.cs), [wait matching](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableWaitTimerCommandHandler.cs), [inbox batch](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommitMaterializer.cs), [no-mutation handling](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommitPipeline.cs)

Each executor's plan captures the version it read. The scoped call can return the provider ticket before the workflow append loses. An executor discards a stale plan and loads current durable state before deciding its next command. `RejectStale` represents rejection at the driver's expected-version check or the provider's batch compare-and-append. The SQL providers persist an accepted batch atomically. [Command processor](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs), [PostgreSQL append](../../../src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs), [SQL Server append](../../../src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStateEngine.cs)

The direct path recovers a promoted active wait. The matched path reconciles a pending resume on the same blocked fiber, which can be picked up by the executor that published the match or by a fresh executor. Both paths commit acquisition before activation. Both persist the first attempt before body dispatch. [Suspension recovery](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Initialization.cs), [resume reconciliation](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs), [acquisition batch](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableResourcePoolCommandHandler.cs), [wait replay](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableWaitState.cs), [activation and admission](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs)

## Profiles

| Profile | Required result |
|---|---|
| `Safety` | All interleavings preserve the wait, inbox and ticket relationships, with at most one acquisition and one durable admission. A committed acquisition leaves no active wait or pending resume. |
| `Progress` | Under weak fairness every run settles at one admission, and every match eventually has a committed consumption. |
| `MatchFirstIngress`, `MatchFirstRedrive` | Both commands prepare at the queued version and the direct caller obtains the promoted ticket. Ingress commits the match first. The stale direct command loses. Each named executor can commit the subsequent consumption and acquisition. |
| `DirectFirst` | Both commands prepare at the queued version. Direct acquisition commits first, the stale match loses, and B settles with the inbox event still `Received`. |
| `LateIngress` | Direct acquisition clears the wait before the public event is accepted. B settles with the unmatched event in the inbox. |
| `MissingResumeConsumption` | Omitting consumption from the matched acquisition leaves a pending resume beside the journalled ticket. `NoOrphanResume` fails. |
| `UnfencedDirect` | A stale direct append can commit after the matched acquisition. The journal contains two acquisitions for the same provider ticket. `SingleAcquisition` fails. |
| `UnfencedAttempt` | Two executors load the same held checkpoint. Letting a stale admission append produces two durable admission commits. `SingleAdmission` fails. |
| `UnscheduledWork` | Without fair service, execution can stutter after a match and strand the pending resume. `ResumeEventuallyConsumed` fails. |

The bound includes two independent executors, one event, one valid promotion, one protected body step, and one stream. The normal progress profile assumes available stores, a public event publisher, continued execution or fresh re-drive after a conflict, and no crash, cancellation, expiry, retry policy or unrelated stream writer. Since each enabled non-stuttering transition advances finite work, weak fairness of `Next` suffices here. TLC counts committed admission records. Recovery can execute an already admitted operation again. Worker invocations, accepted external jobs and external idempotency require their own evidence. [Hosted provider scenarios](../../../tests/OrcaCore.Hosting.Tests/ProtectedStepLeaseRecoveryTests.cs)

From the repository root, use the pinned runner. Witness, mutation and assumption profiles succeed only when the runner validates the specified counterexample.

```powershell
pwsh -File scripts/verification/Invoke-TlaModelCheck.ps1 `
    -ModelDirectory verification/tla/durable-resource-grant-wake `
    -ModuleName GrantWake -CheckLivenessAtEnd
```
