# TLA+ protocol verification

Use TLA+ when a requirement depends on the order of operations across callers, locks, queues, or retries. Start with a question that can be answered by exploring a small finite model. For example, can a waiter start a different instance after the first caller has already published its result?

| Protocol | Question | Local evidence |
|---|---|---|
| [Ephemeral starts](start-idempotency/README.md) | Who can own a start key, and which result can waiting callers observe? | Complete bounded safety and progress checks, fault controls, and executable facade tests |
| [Wait ownership](wait-ownership/README.md) | Which wait owns a correlation key during handover, cancellation, and rollback? | Focused handover checks and regression tests pass. Six broader searches reached their time limit. |
| [Durable wait handoff](durable-wait-handoff/README.md) | Can two workers lose or duplicate a committed resume across a crash? | Bounded safety, conditional recovery and in-memory recovery tests |
| [Durable continuation lease expiry](durable-continuation-lease/README.md) | What can an expired worker change after a replacement claims and dispatches the record? | Fenced dispositions, terminal replay controls and a real replacement pump on in-memory, PostgreSQL and SQL Server providers |

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
