## MODIFIED Requirements

### Requirement: Completion waits are notification-driven and race-free
Verification SHALL prove `WorkflowInstanceHandle<TOutput>.WaitForOutputAsync`, the `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` helper of the same name, and `DagRunHandle.WaitForTerminalAsync` observe completion without missing a registration race. Workflow output subscribes before its first authoritative output read. A pending DAG wait subscribes at the observed version and rechecks; an initial terminal or integrity-fault result requires no subscription. Ephemeral output and DAG terminal waits SHALL be verified without periodic reads. Durable workflow output verification SHALL prove the one-second refresh bound under a controlled clock after a pending read completes, including remote terminal state and suppressed hints. Terminal state committed before, during, or after notification registration SHALL be observed without a missed wakeup. Caller cancellation SHALL cancel only the local wait and SHALL NOT mutate workflow, DAG-run, or child cancellation state.

#### Scenario: Terminal commit races output and DAG wait registration
- **WHEN** terminal output or a terminal DAG snapshot commits at every register/recheck interleaving
- **THEN** each wait completes once from authoritative committed state; if the initial read sees the result or a local hint is delivered, no polling-clock advance is required

#### Scenario: Caller cancels a completion wait
- **WHEN** a caller token wins while the workflow or DAG remains nonterminal
- **THEN** the waiting call cancels locally and no runtime cancellation command or state transition is emitted

#### Scenario: Durable refresh cost and ownership
- **WHEN** independent runtime instances share a durable store, multiple callers wait, hints repeat, or a slow pending read spans timer intervals
- **THEN** controlled-clock verification counts the reads, verifies the one-second no-hint cadence per pending caller, coalesces queued signals and observes at most one outstanding read per waiter

#### Scenario: Durable waiter cleanup
- **WHEN** a wait observes remote success, terminal-without-output, deletion, cancellation, shutdown, owner disposal or a provider exception
- **THEN** verification checks the corresponding result or error and proves the waiter releases its timer and subscription without changing workflow state
