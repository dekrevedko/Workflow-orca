## MODIFIED Requirements

### Requirement: Workflow success projections and output waits are typed
`WorkflowRegistrationResult<TDefinitionHandle>` SHALL be the closed union of `Registered`, `HostIncompatible(DefinitionHostCompatibilityFailure)`, and `Conflict(DefinitionRegistrationConflict)`. `DefinitionHostCompatibilityFailure` SHALL be the closed union of `EngineModeMismatch`, `MissingTransientPools`, `MissingDurableResourcePools`, and `MissingWorkflowEventDispatcher`; each missing-name collection SHALL be defensively copied, distinct, and ordinal-sorted. Validation SHALL perform mode mismatch first, then all statically inspectable host capabilities, then fingerprint conflict, with no registry mutation on failure. Ephemeral registration SHALL inspect all authored transient-pool names; durable registration SHALL inspect static lease requests and require an application dispatcher for authored `Publish`, while selector-created durable names remain runtime validation. `IWorkflowDefinitionRegistry` SHALL expose four typed `GetRequiredHandle(reference)` overloads that resolve an already registered exact mode/identity/version/fingerprint and never register; no match SHALL throw `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` carrying the requested reference's `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` as immutable structured properties. `WorkflowStartResult<TInstanceHandle>` SHALL retain its closed accepted/conflict variants. Both results SHALL expose `GetHandleOrThrow()` returning the typed handle without a cast. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure, registration conflict SHALL throw `WorkflowDefinitionRegistrationConflictException`, and start conflict SHALL throw `WorkflowStartIdempotencyConflictException`; each exception SHALL carry the original closed value. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`. A convenience extension with the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` SHALL first project through `GetHandleOrThrow()`, enabling `await start.WaitForOutputAsync(token)`. Both wait forms SHALL return fixed-codec-detached committed output and treat caller cancellation as cancellation of only the local wait. Ephemeral waiting SHALL remain notification-driven without polling. If the instance terminalizes without output, they SHALL throw `WorkflowOutputUnavailableException` carrying its terminal status and optional `WorkflowFailure`. Durable waiting SHALL subscribe to local commit hints before reading authoritative output and SHALL recheck a pending result on the bounded schedule in the management output-wait requirement, including when another host commits the result. Resultless handles and resultless start results SHALL expose no output wait.

For an ephemeral instance whose committed Failed result originates from one
business step exception, both output-wait forms SHALL throw the ordinary
WorkflowOutputUnavailableException with InnerException referencing that exact
exception object. The detached Failure SHALL remain authoritative; its new
nullable ExceptionType string SHALL retain the captured runtime type name.
The original object SHALL NOT be rethrown, cloned, reconstructed from text or
replaced by a normalization wrapper. Engine-created invocation wrappers MAY be
unwrapped only at their owned invocation boundary; a TargetInvocationException
thrown by user code SHALL itself remain the original object. An explicitly
returned StepResult.Failed Error object SHALL receive the same treatment.
Non-OrcaCore business exceptions SHALL use the existing WF-STEP-UNHANDLED code;
OrcaCore errors SHALL keep their own code. Nonblank messages SHALL be copied
verbatim without appended paths; a blank message SHALL use the stable detached
fallback 'The step failed without an exception message.' while the original
object remains unchanged. A cancelled local output wait SHALL
still throw local cancellation and SHALL NOT publish an exception to the workflow.

Only the final failure elected after retry/policy exhaustion SHALL be exposed.
A sole propagated branch/item step failure SHALL retain its original reference;
a true SFE-JOIN-FAILED aggregate SHALL have no live InnerException and SHALL keep
its ordered detached causes. Timeout/cancellation/termination, lifecycle and
non-step failures SHALL NOT borrow an abandoned attempt's original exception.
Durable-produced failures SHALL keep detached behavior and no live InnerException.
No live exception SHALL enter snapshots, output values, codecs, Core/provider
records, persistence, events or diagnostics. Engine-held original references
SHALL be cleared with the retired instance or earlier when no longer an elected
failure; caller-held exceptions remain caller-owned.

#### Scenario: Registration succeeds on the common path
- **WHEN** a consumer calls `GetHandleOrThrow()` on a registered workflow result
- **THEN** the exact typed definition handle is returned without a downcast or variant pattern match

#### Scenario: Configuration-registered definition is resolved
- **WHEN** a consumer calls `GetRequiredHandle` with an exact typed reference staged and installed at startup
- **THEN** the corresponding handle is returned without registration or keyed service resolution

#### Scenario: Definition does not fit the selected host
- **WHEN** registration observes an engine-mode mismatch or one or more missing statically inspectable pools
- **THEN** it returns `HostIncompatible` before mutation, and `GetHandleOrThrow()` throws `WorkflowDefinitionHostCompatibilityException` carrying the unchanged closed failure

#### Scenario: Start conflicts on the convenience path
- **WHEN** a consumer calls `await start.WaitForOutputAsync(token)` on a conflicting resultful start
- **THEN** `GetHandleOrThrow()` first throws `WorkflowStartIdempotencyConflictException` carrying the original `StartIdempotencyConflict`, and no output subscription is created

#### Scenario: Output becomes available after subscription
- **WHEN** a resultful workflow commits output while `WaitForOutputAsync` is pending
- **THEN** the wait completes once with the detached output, even if the durable commit occurred on another host and no local notification arrives

#### Scenario: Local output wait is cancelled
- **WHEN** the caller cancels the token passed to `WaitForOutputAsync`
- **THEN** only that local wait is cancelled and the workflow receives no cancellation or termination request

#### Scenario: Workflow terminalizes without output
- **WHEN** a resultful instance becomes failed, timed out, cancelled, or terminated before committing output
- **THEN** `WaitForOutputAsync` throws `WorkflowOutputUnavailableException` carrying the terminal status and optional detached failure

#### Scenario: Root step throws an IOException
- **WHEN** an accepted ephemeral root step's IOException wins terminal failure and a direct or start-result output wait observes it
- **THEN** the wrapper's InnerException is that same object; Failure retains WF-STEP-UNHANDLED, its clean message, actual authored location/occurrence and ExceptionType System.IO.IOException

#### Scenario: Final attempt replaces an earlier exception
- **WHEN** two retry attempts fail with distinct objects, including identical messages/types, and the second exhausts policy
- **THEN** output waits expose only the second original; a late earlier attempt cannot overwrite it

#### Scenario: An aggregate has multiple authored causes
- **WHEN** WhenAll aggregates two or more failed branches/items
- **THEN** the aggregate wrapper has no live InnerException; detached causes remain in authored branch/item order and each captured step cause retains its type name

#### Scenario: The durable output wait is unchanged
- **WHEN** a durable resultful workflow fails and its output wait observes unavailability
- **THEN** the typed wrapper has no live InnerException and durable runtime failure projection and existing detached values retain their behavior
