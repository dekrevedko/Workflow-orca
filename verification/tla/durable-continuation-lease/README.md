# Durable continuation lease expiry

Worker A claims a `continue` record and pauses. Worker B claims the same record at A's expiry, commits the runnable position, and marks the record `Dispatched`. A still holds the record ID. Its delayed call has the following effects on the in-memory, PostgreSQL and SQL Server providers. [Scope](proposal.md) · [Model](ContinuationLease.tla) · [Provider tests](../../../tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.ContinuationLease.cs)

| Delayed call by A | While B holds attempt 2 | After B marks the record dispatched |
|---|---|---|
| Mark retryable | Clears B's lease. A poll inside that lease obtains attempt 3. | Reopens the record. A poll obtains attempt 3. |
| Release | Clears B's lease. A poll inside that lease obtains attempt 3. | Leaves the record dispatched. A poll returns no record. |

The provider checks the claim request's timestamp against the stored expiry and increments the attempt on each successful claim. The continuation pump passes only the record ID to its disposition methods. A retry mark can update any non-poisoned row. Release changes only a currently claimed row. A's first attempt and B's second attempt therefore identify the same target for both writes. [Provider port](../../../src/OrcaCore.Provider.Abstractions/ProviderPorts.cs) · [Pump](../../../src/OrcaCore.Engine.Durable/Driver/DurableContinuationPump.cs) · [In-memory implementation](../../../src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs) · [PostgreSQL implementation](../../../src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs) · [SQL Server state engine](../../../src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStateEngine.cs)

### What TLC checked

The model covers one record, one runnable position, one expired lease, one delayed call, and at most three successful claims. B and the third claimant can read the same position before either appends. A late claimant that reads a completed position has nothing to append.

| Profiles | Local result |
|---|---|
| Current safety and fair settlement | Each exhausted 60 distinct states. Dispatch requires committed progress. At most one append commits. With weak fairness for the finite set of steps, the record eventually settles as dispatched. |
| Four ordering witnesses | All four reached their named trace. The early traces reach attempt 3 before B dispatches. After dispatch, the retry trace reaches a second dispatch after a no-op drive. The release trace settles at attempt 2. |
| Append guard disabled | Two drivers read the original version and both append. The injected fault violates `SingleCommit`. |
| Hypothetical attempt-checked dispositions | Exhausted 17 distinct states. B's claim and dispatch survive A's stale writes. The implementation cost would be carrying the acquired attempt through mark and release and checking it atomically in each store. |

DR-034 requires harmless duplicate continuation processing. It does not specify that dispatch is irreversible or that disposition must carry the claim generation. The observed extra poll is real. It produced no duplicate committed workflow transition in this experiment. Models with further expiries, repeated stale writers, crashes, poison or non-transactional business effects would need additional evidence. [Durable driver requirements](../../../docs/specs/16-requirements-durable-driver.md#dr-034-restart-safe-continuation-signal)

### What ran against providers

Five shared cases passed on each of the in-memory provider, PostgreSQL 17 and SQL Server 2022. The test fake passed the same five cases separately. Four cases use a five-second lease. They claim at second 10, check exclusion at 14, reclaim at 15, and probe before B's expiry at 20. They check the same record ID, the dispatch attempt, the immediate state, and the committed event. In the two pre-dispatch cases, a competing append at the original version must fail.

The fifth case starts a real budget-limited workflow and runs the replacement continuation pump on the same store. After completion it drains any successor signals, confirms an empty poll, and applies A's retry. The next pump call processes exactly one record and marks it dispatched. The full ordered event IDs match the history after completion. This exercises the actual no-op drive on each provider.

### Reproduce

Run from the repo root with PowerShell. The model uses the private pinned JRE. The two SQL projects start containers through their existing test fixtures.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-continuation-lease -ModuleName ContinuationLease
$projects = 'OrcaCore.ProviderCertification', 'OrcaCore.Providers.PostgreSql.Tests', 'OrcaCore.Providers.SqlServer.Tests'
foreach ($project in $projects) {
    dotnet test "tests/$project/$project.csproj" -c Release --filter 'FullyQualifiedName~ContinuationLease_' --logger trx
    if ($LASTEXITCODE -ne 0) { throw "Lease verification failed for $project" }
}
```

The [durable verification workflow](../../../.github/workflows/durable-wait-handoff.yml) retains model traces and process-local test results. The [provider job](../../../.github/workflows/ci.yml) runs the shared cases in the SQL projects. The disposition assertions characterize the current providers. Revisit those expectations alongside the model if the outbox contract gains attempt checks.
