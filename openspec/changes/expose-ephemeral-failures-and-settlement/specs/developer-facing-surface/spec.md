## MODIFIED Requirements

### Requirement: Completion waits are notification-driven and race-free
`WorkflowInstanceHandle<TOutput>.WaitForOutputAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<TOutput>` containing the fixed-codec-detached output after successful completion or throw `WorkflowOutputUnavailableException` carrying terminal status and failure when the instance terminalizes without output. A same-named `ValueTask<TOutput>` convenience extension on `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` SHALL project through `GetHandleOrThrow()`. `DagRunHandle.WaitForTerminalAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<DagRunSnapshot>` containing the detached terminal snapshot. Durable implementations SHALL use subscribe-then-recheck around the committed notification boundary so completion cannot be lost; these operations SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT cancel the workflow instance or DAG run.

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
- **THEN** the wait observes the committed result exactly once without a polling interval or lost-notification window

#### Scenario: Caller abandons a local wait
- **WHEN** the wait cancellation token is cancelled
- **THEN** only that caller's wait ends and runtime execution remains governed by its separate cancellation API

#### Scenario: A timed-out physical step keeps running
- **WHEN** output waiting reports TimedOut before the noncooperative body and its ownership handoffs finish
- **THEN** the independent settlement wait remains pending and existing output waiting does not silently become a physical join

## ADDED Requirements

### Requirement: Ephemeral settlement observation is a passive handle operation
PackageId/assembly OrcaCore, namespace OrcaCore, SHALL add exactly public
string? WorkflowFailure.ExceptionType { get; } and public ValueTask
WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken cancellationToken
= default). The getter SHALL remain runtime-created with no public constructor or
setter; the method SHALL be nonvirtual on the existing base handle and inherited
unchanged by WorkflowInstanceHandle<TOutput>. No new helper/overload, timeout,
configuration option, result union, public class, package, friend or provider API
SHALL be added. WorkflowOutputUnavailableException SHALL keep its public shape,
using its inherited InnerException for the original reference.

An already-issued ephemeral handle SHALL retain a small settlement receipt,
without retaining instance payloads or exception slots after retirement. The
receipt SHALL prove terminal final physical quiescence, remain valid after
retirement/Completed/owner disposal, and never target a replacement instance.
A pre-cancelled caller token SHALL throw local OperationCanceledException before
mode/proof checks. Otherwise durable handles SHALL throw NotSupportedException
without provider/DI/runtime lookup, in every durable lifecycle state.

#### Scenario: A resultless or resultful consumer observes settlement
- **WHEN** an accepted ephemeral handle's consumer invokes the base settlement method
- **THEN** it observes the same receipt without a cast, output value or additional public service

#### Scenario: A durable consumer calls settlement
- **WHEN** a durable handle receives a valid noncancelled settlement call
- **THEN** NotSupportedException is returned without storage/provider or DI access; a pre-cancelled token instead produces local cancellation
