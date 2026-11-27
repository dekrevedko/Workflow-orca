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

## Reproduction

Run TLC from the repository root using the pinned private JRE. The explicit SQL suites require Docker.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/late-protected-step-lease-release -ModuleName LateProtectedStep -CheckLivenessAtEnd
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.ProtectedStepLeaseRecoveryTests.PostgreSqlLateProtectedStep -showLiveOutput -xml postgres.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.ProtectedStepLeaseRecoveryTests.SqlServerLateProtectedStep -showLiveOutput -xml sqlserver.xml
```
