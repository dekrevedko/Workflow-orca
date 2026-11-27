# Late protected step and lease release

Workflow A holds the only ticket for a capacity-one pool. Its first step attempt is admitted but has yet to submit work to the protected resource. Workflow B requests the same unit. Another host cancels A while A's worker remains alive. A lookup at the resource finds no job for A's protection token. A can still submit its first operation after that lookup.

## What justifies release

MG-064 assigns the decision to a trusted integration. OrcaCore accepts a protection token and a confirmation ID, checks the obligation's state, and records the release and any FIFO promotion. The integration supplies the evidence about the external resource. [MG-064](../../../docs/specs/09-requirements-management-operations.md#mg-064-ticket-review-reconciliation-and-pool-operations) / [Lease lifecycle](../../../openspec/specs/durable-runtime/spec.md#requirement-durable-leases-are-lexical-occurrence-owned-obligations) / [Confirmation implementation](../../../src/OrcaCore.Engine.Durable/Driver/DurableResourceLeaseRecovery.cs)

| Schedule | Evidence available before confirmation | Expected transfer |
|---|---|---|
| A submits after cancellation | A's only submitter has finished, and the resource has recorded completion of A's accepted job. Until both hold, the ticket remains reserved. | B stays queued through A's late submission and return. Trusted confirmation grants B one successor ticket. |
| Resource fences A first | The resource has durably revoked A's token at the boundary used by every submit. In this schedule A has no active job when the fence takes effect. | B can use its successor ticket while A's worker is still alive. When A resumes, the resource rejects its submission. |

A confirmed release can advance A's workflow checkpoint to publish the receipt. Replaying confirmation must preserve B's ticket and provider generation. Ticket accounting and actual resource use are separate observations. A provider may account for one reserved unit even while external jobs from two different holders are active.

## TLC coverage

The model separates the workflow journal, the governance receipt and tickets, and work accepted by the protected resource. A single submit message can still be in transit after its worker retires. A fence prevents later acceptance, while an already accepted job must finish before its capacity can be reused. The provider trusts the caller's confirmation and cannot observe either external fact.

| Profile | Check |
|---|---|
| `Safety` | The two holders never use the external unit concurrently. Release has sufficient external proof, fencing excludes a late acceptance, and confirmation replay preserves one transfer. |
| `AfterProof` | Once sufficient proof exists, fair processing of the confirmation, publication, and B's admission eventually gives B use of the unit. |
| `Progress` | Eventual reuse from quarantine additionally assumes that the external worker, its pending request, and any accepted job can finish. |
| `LateSubmitRetained`, `FencedLateSubmit` | Reachability probes demonstrate both safe orders, including an empty lookup and replay after the transfer. |
| `EmptyLookupRelease` | A faulty caller confirms based on an empty lookup. B starts using the resource, A resumes, and the resource accepts A's first submit. Both are active in the final state even though governance holds only B's ticket. |
| `NoExternalResolution` | Removing the external progress assumption lets quarantine persist indefinitely. Fair processing by OrcaCore alone cannot finish someone else's job. |

The three complete checks each exhausted 227 distinct states. The unsafe empty-lookup control reached simultaneous use in eight states. It deliberately holds A until B starts to expose that ordering; the safety check explores all modeled orders. Bounds are one admitted attempt, one delayed submission by A, one B reservation and job, one irreversible A fence, and one confirmation identity with replay. [Module](LateProtectedStep.tla) / [Profile manifest](profiles.json)

## SQL evidence and limits

A runs in its own process against a fresh database. The controller observes A's committed attempt admission, B's queued occurrence, A's terminal cancellation, and the exact quarantined ticket. A SQL table owned by the test resource records each submission and its active state independently of OrcaCore. Every submit checks its token against the resource fence in a serialized transaction. Finishing an accepted job is a separate operation.

| SQL schedule | PostgreSQL 17 and SQL Server 2022 observations |
|---|---|
| `Stopped` | After cancellation and an empty lookup, A submitted and its process exited. The job remained active. A kept its ticket and B stayed queued until the job finished and the controller confirmed stop. B then ran under one generation-2 ticket. |
| `Fenced` | The resource revoked A's token with zero active A jobs. The controller confirmed release while A was still paused. B began work with one generation-2 ticket. When A resumed, the resource rejected its stale submit and B stayed active. |
| `EmptyLookup` (unsafe caller) | The controller confirmed on the strength of the empty lookup alone. B began work. A then submitted successfully, leaving two active jobs while the provider held only B's ticket. |

The bounded stop proof uses one submitter, a joined process with no pending submission, and a terminal external job. The fence proof relies on every submit path at the resource enforcing the same durable revocation. OrcaCore accepts a trusted caller's confirmation. A deployment must establish the applicable proof at its own resource boundary. The tests explicitly re-drive B through a fresh registry after promotion. [Provider scenarios](../../../tests/OrcaCore.Hosting.Tests/ProtectedStepLeaseRecoveryTests.cs), [protected worker](../../../tests/OrcaCore.Hosting.Tests/ProtectedStepLeaseRecoveryTests.Worker.cs), and [resource transactions](../../../tests/OrcaCore.Hosting.Tests/ProtectedStepLeaseRecoveryTests.JobResource.cs)

### Two grant-recovery findings

* **Missing tickets in the restored obligation.** In both safe schedules on both providers, B's attempt-admission checkpoint recorded `Held` and the right protection token but zero tickets. B's acquisition event and live governance record agreed on the same generation-2 ticket, and its protected job ran. The clean re-drive sets `Held` without copying the tickets into the obligation. The canonical contract requires the obligation to retain its exact tickets. The test reports the checkpoint count so a correction remains visible. [Driver recovery](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs) and [lease contract](../../../openspec/specs/durable-runtime/spec.md#requirement-durable-leases-are-lexical-occurrence-owned-obligations)
* **Missing lease context after correlated grant ingress.** A separate diagnostic delivered the correlated `ResourcePoolGranted` event through public ingress after B's reservation committed. Under both safe orders on both providers, B matched and consumed the wait and recorded an acquisition. At body entry, reading `context.ResourceLease.ProtectionToken` failed with a null lease context. The journal recorded release and terminal failure. The scoped resume carries the held obligation forward with its wait ID. The next driver stage removes the obligation by that same ID. The automated SQL rows use the fresh-registry path described above. [Scoped resume](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs), [obligation removal](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs), and [DR-AC-022](../../../docs/specs/16-requirements-durable-driver.md)

## Reproduction

Run TLC from the repository root using the pinned private JRE. The explicit SQL suites require Docker.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/late-protected-step-lease-release -ModuleName LateProtectedStep -CheckLivenessAtEnd
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.ProtectedStepLeaseRecoveryTests.PostgreSqlLateProtectedStep -showLiveOutput -xml postgres.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.ProtectedStepLeaseRecoveryTests.SqlServerLateProtectedStep -showLiveOutput -xml sqlserver.xml
```
