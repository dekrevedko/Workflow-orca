# Durable step completion and timeout recovery

Two workers can read the same admitted step attempt and prepare different outcomes. Each submits its own observed stream revision. The first successful compare-and-swap (CAS) fixes both the policy outcome and the committed workflow state. A replacement that reads an expired in-flight attempt applies its timeout policy before invoking user code. [DR-019 and DR-AC-024/025](../../../docs/specs/16-requirements-durable-driver.md)

## Model bounds

The initial state contains one committed, in-flight visit outside `UnderLease`, ordinal 1, version 1, one absolute deadline and known initial bytes. Each worker holds a separate snapshot. Their successful bodies produce distinguishable bytes, so overwriting the winner with the other body's result is observable. A physical replay uses the same operation, ordinal and deadline. Worker A can die before writing or lose the acknowledgement of a committed success. Worker B remains available for recovery.

The clock distinguishes the interval before each persisted deadline from expiry. Crossing the boundary makes the timeout selectable and stops new successful body observations for that attempt. A success already selected can submit after expiry. The model abstracts timer delivery and the local task selector. The code separately arbitrates physical completion and the timer callback. [Step execution](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.Helpers.cs) and [winner selector](../../../src/OrcaCore.Engine.Durable/Driver/DurablePolicyWinnerSelector.cs)

Each journal entry represents one winning policy command. With budget left, a timeout retains the previous state and atomically admits the next ordinal with a new deadline and zero backoff. On the last attempt it commits failure. A token-ignoring body can return after the next ordinal succeeds. A late physical return carries no authority to change the committed bytes. The logical revision advances once per winning command and abstracts provider-specific journal event counts. [Admission, retry and completion](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs)

In this model, preparing and committing a zero-backoff retry share one logical timestamp. The driver computes the next absolute deadline when it prepares the command. A delayed SQL append can therefore admit an already-expired retry. The model covers expiry at the next read and abstracts the interval before that append. “Completed” in the model means the step outcome has committed. A real workflow can still have an End transition to drive. [Retry preparation](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs)

## What TLC checked

| Profile | Result |
|---|---|
| Safety1, Safety2 | All 339 and 5,460 reachable states respectively satisfy unique winners, identity and deadline stability, exact budget accounting, and winning-state preservation. |
| Progress1, Progress2 | Under weak fairness for clock advancement and the actions of recovery worker B, the visit settles and every lost success acknowledgement leads to a terminal reread. Progress allows an abandoned body to run forever. |
| ReplayBeforeDeadline, RecoverAtDeadline | After A dies, B can physically replay before the original deadline or prepare timeout on an expired read. |
| TimeoutWinsCAS | A has selected success. B commits timeout first and the store rejects A's stale success. |
| LostSuccessAck | A selects success. B reads the expired attempt. A commits and loses the acknowledgement. The store rejects B's stale timeout, and B rereads A's exact bytes and committed revision. |
| LateBodyAfterRetry | The first attempt times out. The retry succeeds. The first body's subsequent physical return preserves the retry's bytes. |
| SkipCAS, RefreshDeadline, PublishLateBytes, ExtraRetry | TLC detects each mutation through its named invariant. |
| NoClock, NoRecovery | Removing either fairness assumption admits a trace that never settles. |

Reachability profiles deliberately negate the target state. Their expected invariant violations supply traces. Safety uses no fairness assumptions. The scope covers two policy budgets (1 and 2), one logical visit and one optional loss of A. Fixed-delay timers, external effects, leased execution, workflow-level deadlines, provider transactions and real host timing require separate executable checks.

Run from the repository root. The runner writes the checked inputs, per-profile TLC logs, source manifest and results under the ignored `.codex-run` directory.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-step-outcome -ModuleName DurableStepOutcome -CheckLivenessAtEnd
```
