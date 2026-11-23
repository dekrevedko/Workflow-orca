## MODIFIED Requirements

### Requirement: Completion waits are notification-driven and race-free
`WorkflowInstanceHandle<TOutput>.WaitForOutputAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<TOutput>` containing the fixed-codec-detached output after successful completion or throw `WorkflowOutputUnavailableException` carrying terminal status and failure when the instance terminalizes without output. A same-named `ValueTask<TOutput>` convenience extension on `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` SHALL project through `GetHandleOrThrow()`. `DagRunHandle.WaitForTerminalAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<DagRunSnapshot>` containing the detached terminal snapshot, or throw `DagRunFaultedException` while the run is in the nonterminal integrity-fault state as the `management-and-querying` DAG terminal-wait requirement defines. Workflow output waits SHALL subscribe before their first authoritative output read. A DAG wait MAY return an already visible terminal or integrity-fault outcome on its initial read; if pending, it SHALL subscribe at the observed version and recheck authoritative state. Durable workflow output waits SHALL use local commit hints and the bounded refresh in the management output-wait requirement to observe remote completion. Ephemeral output waits and DAG terminal waits SHALL remain notification-driven without periodic reads. Caller cancellation SHALL cancel only the local wait and SHALL NOT cancel the workflow instance or DAG run.

Logical output/terminal completion SHALL remain separate from physical settlement.
The new WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken) SHALL be
available on resultless and inherited resultful handles and SHALL NOT change any
existing output wait, DAG wait, timeout or cancellation contract. Ephemeral output
waits SHALL expose an eligible original step exception only through the existing
wrapper's InnerException; WorkflowFailure.ExceptionType SHALL remain detached text.
Only the two exact members in this change's approved catalogue SHALL be added.
The normative declaration companion SHALL change in the reviewed canonical and
numbered-document transition. Executable public-API/compile/package baselines
SHALL change with actual source, never during a contract or canonical-only target.

#### Scenario: Completion races wait subscription
- **WHEN** output or DAG terminal state commits immediately before, during, or after wait subscription
- **THEN** the wait observes the committed result exactly once; an already-visible result or a delivered local notification requires no timer advance

#### Scenario: Caller abandons a local wait
- **WHEN** the wait cancellation token is cancelled
- **THEN** only that caller's wait ends and runtime execution remains governed by its separate cancellation API

#### Scenario: A timed-out physical step keeps running
- **WHEN** output waiting reports TimedOut before the noncooperative body and its ownership handoffs finish
- **THEN** the independent settlement wait remains pending and existing output waiting does not silently become a physical join

#### Scenario: DAG wait observes a faulted run
- **WHEN** an authoritative recheck of the DAG wait, whether the first or one following a committed-change notification, observes the integrity-fault state
- **THEN** the wait ends with `DagRunFaultedException` without polling and without returning a snapshot that claims a terminal result, while a fault that was reconciled before the recheck is not reported and the wait continues
