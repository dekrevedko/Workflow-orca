# Durable continuation lease expiry

A worker retains the dispatch attempt it received when claiming an outbox record. If B reclaims A's record, A's mark, poison or release returns false. B keeps its lease and can dispatch the record. After dispatch, even a retry with B's own matching attempt returns false because the row is terminal. [Model](ContinuationLease.tla) · [Provider contract and migration](../../../docs/durable-driver-lane-host.md#upgrading-hosts-and-providers-for-outbox-claim-fencing)

| A's late call | While B holds attempt 2 | After B dispatches |
|---|---|---|
| Mark retryable | B remains claimed until its own deadline. | The row stays dispatched. |
| Release | B remains claimed until its own deadline. | The row stays dispatched. |

The original diagnostic at commit `29bfdbf6` showed an extra claim after a stale retry. The corrected pump passes the attempt for each record to the provider and counts dispositions that actually applied. The provider checks the attempt and current row state in a single write. Reclaim eligibility still uses the claim request timestamp. The store rejects an append that would reuse a retained ID and reset its attempt. Allocate fresh IDs after retention as well. [Pump](../../../src/OrcaCore.Engine.Durable/Driver/DurableContinuationPump.cs) · [In-memory](../../../src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs) · [PostgreSQL](../../../src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs) · [SQL Server](../../../src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStateEngine.cs)

### Model checks

The model starts with one record and one runnable stream position. A's claim expires once, B reclaims, and A makes one delayed retry or release. A separate action replays a retry from B after its successful dispatch. The model covers one retained record lifetime. Provider tests exercise poison diagnostics, batch atomicity, lease timestamps and grouped pump calls.

| Profiles | Result |
|---|---|
| Current safety and fair settlement | Each exhausted 21 states. B retains its active claim, dispatch requires progress, and at most one workflow append commits. Weak fairness for the enabled steps yields a dispatched record and one commit. |
| Five witnesses | Both late operations reach the active-claim and post-dispatch boundaries. B's own terminal replay also occurs. |
| Attempt comparison removed | A changes B's claimed record to retryable. |
| State comparison removed | B's matching-attempt replay changes its dispatched record to retryable. |
| Attempt and stream-version comparisons removed | A stale disposition admits a third claim and two drivers append at the original position. Both comparisons are disabled to expose this scheduling case. |

### Provider correspondence

Four shared certification schedules claim at second 10, exclude a second worker at 14, and reclaim at expiry at 15. The tests check A's rejected write, B's state and diagnostics, an empty poll inside B's lease, B's successful dispatch and an unchanged terminal state on replay. A collision test checks rollback of a whole append, another reclaim at the original second-20 deadline, and poison detail from the third owner. A real replacement pump settles a group containing attempts 1 and 2. The test applies A's late retry after completion and checks an empty poll and the exact ordered workflow event IDs. [Certification cases](../../../tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.ContinuationLease.cs)

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

The [durable verification workflow](../../../.github/workflows/durable-wait-handoff.yml) retains model traces and process-local test results. The [provider job](../../../.github/workflows/ci.yml) runs the shared cases in the SQL projects.
