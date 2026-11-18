# TLA+ protocol verification

Use TLA+ when a requirement depends on the order of operations across callers, locks, queues, or retries. Start with a question that can be answered by exploring a small finite model. For example, can a waiter start a different instance after the first caller has already published its result?

| Protocol | Question | Local evidence |
|---|---|---|
| [Ephemeral starts](start-idempotency/README.md) | Who can own a start key, and which result can waiting callers observe? | Complete bounded safety and progress checks, fault controls, and executable facade tests |
| [Wait ownership](wait-ownership/README.md) | Which wait owns a correlation key during handover, cancellation, and rollback? | The pilot reproduced WOP-F001 in the model and C#. The normal checks currently fail. |

Run either from the repository root with PowerShell 7 on Windows or Linux x64.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/start-idempotency -ModuleName StartIdempotency
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1
```

The runner invokes the checksum-pinned JRE by its private path under `.codex-run/tla-tools`. Both CI workflows use the same bootstrap. The development dependencies and their official download URLs are in [toolchain.json](toolchain.json).

When changing a modeled protocol, update its transitions and linked tests together. Record where the implementation admits competing operations, the bounds of the model, and any fairness assumptions. Keep the property independent of the transition that should enforce it. Check that an injected defect violates the expected property and reproduce actionable traces against the actual code.

A normal profile passes when TLC completes the search. A witness intentionally violates a named “unreachable” predicate to produce a trace. A timeout leaves the result inconclusive. Each run saves its configuration, command, trace, counts and classifications in the output directory. CI uploads these alongside the C# results.
