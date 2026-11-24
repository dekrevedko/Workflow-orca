# Durable resource grant rollback and recovery

A scoped ticket belongs to one durable lease occurrence. Workers A and B can both observe that ticket. If A journals the grant and B loses its workflow append, B leaves the ticket with A. A replacement can adopt the original reservation after host loss. If the owner stops before activation, recovery reads the durable governance receipt to determine whether to record an exact release or a zero-ticket cancellation. A held lease with uncertain user work remains subject to quarantine. See the [durable handoff contract](../../../openspec/specs/durable-runtime/spec.md#scenario-workflowgovernance-handoff-crashes).

The binding [ownership decision](../../../docs/implementation/00-stack-decisions.md#scoped-grant-ownership-across-workflow-and-governance-commits) explains why a losing append leaves the ticket in place. [AC-527](../../../docs/specs/12-acceptance-criteria.md) defines the provider acceptance case.

## Model and controls

[ResourceGrant.tla](ResourceGrant.tla) models one occurrence, one unit of capacity, two contenders, and one queued rival. The workflow and governance commits are separate actions. The recovery experiment includes a stale admission that reads the owner before cancellation and tries to write afterward. It abstracts protected work to one activation and represents adoption with the original ticket identity.

| Check | Result |
|---|---|
| `SourceSafety` | All 51 distinct states explored. The ticket stays with the workflow both before and after activation. |
| `Recovery` | All 102 distinct states explored. A journalled grant retains its ticket, a closed token cannot reacquire, and the receipt agrees with whether a reservation committed. |
| Seven reachability witnesses | Retention across rollback before and after activation, legitimate transfer, replacement adoption, and compensation after stopping before reservation, after reservation, or after journalling. |
| Three safety mutations | Releasing on conflict, omitting the closed-token fence, and ignoring the owner checkpoint each produce their configured invariant violation. |
| Disabled recovery | The temporal control strands a closing occurrence. The normal progress property assumes that enabled governance closure and workflow publication eventually run. |

The recovery progress claim starts once the workflow has committed closure. Worker scheduling, outage duration, detection of host death, SQL lock implementation and the timing of a hosted sweep are outside the finite model.

## Provider evidence

The [ownership test](../../../tests/OrcaCore.ProviderCertification/DurableResourceGrantRaceCertification.cs) queues a rival instance through the pool store, holds A after workflow activation, and stops B after its conflicting append. It checks the live ticket and generation before A can enter the body. A second row omits B and checks legitimate transfer on exit. At revision `74cd0d05`, the competing row failed on both PostgreSQL 17 and SQL Server 2022. The rival already held generation 2 while A's checkpoint still held generation 1.

The [recovery tests](../../../tests/OrcaCore.ProviderCertification/DurableResourceGrantRaceCertification.Recovery.cs) use separate service providers on the same real database and partition. Injected exceptions stop a worker at a particular durable write or discard its acknowledgement. The tests exercise the production driver or hosted sweep on recovery. With the repair at `2ddc2cf4`, all ten cases (two ownership rows and eight recovery schedules) passed on PostgreSQL 17 and on SQL Server 2022.

| Schedule | Evidence checked on each database |
|---|---|
| `ResumeReserved`, `ResumePromoted` | A cold worker reuses the exact original or FIFO-promoted ticket, enters once, and records one matching release. |
| `CloseReserved`, `CloseJournalled` | A stopped, never-entered occurrence transfers its exact ticket once. Workflow history, terminal checkpoint and governance agree. |
| `CloseEmpty`, `CancelEmpty` | An acquire reads the live checkpoint before termination or cancellation request, then attempts its write before the sweep. It reserves zero units. |
| `CloseQueued` | A blocker's release reads the queued owner's live checkpoint before termination. Its subsequent write cannot promote that owner. |
| `ReceiptReplay` | A replacement stops after governance has transferred the ticket and before publishing the workflow release. Another host publishes the receipt without changing the successor ticket. |

A committed scoped release fences delayed acquisition with the occurrence's protection token. The [commit pipeline](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommitPipeline.cs) leaves a shared grant recoverable on conflict. The [governance aggregate](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs) validates owner checkpoint versions atomically with admission and promotion. [Settlement](../../../src/OrcaCore.Engine.Durable/Execution/DurableScopedReservationRecovery.cs) recovers terminal workflows even when their pool has no held tickets or waiters.

Deploy all grant writers together. Drain old hosts first because earlier writers can still release on conflict and their governance reader predates zero-ticket closure receipts. A custom provider or decorator must implement the [guarded governance capability](../../../src/OrcaCore.Provider.Abstractions/IDurableResourceGovernanceStore.cs) to admit scoped reservations.

### Other governance coverage

Four Section 3.11d Infrastructure scenarios passed at `13710495` and failed at
`3e2cb737`. In `four-post-commit-barriers`, `fact-equality-and-ticket-uniqueness`,
and `confirmation-and-tombstone-accounting`, the workflow wrote its checkpoint to
a separate in-memory provider. Governance could not see it and rejected admission.
The fixture now drives the workflow and checks its ticket against the same provider.

In `creation-versus-current-capacity`, each management host now receives the workflow
event store alongside the shared governance store. The test still creates independent
hosts to check that resize state survives replacement and incompatible creation options
are rejected. All four scenarios pass with their original assertions.
[Certification scenarios](../../../tests/OrcaCore.ProviderCertification/Section7GovernanceScenarioHost.cs)

## Reproduce

The [durable resource grant workflow](../../../.github/workflows/durable-resource-grant.yml) runs all model profiles on grant-related pull requests and pushes to master. Its `durable-resource-grant-evidence` artifact contains the per-profile verdicts, TLC logs, model inputs, and source hashes. The `OldReleaseOnConflict` control must reproduce the `ActiveGrantRetained` violation.

From the repository root, use the private pinned Java runtime supplied by the verification runner. The SQL probes require Docker and a Release build.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-resource-grant -ModuleName ResourceGrant
dotnet build OrcaCore.slnx -c Release
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests -c Release --no-build --filter FullyQualifiedName~ResourceGrant_
dotnet test tests/OrcaCore.Providers.SqlServer.Tests -c Release --no-build --filter FullyQualifiedName~ResourceGrant_
```
