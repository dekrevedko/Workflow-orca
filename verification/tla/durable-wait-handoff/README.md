# Durable wait handoff

The [durable verification proposal](proposal.md) defines the acceptance criteria. The starting state has one persisted active wait and one accepted, matching inbox event for one target. Two workers can independently read the same work. At most one worker crash discards its local decision or commit reply. The model checks when the persisted state permits a worker to retry.

| Action | Implementation boundary |
|---|---|
| Read | Load the eligible wait and received event, or observe committed resume work. Each worker retains its local decision until it retries, commits, or crashes. |
| CommitMatch | Match the exact wait, advance inbox ownership to Applied, and persist the continuation signal together. A stale attempt leaves the stored state alone. |
| CommitResume | Check the expected version and consume the outstanding resume along with the resulting workflow transition. A second worker can attempt the same transition. |
| Crash / Reply | Losing the worker's local result leaves the stored changes available to the next read. The before-commit witness requires a still-active wait. The lost-reply witness requires a committed match and outstanding resume. Both witnesses then reach one completion. |

The match decision and persisted pending resume are represented in [DurableWaitTimerCommandHandler](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableWaitTimerCommandHandler.cs) and [DurableWaitState](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableWaitState.cs). The in-memory provider validates the expected version and applies the batch under a lock. See [InMemoryWorkflowProvider](../../../src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs). The logical pending flag represents outstanding workflow work. A physical continuation record can remain claimable until a later disposition.

Safety checks that consuming the wait and applying its event leaves either a recoverable resume or a completed transition, and that the transition commits at most once. Progress assumes an available matching definition, eventual execution of continuously enabled worker actions, and at most one crash. The bounded model covers a single event and occurrence. Claim expiry, timers, poison handling, payloads, retention, SQL transaction isolation and physical business-step invocation are outside the model.

Both normal profiles completed at 116 distinct states. The lost-handoff mutant violated ownership, and a stale competing commit violated single completion. Both crash witnesses reached their required states. The linked recovery theory injects two forms of pre-commit failure. The competing-owner test recreates a facade over the same provider and verifies one recorded match and one consumed resume. These are three executable cases using an in-memory store. [Recovery tests](../../../tests/OrcaCore.Engine.Durable.Tests/Recovery/DurableRecoveryTests.cs)

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-wait-handoff -ModuleName DurableWaitHandoff
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj -c Release --filter "FullyQualifiedName~FailedInlineConsumption_IsLaterAppliedWithoutBrokerResubmission|FullyQualifiedName~CallbackOnlyAcceptance_CommitsHandoffAndCompetingDefinitionOwnersResumeOnce"
```
