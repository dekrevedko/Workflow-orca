## MODIFIED Requirements

### Requirement: Typed committed root state and output are queryable
Both engines SHALL provide fixed-codec-detached root business-state inspection. Resultful definitions SHALL additionally expose `WorkflowOutputResult<TOutput>` as pending, available, or unavailable; resultless definitions SHALL have no synthetic output. `GetStateAsync<TState>` SHALL return the last committed root state and fail with `WorkflowStateTypeMismatchException` for another type. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`, and the start-result helper SHALL expose the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` after projecting the handle through `GetHandleOrThrow()`. Neither mode SHALL return live state, in-flight attempt copies, branch/item-private payloads, or checkpoint/provider DTOs as snapshot/state/output values. The explicitly permitted ephemeral wrapper InnerException is a separate application-exception reference, not a value-inspection API.

Workflow output waiting SHALL subscribe before its first authoritative read and complete only from committed state. Ephemeral output waiting SHALL rely on notifications without periodic reads. After each authoritative read that returns Pending, a durable workflow output wait SHALL schedule another authoritative read no later than one second after that read completes, subject to provider and scheduler availability. A local commit hint MAY trigger an earlier read. A waiter SHALL serialize its reads and coalesce accumulated hints or elapsed intervals without catch-up bursts. Caller cancellation, host application stopping or disposal of the owning durable service provider SHALL signal cancellation to an outstanding read and SHALL stop scheduling new reads. Completion, cancellation and read failure SHALL release local subscriptions and timers when the outstanding read returns; a provider that does not honor cancellation can delay return. A missing projection SHALL surface as WorkflowInstanceNotFoundException. Other query/provider failures, including a missing checkpoint after a successful projection read, SHALL propagate to the caller, who may start a new wait. A terminal result without output SHALL throw typed `WorkflowOutputUnavailableException` carrying terminal status and optional `WorkflowFailure`. Caller cancellation SHALL cancel only the local wait and SHALL NOT request workflow cancellation or mutate runtime state.

Only an ephemeral output-unavailable wrapper for one elected business-step
failure MAY carry the original live exception graph in InnerException. That
application-provided graph can itself retain mutable application objects; it is
not a detached query result or a safe disposal/settlement proof. The engine SHALL
not inject live engine state, provider DTOs or DI services into that graph.
WorkflowFailure, including nullable ExceptionType text, SHALL remain detached and
authoritative. Snapshots, state/output values and typed branch/item failure data
SHALL never contain the live exception slot. Durable output-unavailable wrappers
SHALL not expose an original live exception.

#### Scenario: Durable business state is inspected
- **WHEN** a caller requests the registered root state type for a retained instance
- **THEN** management returns a detached value representing the last committed root business state

#### Scenario: Resultful workflow completed
- **WHEN** a caller queries a successful resultful instance through its typed handle
- **THEN** it receives the atomically committed `TOutput` and fixed outcome metadata separately

#### Scenario: Output commits while a caller starts waiting
- **WHEN** successful output commits before, during, or immediately after `WaitForOutputAsync` registers its notification
- **THEN** an authoritative read observes the committed output exactly once; a delivered local hint can complete the wait without advancing the fallback clock

#### Scenario: Output wait is cancelled by its caller
- **WHEN** the caller cancellation token wins while output is still pending
- **THEN** only that local wait is cancelled, while the workflow receives no cancellation request and remains eligible to complete

#### Scenario: Workflow terminates without output
- **WHEN** a resultful workflow reaches a terminal status that has no successful output
- **THEN** `WaitForOutputAsync` throws `WorkflowOutputUnavailableException` with that terminal status and optional committed failure

#### Scenario: Structured branches contain private state
- **WHEN** active branch or item fibers hold payloads of the same or another CLR type
- **THEN** root-state and output queries never select those private payloads

#### Scenario: A snapshot is taken beside an original-exception output wait
- **WHEN** a failed ephemeral instance is inspected and its eligible output wait throws
- **THEN** the snapshot/failure values remain detached and only that wrapper's InnerException holds the original application exception; inspecting it does not prove settlement

#### Scenario: Completion on another workflow host
- **WHEN** a durable output wait reads Pending and a second runtime commits successful output without notifying the waiting runtime
- **THEN** its next bounded recheck returns the detached committed result

#### Scenario: Remote terminal state has no successful output
- **WHEN** a different durable host terminalizes the workflow without output and no local hint is delivered
- **THEN** the next authoritative recheck throws the usual output-unavailable exception with the committed terminal status and optional failure

#### Scenario: A pending durable wait ends
- **WHEN** the caller cancels, its host stops, its service provider is disposed, or its query fails
- **THEN** that wait releases its observation resources, schedules no further reads and sends no workflow command

