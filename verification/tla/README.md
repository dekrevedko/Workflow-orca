# TLA+ protocol verification

Use TLA+ when a requirement depends on the order of operations across callers, locks, queues, or retries. Start with a question that can be answered by exploring a small finite model. For example, can a waiter start a different instance after the first caller has already published its result?

| Protocol | Question | Local evidence |
|---|---|---|
| [Ephemeral starts](start-idempotency/README.md) | Who can own a start key, and which result can waiting callers observe? | Complete bounded safety and progress checks, fault controls, and executable facade tests |
| [Ephemeral start-key retirement](start-key-retirement/README.md) | Which instance does a caller get if the key is retired and reused before its call returns? | Pin protection, captured replay and conflict results, late cleanup, and ordinary access through an old handle |
| [Wait ownership](wait-ownership/README.md) | Which wait owns a correlation key during handover, cancellation, and rollback? | Both targets passed contention, four key-isolation checks and fair progress at the full pilot bounds. [Results](wait-ownership/full-bound-results.md) |
| [Durable wait handoff](durable-wait-handoff/README.md) | Can two workers lose or duplicate a committed resume across a crash? | Bounded safety, conditional recovery and in-memory recovery tests |
| [Durable continuation lease expiry](durable-continuation-lease/README.md) | What can an expired worker change after a replacement claims and dispatches the record? | Fenced dispositions, terminal replay controls and a real replacement pump on in-memory, PostgreSQL and SQL Server providers |
| [Durable event versus timeout](durable-event-timeout/README.md) | Can an old timeout or redelivery of E1 affect the next wait on the same key? | Occurrence-specific arbitration, retained inbox identity and replay with a fresh pending E2 |
| [DAG child join and parent cancellation](dag-child-join/README.md) | Can later parent cancellation change how a child's terminal fact is first joined or replayed? | Source and reference checks pass. Both restart schedules preserve the independent cancellation and acknowledge one join on PostgreSQL and SQL Server. |
| [Durable resource grant rollback](durable-resource-grant/README.md) | Can a losing workflow append release the winning worker's ticket? | The winner retains its ticket. TLC and both SQL providers exercise adoption, exact compensation, and stale admission after closure. |
| [Resource pool resize](resource-pool-resize/README.md) | Can an upward resize strand the partition FIFO or promote a closed owner? | The resize and eligible grants commit together under the partition and workflow fences. |

Run the checks from the repository root with PowerShell 7 on Windows or Linux x64.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/start-idempotency -ModuleName StartIdempotency
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1 -Profile 'Handover*'
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-wait-handoff -ModuleName DurableWaitHandoff
```

The runner invokes the checksum-pinned JRE by its private path under `.codex-run/tla-tools`. The CI workflows use the same bootstrap. The development dependencies and their official download URLs are in [toolchain.json](toolchain.json).

When changing a modeled protocol, update its transitions and linked tests together. Record where the implementation admits competing operations, the bounds of the model, and any fairness assumptions. Keep the property independent of the transition that should enforce it. Check that an injected defect violates the expected property and reproduce actionable traces against the actual code.

A normal profile passes when TLC completes the search. A witness intentionally violates a named “unreachable” predicate to produce a trace. A timeout leaves the result inconclusive. Each run saves its configuration, command, trace, counts and classifications in the output directory. CI uploads these alongside the C# results.

See the [requirements alignment review and next model candidates](alignment-review.md).
