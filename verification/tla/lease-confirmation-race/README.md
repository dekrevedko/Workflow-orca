# Quarantined lease confirmation race

Two reconcilers present different confirmation IDs for one quarantined lease. Resource governance has one unit held by the owner and one eligible waiter. Its winning transaction releases the owner's ticket, grants the waiter a new ticket, binds the winner's ID and retains a release receipt. The workflow records that ID in a separate version-checked checkpoint.

## Publication and recovery

- `Released` proves that this invocation won the governance transaction. The driver publishes the submitted ID. If another writer publishes the checkpoint first, the original live caller still returns `Released` after its version retry. A new invocation returns `AlreadyConfirmed`.
- `AlreadyConfirmed` can mean that a different ID won. Before writing the checkpoint, the driver rereads the exact token's receipt and publishes its accepted ID. A workflow version conflict forces another read. If the receipt is unavailable, the duplicate path reevaluates retained evidence and token state within the existing bounded retry loop.
- Older checkpoints can contain the losing ID. On confirmation of the original token, a retained winning receipt allows one corrective workflow append. When B's ID is presented for a different token, the driver checks a conflicting workflow entry against that entry's own receipt. A receipt naming A prevents a false conflict. A real governance binding to another token still takes precedence. A workflow-only binding remains evidence when there is no receipt. Diagnostics reflect the stored checkpoint until it is repaired.

[Workflow recovery](../../../src/OrcaCore.Engine.Durable/Driver/DurableResourceLeaseRecovery.cs) · [Governance transaction](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs) · [Confirmation status and diagnostics contract](../../../openspec/specs/management-and-querying/spec.md)

## TLC checks

The source model separates governance receipt and binding, workflow checkpoint version and ID, caller-local observations, and the holder's and waiter's units. Each caller can read before or after the governance commit. Either winner can die after that commit and before its workflow write. The stale loser can also publish the winning receipt while the original winner is still alive.

| Profiles | What they check |
|---|---|
| `Source` | Complete safety and progress search. The workflow agrees with the receipt, only the winning ID is bound, completed calls return the correct status, and there is exactly one transfer per release. A committed receipt eventually reaches the workflow under weakly fair repair. |
| `SourceStale`, `SourceFresh`, `SourceStaleBWinner` | A prechecked loser rereads the winning ID after the kill. A fresh loser reads the existing receipt. Reversing the caller roles preserves the same result. |
| `SourceRetry`, `SourceRecovery`, `SourceCasRetry`, `SourceLiveWinnerCasRetry`, `SourceBWinner` | Reachable schedules cover same-ID restart, independent gap repair, workflow conflicts, the live winner returning `Released` after its peer publishes, and B winning governance first. |
| `HistoricalAccounting`, `HistoricalCrashAgreement`, `HistoricalPersistentSplit` | At the pre-fix baseline, ticket accounting held even when the workflow recorded the wrong ID. The nine-state crash trace retains A in governance and publishes B in the workflow. A later retry can leave that disagreement intact. |
| `DoubleGrant`, `DropUnit` | Injected faults violate the safety invariants for grant count and capacity accounting. |
| `DisabledRecovery` | Disabling the repair worker breaks the progress assumption. TLC finds a release gap that stays open forever. |

The source search exhausted 2,699 distinct states and historical accounting exhausted 2,147. The remaining 13 profiles produce designated counterexamples or reachability traces. The historical identity defect was also reproduced on both SQL providers before the fix at `fbfaf2ec`. [Model](LeaseConfirmationRace.tla) · [Profile manifest](profiles.json)

The model starts with one quarantined token, one eligible waiter, and a retained governance receipt after release. It abstracts the provider's internal retries, process scheduling and storage engine. Progress assumes a weakly fair recovery worker while a receipt awaits publication. Cross-token lookups and repair of an already-split stored checkpoint are exercised by the SQL cases.

## SQL regression schedules

The tests use two durable workflows and separate OS processes with PostgreSQL 17 or SQL Server 2022. The owner terminates, its protected step returns, and the second workflow is queued for the capacity-one pool. In each hard-kill case the controller waits until SQL has committed governance and the owner's workflow checkpoint is still quarantined. It then kills A and waits for process exit.

| Schedule on each provider | Required outcome |
|---|---|
| B prepares before A commits. A is killed. | B's stale governance append conflicts. B obtains `AlreadyConfirmed`, publishes A from the receipt, and leaves B unbound. |
| A is killed before B starts. | B reads A's receipt and repairs the open workflow checkpoint with A. |
| B prepares before A commits. Both stay alive. | B publishes A while A's provider response is held. When A resumes against the stale workflow version, its original call returns `Released`. A later replay returns `AlreadyConfirmed`. |

All six schedules passed locally in Release on PostgreSQL 17 and SQL Server 2022. The four hard-kill cases also passed the separate legacy checkpoint repair probe.

A fresh process verifies governance, workflow and public diagnostics all report `confirmation-a`. It checks both IDs replay as `AlreadyConfirmed` on the original token. For an unknown token, A gives `ConfirmationConflict` and B gives `TokenNotFound`. The verifier checks one governance commit after setup and one distinct generation-2 ticket for the waiter over the committed governance history. Replays preserve the ticket, workflow journal and governance version.

After each genuine hard-kill schedule passes its assertions, a separate compatibility probe seeds an already-released checkpoint with B's ID while retaining A's genuine SQL receipt. A new process checks that B gives `TokenNotFound` on an unknown token even before repair. Confirmation of the original token restores A with one corrective workflow event. Subsequent replays preserve the checkpoint, governance version and waiter ticket. [SQL schedules](../../../tests/OrcaCore.Hosting.Tests/LeaseConfirmationRaceTests.Scenarios.cs) · [Process controller](../../../tests/OrcaCore.Hosting.Tests/LeaseConfirmationRaceTests.ProcessRestart.cs)

Run the profiles from the repository root with the private JRE. The explicit SQL suites require Docker and write the individual xUnit case results.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/lease-confirmation-race -ModuleName LeaseConfirmationRace -CheckLivenessAtEnd
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.LeaseConfirmationRaceTests.PostgreSqlProcessRestart -showLiveOutput -xml postgres.xml
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.LeaseConfirmationRaceTests.SqlServerProcessRestart -showLiveOutput -xml sqlserver.xml
```
