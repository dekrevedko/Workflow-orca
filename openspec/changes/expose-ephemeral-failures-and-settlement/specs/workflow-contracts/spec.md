## MODIFIED Requirements

### Requirement: Workflow success projections and output waits are typed
`WorkflowRegistrationResult<TDefinitionHandle>` SHALL be the closed union of `Registered`, `HostIncompatible(DefinitionHostCompatibilityFailure)`, and `Conflict(DefinitionRegistrationConflict)`. `DefinitionHostCompatibilityFailure` SHALL be the closed union of `EngineModeMismatch`, `MissingTransientPools`, `MissingDurableResourcePools`, and `MissingWorkflowEventDispatcher`; each missing-name collection SHALL be defensively copied, distinct, and ordinal-sorted. Validation SHALL perform mode mismatch first, then all statically inspectable host capabilities, then fingerprint conflict, with no registry mutation on failure. Ephemeral registration SHALL inspect all authored transient-pool names; durable registration SHALL inspect static lease requests and require an application dispatcher for authored `Publish`, while selector-created durable names remain runtime validation. `IWorkflowDefinitionRegistry` SHALL expose four typed `GetRequiredHandle(reference)` overloads that resolve an already registered exact mode/identity/version/fingerprint and never register; no match SHALL throw `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` carrying the requested reference's `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` as immutable structured properties. `WorkflowStartResult<TInstanceHandle>` SHALL retain its closed accepted/conflict variants. Both results SHALL expose `GetHandleOrThrow()` returning the typed handle without a cast. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure, registration conflict SHALL throw `WorkflowDefinitionRegistrationConflictException`, and start conflict SHALL throw `WorkflowStartIdempotencyConflictException`; each exception SHALL carry the original closed value. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`. A convenience extension with the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` SHALL first project through `GetHandleOrThrow()`, enabling `await start.WaitForOutputAsync(token)`. Both wait forms SHALL be notification-driven and race-free, SHALL return the fixed-codec-detached output, SHALL never poll, and SHALL treat caller cancellation as cancellation of only the local wait rather than the workflow. If the instance terminalizes without output, they SHALL throw `WorkflowOutputUnavailableException` carrying its terminal status and optional `WorkflowFailure`. Durable waiting SHALL subscribe and recheck around the committed notification boundary so a completion cannot be lost. Resultless handles and resultless start results SHALL expose no output wait.

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
- **THEN** the notification-driven wait completes once with the detached output without polling or losing the completion race

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

### Requirement: Workflow failures carry authored and occurrence provenance
Every runtime-created `WorkflowFailure` SHALL carry non-null `AuthoredLocation` and non-null
`FailureOccurrence`. `AuthoredLocation` SHALL use the existing authored-only grammar and identify
the instruction at which the failure is created. `FailureOccurrence` SHALL be an externally
non-derivable runtime-created abstract record with exactly `Root`, `Branch(AuthoredBranchId)`, and
`Item(int index)` variants; its base constructor SHALL be `private protected`, variant constructors
SHALL be internal, branch identity SHALL be non-null, and item index SHALL be nonnegative.

Provenance SHALL attach when the failure is created rather than at join. A single `WhenAll` failure
SHALL propagate unchanged. A synthesized `SFE-JOIN-FAILED` SHALL carry the owning scope fiber's
occurrence while each ordered cause retains its own provenance. Occurrence variants SHALL retain
record value equality; `WorkflowFailure` SHALL retain reference equality. Detachment SHALL copy
location, occurrence, and causes. `orcacore-json-v1` SHALL round-trip the closed versioned
occurrence discriminator allowlist `root`, `branch`, and `item`.

WorkflowFailure SHALL additionally expose runtime-created string? ExceptionType.
An ephemeral business-step failure SHALL capture Exception.GetType().FullName,
falling back to Name only when FullName is unavailable, as opaque detached text.
A synthetic, legacy or durable-produced failure without captured type SHALL use
null. A join's synthetic parent SHALL use null; its causes SHALL preserve their
individual captured strings. Causes SHALL contain actual failures, never a
synthetic exception-type metadata entry. No Type resolution or Exception
construction/deserialization SHALL be performed from that string.

The fixed codec's existing five-property failure object SHALL remain the canonical
legacy form, with byte-identical encoding when ExceptionType is null. A non-null
type SHALL use the exact version-2 failure form with seven unique properties:
version=2, code, message, authoredLocation, occurrence, exceptionType, causes.
The writer SHALL emit that property order. New readers SHALL accept the exact
legacy five-property form and the exact seven-property version-2 form recursively;
version and exceptionType SHALL be present together, with integer version 2 and a
nonempty/non-whitespace type string. Explicit null/empty/whitespace type in the
version-2 form, duplicate/unknown/missing properties, other versions and malformed
provenance SHALL reject through the existing codec validation boundary. The
root/branch/item occurrence discriminator and codec ID orcacore-json-v1 SHALL not
change. This is an explicit closed failure-subformat amendment, not an open
extension or serializer hook. Legacy readers reject the new form; new-to-old
binary compatibility is not promised. Existing null-type/durable payloads and
all unrelated valid bytes/fingerprints SHALL remain unchanged.

#### Scenario: One ForEach item fails
- **WHEN** root `ForEach.WhenAll` has exactly one failed item
- **THEN** the propagated failure retains the authored instruction location and that item's stable index occurrence without a synthetic join wrapper

#### Scenario: Several branches fail
- **WHEN** root `Parallel.WhenAll` synthesizes `SFE-JOIN-FAILED`
- **THEN** the aggregate records the owning root occurrence and every authored-order cause retains its own branch occurrence

#### Scenario: Consumer matches an occurrence
- **WHEN** application code switches over a `FailureOccurrence`
- **THEN** it can handle the three documented variants and uses a defensive default because C# v1 does not compiler-enforce exhaustiveness for the closed runtime-created hierarchy

#### Scenario: Legacy and typed failures round-trip together
- **WHEN** a failure graph mixes original five-property nodes and version-2 captured-type nodes
- **THEN** new readers preserve every code/message/type/location/occurrence/cause order, write null-type nodes in the original exact form and never serialize an Exception object

#### Scenario: A malformed typed failure is supplied
- **WHEN** a failure object has duplicate or unknown properties, an unsupported version, a missing/blank/null exceptionType or malformed occurrence
- **THEN** the existing malformed-codec boundary rejects it instead of ignoring data or constructing a CLR exception

#### Scenario: A captured type looks like a resolvable CLR type
- **WHEN** a valid detached exceptionType string is read
- **THEN** it remains text; no Type.GetType, reflection resolver, DI lookup, user constructor or exception deserializer runs
