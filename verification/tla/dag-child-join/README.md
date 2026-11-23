# DAG child join during parent cancellation

A child that cancels independently joins as `Failed / CHILD_CANCELLED`. A child cancelled by its owning DAG joins as `Cancelled` with no failure code. The winning request determines that outcome, even when a different host finalizes the cancellation or handles the join. [Composition requirements](../../../docs/specs/08-requirements-composition.md#cp-023-one-durable-child-instance-per-node-occurrence)

| Recovery schedule | Verified outcome |
|---|---|
| A cancels independently and joins. The worker dies before marking the outbox. The parent then requests cancellation. | The replacement claims the same notice on attempt 2 and acknowledges it. The original join remains the only join. |
| A cancels independently. The parent requests cancellation before any join. The worker dies. | The replacement handles the first delivery on attempt 1, records `Failed / CHILD_CANCELLED` and acknowledges it. |

In both PostgreSQL schedules, B keeps its admission, the parent remains in `CancellationRequested`, and the journal has no integrity fault. At the pre-repair revision, the same process tests respectively poisoned a valid notice and acknowledged the wrong classification.

## Durable evidence and compatibility

The child journal records the winning request in `WorkflowCancellationRequestedEvent.CommandId`. The facade and the owning DAG generate different UUIDv8 identities for the same instance. The hash recipe and golden vectors are part of the persisted format. A losing caller can help finalize the winner. The finalizer's command ID and the time at which the join reads the parent do not identify the winner. The coordinator validates the request and cancelled terminal event in child-stream order against the admitted child's identity and lineage. Replay also compares the saved state, failure code, output contract, fingerprint and bytes. [Command identity](../../../src/OrcaCore.Engine.Durable/Execution/DurableCancellationIdentity.cs), [child journal reader](../../../src/OrcaCore.Engine.Durable/Dag/DurableDagChildEngine.cs) and [join coordinator](../../../src/OrcaCore.Durable.Hosting/Dag/DurableDagChildCoordinator.cs)

Older writers persisted random UUIDv7 request IDs. A valid existing join retains its saved cancellation outcome. A first join can establish independent cancellation while the fenced parent is still running. Once the barrier has committed, the old record alone cannot establish cause. The worker records an integrity fault, retains the admission and poisons the ambiguous notice for reconciliation. This also applies to requests accepted by an older worker during a rolling upgrade. Use upgraded join workers for the repaired semantics. An already faulted historical run requires separate reconciliation.

## Model and executable checks

| Check | Result |
|---|---|
| Repaired source and independent reference | Each explored all 1,606 reachable states. The oracle tracks caller order. The source reads the persisted winner. |
| Focused source and legacy checks | Both race orderings, legacy first join and legacy replay passed. |
| Five mutations | Reclassifying the first join or replay against the parent, overwriting the winning origin, blindly trusting a replay and appending a second join each violated its named property. |
| Eight reachability checks | All reached their named state, including a fresh read after restart, opposite request winners and rejection of changed output. |

The model permits one process loss, one lost acknowledgement and one admitted sibling. Event ordering and UUID encoding are checked in C#. The in-memory recovery suite includes DAG-caused cancellation, changed output, and legacy ambiguity. The PostgreSQL tests keep the database running, kill and reap the worker at a committed boundary, and launch a fresh process. The replacement reads the persisted state and uses real lease expiry. SQL Server process recovery and database-server failure are outside this qualification. [TLA+ module](DagChildJoin.tla), [recovery tests](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.JoinRace.cs) and [process restart tests](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.PostgreSqlRestart.cs)

Run from the repository root with PowerShell. TLC uses the private JRE. The explicit process tests require Docker with Linux containers.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/dag-child-join -ModuleName DagChildJoin
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj -c Release --filter "FullyQualifiedName~DagChildRecoveryTests"
dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method OrcaCore.Hosting.Tests.DagChildRecoveryTests.PostgreSqlProcessRestart -showLiveOutput
```
