# DAG child join during parent cancellation

**The coordinator violates CP-023/024 in both tested orders.** A cancels independently while its parent is running. B is a second admitted child that keeps the parent open. The same terminal child fact should produce one node join as `Failed / CHILD_CANCELLED`, including when the worker retries. [Requirements](../../../docs/specs/08-requirements-composition.md#cp-023-one-durable-child-instance-per-node-occurrence) and [model scope](proposal.md)

| Ordering | Required result | Observed on the in-memory durable host |
|---|---|---|
| A joins, acknowledgement is lost, parent requests cancellation, claim expires | Recognize the existing join and dispatch the reclaimed notice. | The original node remains `Failed / CHILD_CANCELLED`. Attempt 2 faults the parent and poisons the original notice with `DAG_PROTOCOL_INVALID`. |
| A is already terminal, parent requests cancellation, A joins for the first time | Record `Failed / CHILD_CANCELLED` and dispatch the notice. | Attempt 1 dispatches the notice but commits `Cancelled` with an empty failure code. The parent stays `CancellationRequested`. |

Both schedules recorded exactly one join for A and preserved B's admission. In the second case a consumer looking only at successful outbox dispatch would miss the incorrect node history.

## Why the outcomes differ

The child reader returns terminal status and output. It supplies no cancellation origin to the coordinator. For a cancelled child, the first join consults the parent's current state. On redelivery, `MatchesJoined` runs the same classification again and compares it with the committed node. A later parent cancellation thus changes the expected classification of an unchanged child fact. The pump records the resulting integrity fault before poisoning the transport. [Terminal fact](../../../src/OrcaCore.Engine.Durable/Dag/DurableDagChildEngine.cs), [join and replay](../../../src/OrcaCore.Durable.Hosting/Dag/DurableDagChildCoordinator.cs) and [outbox disposition](../../../src/OrcaCore.Durable.Hosting/Dag/DurableDagChildPump.cs)

Validating a duplicate against the saved join addresses the replay boundary. The first join also needs evidence of what caused the child's cancellation. The reference model uses the known cause of the scheduled terminal transition. A production repair would need to define how that evidence survives a restart. The reference does not prescribe a persistence format.

## Model and recovery checks

The model separates child terminality, the parent's cancellation commit, the first join append, acknowledgement loss, reclaim of the same notice, and the worker's replay decision. B remains admitted throughout. A separate integrity control changes the successful child's observed terminal output after its join. The bounded settlement check assumes the delivery worker eventually takes a continuously enabled step after the child is terminal. The model permits one lost acknowledgement. [TLA+ module](DagChildJoin.tla)

| Profiles | Local TLC result |
|---|---|
| `Reference` | All 119 reachable states checked. Causal classification, one join, admission, disposition and conditional settlement passed. |
| `SourceLostAck`, `SourceDelayedFirst` | **Both failed.** The first trace ends with an integrity fault and poisoned notice. The second ends with the wrong acknowledged classification. |
| `RecoveredReplay`, `DelayedFirst`, `DagCausedJoin`, `RejectedConflict` | All four reference witnesses reached the named outcome. |
| `TrustReplay`, `Reappend` | Blindly accepting changed output and appending a second join each violated its named property. |

The C# schedules run against the production handler and pump using the in-memory provider. The lost-ack test calls the handler on the actual first claim and withholds its mark. A later poll at lease expiry reclaims the same ID and payload at attempt 2. In both schedules the child is observably terminal before the parent barrier. A separate worker holds the parent's cancel notices during the checked join. The tests inspect the run journal, A's outcome, B's admission and the final outbox disposition.

The Release recovery run had **12 passes and two contract failures**. All 11 pre-existing cases passed, including the case where DAG cancellation really cancels the child. The added integrity control also passed. After a successful join of output 42, it injected schema-valid bytes for 43 into the terminal checkpoint. Redelivery preserved the original joined nodes, recorded one integrity fault and poisoned the notice. This distinguishes a changed fact from an unchanged fact observed after the parent barrier. [Race regressions and integrity control](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.JoinRace.cs) and [existing recovery controls](../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.cs)

Run from the repository root. Both commands currently return a failure status for the two reproduced discrepancies. The model runner uses the private JRE. The CI workflow retains the TLC traces and TRX even when qualification fails. This run covers deterministic in-memory schedules and simulated acknowledgement loss. SQL storage and an actual host crash remain unqualified.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/dag-child-join -ModuleName DagChildJoin
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj -c Release --filter "FullyQualifiedName~DagChildRecoveryTests"
```
