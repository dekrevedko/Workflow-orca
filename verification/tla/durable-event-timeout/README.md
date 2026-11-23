# Durable event versus timeout on a reused key

After E1 matches A, A's claimed timeout can arrive while B waits on the same key. Its command still carries A's timer ID. Processing it produces a no-op, and completing the claim addresses that timer alone. An identical E1 redelivery returns `Duplicate`. If no fresh event is pending, B and its timer remain active and the journal stays unchanged.

A duplicate call can advance B when a different eligible event is already pending. Direct ingress checks the retained fingerprint, then scans the target's inbox for a matching Received envelope. If that envelope is E2, the call returns `Duplicate` for E1 and the journal records **A/E1, B/E2**. The duplicate acknowledgement describes ownership of the submitted envelope. The matched fact identifies the event that actually advanced the workflow. [Direct ingress](../../../src/OrcaCore.Engine.Durable/Facade/DurableWorkflowEventIngressCore.cs)

## What TLC checks

The model starts with A waiting and its timer armed. A and B are successive structural waits in one root workflow with the same event contract version and correlation. They have distinct wait and timer IDs. Receiving an event, matching it, publishing B, claiming a timer and processing the saved command are separate transitions. Matching an event applies its inbox record and cancels its logical timer in the same commit. A timer win records the fired timer and cancels its owning wait. [Model](EventTimeout.tla) and [scope](proposal.md)

| Check | Evidence |
|---|---|
| Single winner and correct occurrence | Each wait has at most one recorded winner. Each event is used at most once. Each winning timer belongs to the wait it resolves. |
| Preservation while B waits | Processing TA, or redelivering E1 with no E2 pending, preserves the active wait, logical timer, inbox and winner history on that transition. |
| Replay when E2 is pending | Replay itself must record B/E2 and apply E2. A subsequent independent delivery cannot satisfy the replay witness. |
| Reachability | Six witnesses cover both stale orders with B still active, A's timeout beating a received E1, a later E2 win, B's own timeout, and the replay-triggered E2 win. |
| Fault controls | Retargeting the saved timeout produces B/TA. Reusing an applied event produces B/E1. Each control fails its named ownership invariant. |

The safety profile exhausted 70 distinct states. The two controls and six witnesses produced their expected traces. This finite check assumes retained inbox identity and atomic stream commits. It covers direct routing, one saved timer claim and one redelivery, with E2 arriving after B registers. It makes no fairness or eventual-delivery claim. The model combines processing and acknowledgement of a timer claim and ends a timeout path at the winning commit. Provider tests exercise the separate scheduler acknowledgement, driver failure and terminal inbox cleanup.

## Provider replay

The shared test drives the durable runtime through two authored waits. It obtains A's actual due command from the provider scheduler before E1 matches. It also saves a timeout decision at A's stream version, submits that batch after B registers and checks the expected-version conflict. The already-claimed command then goes through a fresh command processor. After each harmless stale operation the test compares the complete journal event-ID sequence, B's projected WaitId, its reloaded active timer and E1's Applied row. After the final winner it checks the terminal projection, the empty active-wait lookup and the absence of active waits and timers in the reloaded aggregate. [Shared provider schedules](../../../tests/OrcaCore.ProviderCertification/DurableEventTimeoutCertification.cs)

| Schedule | Expected observation |
|---|---|
| A's timeout commits while E1 is Received | A's wait is cancelled, the root fails with `WF-WAIT-TIMEOUT`, the unapplied direct E1 is poisoned at termination, and B never registers. |
| A's late timeout, then E1 replay | B remains active after both operations. A new E2 matches B, cancels its timer and completes the workflow. |
| E1 replay, then A's late timeout | B remains active after both operations. Only B's timer is returned at the next due claim. Firing it fails the root with the typed timeout code. |
| A's late timeout, then E1 replay with E2 pending | The test stages E2 at the provider acceptance boundary while B is active. The E1 call returns Duplicate, consumes E2 for B and completes the workflow. Both inbox rows end Applied. |

The replay uses a controlled clock, direct ingress, provider persistence and the same process/complete calls as the hosted timer worker. Staging a Received record pauses between durable acceptance and inline delivery. These schedules exercise specific serializations. Broker transport, arbitrary database failures, retention expiry and timer lease recovery are outside this check. [Timer worker](../../../src/OrcaCore.Durable.Hosting/Services/OrcaCoreTimerHostedService.cs), [winner arbitration](../../../src/OrcaCore.Engine.Durable/Aggregates/DurableWaitTimerCommandHandler.cs) and [atomic provider batch](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommitMaterializer.cs)

All four schedules passed locally in Release on each of the in-memory, PostgreSQL 17 and SQL Server 2022 providers (12 cases). Seven existing checks also passed across durable callback recovery and the process-local expected-version, duplicate-inbox and stale-route fixtures. The older structural timeout test in `DurableDeadlineExecutionTests` is excluded from its test project. The typed-timeout results above come from the new executable schedules.

Run from the repository root with the private JRE. The SQL test projects require Docker.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-event-timeout -ModuleName EventTimeout
foreach ($project in 'OrcaCore.ProviderCertification','OrcaCore.Providers.PostgreSql.Tests','OrcaCore.Providers.SqlServer.Tests') {
    dotnet test "tests/$project/$project.csproj" -c Release --filter FullyQualifiedName~DurableEventTimeout_ReusedKey
}
```