### Requirement: DAG terminal waiting is notification-driven
`DagRunHandle` SHALL expose `ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default)`. The wait SHALL register a terminal notification before rechecking authoritative DAG state, complete immediately or after notification with one terminal snapshot or with the typed integrity-fault outcome defined below, and SHALL NOT poll. An initial authoritative read MAY resolve a terminal or integrity-fault result before a subscription is needed. When pending, subscribe at the observed version and recheck. The periodic recheck for durable workflow output SHALL NOT be installed on a DAG terminal observer. Caller cancellation SHALL cancel only the local wait and SHALL NOT request DAG-run or child cancellation.

The internal integrity-fault run state is nonterminal and SHALL be neither a public status value nor a terminal result. `DagRunFaultedException`, with stable code `DAG-RUN-FAULTED` and carrying the run id, SHALL mean that an authoritative recheck observed a committed checkpoint in that state; it SHALL NOT mean that a fault was committed at some point during the wait. `WaitForTerminalAsync` SHALL throw it when its first authoritative recheck, or any recheck that follows a committed-change notification, observes the faulted state. The integrity-fault commit SHALL wake registered waiters through the same committed notification boundary as every other run change, and a waiter SHALL always recheck the latest committed checkpoint rather than the version named by the notification. A fault that was committed and authoritatively reconciled before the waiter rechecked SHALL NOT be reported: the waiter SHALL observe the reconciled nonterminal checkpoint and continue waiting, and SHALL NOT inspect committed transitions between its last observed version and the latest checkpoint. The wait SHALL NOT return a snapshot for a run it observed as faulted and SHALL NOT wait or poll for reconciliation; a wait started after authoritative reconciliation SHALL behave as an ordinary wait. `GetSnapshotAsync` SHALL keep returning a detached nonterminal snapshot of a faulted run with a null completion time and the run status that was committed before the fault, derived from the committed checkpoint alone so that it is identical before and after host restart: `CancellationRequested` when any node is `CancellationRequested` or `Cancelled`, otherwise `Running`. That derivation SHALL be exact because only a committed run-cancellation request produces either node state and every nonterminal run carrying such a request retains at least one such node. `RequestCancellationAsync` on a faulted run SHALL return `AlreadyRequested` when the derived status is `CancellationRequested`, and SHALL otherwise throw `DagRunFaultedException` without committing a cancellation request or any other record.

#### Scenario: DAG terminal commit races waiter registration
- **WHEN** the DAG run terminalizes before, during, or immediately after `WaitForTerminalAsync` registers its notification
- **THEN** subscribe-then-recheck returns the committed terminal snapshot without a missed wakeup or polling

#### Scenario: DAG terminal wait is locally cancelled
- **WHEN** the caller cancellation token wins while the DAG remains nonterminal
- **THEN** only that waiting call is cancelled, while DAG admission, running children, and run cancellation state remain unchanged

#### Scenario: DAG run was faulted before the wait began
- **WHEN** `WaitForTerminalAsync` is called for a run whose integrity fault is already committed
- **THEN** it throws `DagRunFaultedException` after the first authoritative recheck, and `GetSnapshotAsync` still returns the nonterminal snapshot with the pre-fault run status

#### Scenario: Integrity fault commits after waiter registration
- **WHEN** an integrity fault commits while a registered waiter is pending and is still the committed state when that waiter rechecks
- **THEN** the committed notification wakes that waiter and it throws `DagRunFaultedException` without a missed wakeup or polling

#### Scenario: Fault and reconciliation both commit before the waiter rechecks
- **WHEN** a registered waiter is held until an integrity fault and its authoritative reconciliation have both committed
- **THEN** its recheck observes the reconciled nonterminal checkpoint, no `DagRunFaultedException` is thrown for the earlier fault, and the wait continues to the eventual terminal snapshot

#### Scenario: Run is faulted before any cancellation request
- **WHEN** a run with no committed cancellation request is faulted and the host restarts
- **THEN** its snapshot reports `Running`, and `RequestCancellationAsync` throws `DagRunFaultedException` and commits nothing

#### Scenario: Run is faulted after a cancellation request
- **WHEN** a run with a committed cancellation request is faulted and the host restarts
- **THEN** its snapshot reports `CancellationRequested`, and `RequestCancellationAsync` returns `AlreadyRequested`

#### Scenario: Faulted run is reconciled
- **WHEN** authoritative reconciliation restores a faulted run and a caller waits again
- **THEN** the new wait behaves as an ordinary notification-driven wait and returns the eventual terminal snapshot
