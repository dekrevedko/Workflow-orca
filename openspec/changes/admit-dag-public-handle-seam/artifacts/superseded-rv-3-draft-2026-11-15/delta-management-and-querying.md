## MODIFIED Requirements

### Requirement: DAG terminal waiting is notification-driven
`DagRunHandle` SHALL expose `ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default)`. The wait SHALL register a terminal notification before rechecking authoritative DAG state, complete immediately or after notification with one terminal snapshot or with the typed integrity-fault outcome defined below, and SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT request DAG-run or child cancellation.

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
