## Purpose

Define the operator-facing query and command surface used to inspect and control OrcaCore workflow instances.

## Requirements

### Requirement: Management API is scope-oriented and fluent
The ordinary management interface SHALL use the exact typed definition and instance handles approved by the matrix. The common `IWorkflowDefinitionRegistry` SHALL expose all resultless/resultful ephemeral/durable registration overloads and return the matching typed handle through its closed `Registered` result or resolve an already registered exact typed reference through `GetRequiredHandle`; engine-mode mismatch or missing statically inspectable host capabilities SHALL return `HostIncompatible` before mutation. Exact-reference lookup SHALL never register and SHALL throw `WorkflowDefinitionNotRegisteredException` when absent or stale. Each definition handle SHALL remain the sole owner of typed `StartOrGetAsync` and `GetInstanceAsync`; each instance handle SHALL expose only detached snapshot/state queries, cancellation request, and termination, with typed output query added only by `WorkflowInstanceHandle<TOutput>`. State inspection SHALL remain separate from committed workflow output inspection.

#### Scenario: Operator targets one workflow instance
- **WHEN** an operator selects a known instance in either execution mode
- **THEN** `GetSnapshotAsync`, `GetStateAsync<TState>`, optional typed `GetOutputAsync`, `RequestCancellationAsync`, and `TerminateAsync` provide the exact typed results and errors

#### Scenario: Application resolves a configured workflow
- **WHEN** runtime code supplies an exact typed reference installed by the host's staged definition batch
- **THEN** `GetRequiredHandle` returns the typed definition handle without registration, a cast, keyed DI, or another workflow client abstraction

#### Scenario: Operator looks for bulk querying
- **WHEN** an operator inspects ordinary v1 handles
- **THEN** engine-wide/definition selections, `Where`, list/count/statistics, and bulk mutation members are absent

### Requirement: Filtering semantics are constrained and translatable
Management filtering SHALL use a constrained, translatable predicate model over queryable runtime metadata rather than arbitrary runtime delegates.

#### Scenario: Durable provider evaluates a filter
- **WHEN** an operator filters instances by status, definition, or timestamps
- **THEN** the filter can be translated consistently across in-memory and durable execution modes

### Requirement: Inspection exposes operationally useful metadata
The management surface SHALL expose active waits, runtime status, timestamps, grouped statistics, and related operational metadata without requiring business payload deserialization alone.

#### Scenario: Operator investigates waiting instances
- **WHEN** an operator queries for waiting workflows
- **THEN** the resulting inspection surface includes wait identity, correlation data, and runtime metadata needed for troubleshooting

### Requirement: Command availability follows execution-mode capability
Shared instance handles SHALL expose only cooperative idempotent cancellation request and immediately fenced termination. `Completed`, `Failed`, `TimedOut`, `Cancelled`, and `Terminated` SHALL remain immutable terminal statuses. Public pause/resume, failed-instance/step retry, archive, purge, broad termination, retention/history, and engine-specific lifecycle commands SHALL be deferred and absent. Application commands SHALL obtain time and protocol identifiers from the owning runtime. Advanced resource administration SHALL remain a separate host-management seam rather than a durable workflow instance method.

#### Scenario: Operator invokes a durable-only command in ephemeral mode
- **WHEN** a caller uses an ephemeral instance handle
- **THEN** durable-only methods are absent rather than callable and predictably rejected

#### Scenario: Durable command is invoked
- **WHEN** an operator requests cancellation or termination of a durable instance
- **THEN** runtime-owned identity/time and the single serialized mutation lane apply, and protected work still follows definite-cleanup/quarantine rules

#### Scenario: Deferred lifecycle command is requested
- **WHEN** a caller looks for pause, resume, retry, archive, purge, history, or broad selection mutation
- **THEN** the public member is absent until a separate lifecycle/authorization/retention amendment is approved

### Requirement: Typed committed root state and output are queryable
Both engines SHALL provide fixed-codec-detached root business-state inspection. Resultful definitions SHALL additionally expose `WorkflowOutputResult<TOutput>` as pending, available, or unavailable; resultless definitions SHALL have no synthetic output. `GetStateAsync<TState>` SHALL return the last committed root state and fail with `WorkflowStateTypeMismatchException` for another type. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`, and the start-result helper SHALL expose the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` after projecting the handle through `GetHandleOrThrow()`. Neither mode SHALL return live state, in-flight attempt copies, branch/item-private payloads, or checkpoint/provider DTOs.

Workflow output waiting SHALL be notification-driven and race-free: register the terminal/output notification before an authoritative state recheck, complete from the committed output if terminality raced registration, and never poll. A terminal result without output SHALL throw typed `WorkflowOutputUnavailableException` carrying terminal status and optional `WorkflowFailure`. Caller cancellation SHALL cancel only the local wait and SHALL NOT request workflow cancellation or mutate runtime state.

#### Scenario: Durable business state is inspected
- **WHEN** a caller requests the registered root state type for a retained instance
- **THEN** management returns a detached value representing the last committed root business state

#### Scenario: Resultful workflow completed
- **WHEN** a caller queries a successful resultful instance through its typed handle
- **THEN** it receives the atomically committed `TOutput` and fixed outcome metadata separately

#### Scenario: Output commits while a caller starts waiting
- **WHEN** successful output commits before, during, or immediately after `WaitForOutputAsync` registers its notification
- **THEN** subscribe-then-recheck observes the committed output exactly once without polling or a missed wakeup

#### Scenario: Output wait is cancelled by its caller
- **WHEN** the caller cancellation token wins while output is still pending
- **THEN** only that local wait is cancelled, while the workflow receives no cancellation request and remains eligible to complete

#### Scenario: Workflow terminates without output
- **WHEN** a resultful workflow reaches a terminal status that has no successful output
- **THEN** `WaitForOutputAsync` throws `WorkflowOutputUnavailableException` with that terminal status and optional committed failure

#### Scenario: Structured branches contain private state
- **WHEN** active branch or item fibers hold payloads of the same or another CLR type
- **THEN** root-state and output queries never select those private payloads

### Requirement: Management construction preserves one mutation lane
Management mutation handles SHALL be created by the owning runtime and SHALL NOT create fallback command processors, clocks, observers, or independent instance lanes.

#### Scenario: Management command races runtime work
- **WHEN** management mutation and workflow advancement target one durable instance concurrently
- **THEN** both use the configured serialization and conflict-handling path

### Requirement: Application wait snapshots expose authored facts
`WorkflowInstanceSnapshot.ActiveWaits` SHALL expose opaque `WaitId`, immutable `AuthoredLocation`, `EventName`, `CorrelationId`, registration time, and optional deadline. It SHALL NOT expose `FiberId`, `ScopeId`, wait sequence, raw park reason, or obligation ownership.

#### Scenario: Application lists active waits
- **WHEN** an operator inspects waits for a workflow with nested branches/items
- **THEN** results identify authored paths and matching data without runtime routing identities or a `WaitLong` kind

### Requirement: Application projection models have one exact declaration
The application tier SHALL define exactly one `WorkflowInstanceSnapshot`, `ActiveWaitSnapshot`, cancellation status, termination status, registration/start result, and typed output-result family matching the normative matrix. Constructors that grant runtime authority SHALL remain internal, and public immutable data construction SHALL grant no management capability.

#### Scenario: Public management surface is inspected
- **WHEN** public baselines and declaration scans run
- **THEN** each approved model appears exactly once and no engine-local query/statistics/destructive-confirmation duplicate remains

### Requirement: Deferred management operations have no placeholder
Public pause/resume, failed-instance or failed-step retry, archive, purge, broad termination, eviction, retained history, selection, list/count/statistics, and lifecycle remediation SHALL be absent from v1 ordinary application handles and SHALL remain documented with re-entry requirements.

#### Scenario: Deferred operation is inspected
- **WHEN** compile, reflection, and package guards inspect application management
- **THEN** no callable member, obsolete alias, empty implementation, or confirmation DTO for a deferred operation exists

### Requirement: Single-instance terminal operations are exact
`RequestCancellationAsync` SHALL be cooperative and idempotent with `Requested`, `AlreadyRequested`, and `AlreadyTerminal`. `TerminateAsync` SHALL fence progression immediately with `Terminated` or `AlreadyTerminal` and SHALL apply definite cleanup or resource quarantine. Neither operation SHALL reopen an immutable terminal state or treat termination as proof external work stopped.

#### Scenario: Single instance is terminated
- **WHEN** a caller invokes terminate on an explicit instance handle
- **THEN** progression is fenced through the owning runtime lane with no redundant confirmation, broad-operation overload, or unsafe lease release

### Requirement: Named outcomes preserve their application type
Completion snapshots and typed output handles SHALL carry optional fixed `WorkflowOutcomeName`. Unnamed successful completion SHALL be absent rather than an empty/default value, and equality SHALL be exact ordinal.

#### Scenario: Operator filters by named outcome
- **WHEN** a caller compares a completed snapshot outcome
- **THEN** the fixed authored value uses exact matching with no case folding, trimming, or dynamic runtime selector

### Requirement: Workflow, wait, and step timeout facts remain distinct
The exact v1 application snapshot SHALL remain unchanged. It SHALL expose authored active-wait deadlines and SHALL represent terminal workflow timeout through `WorkflowInstanceStatus.TimedOut` plus detached `WorkflowFailure`. Running absolute workflow deadline and active attempt ordinal/deadline/outcome SHALL remain runtime/BCL telemetry or internal durable facts, not v1 application snapshot/history fields. Advanced lease diagnostics SHALL expose quarantine separately. No projection SHALL report any elapsed timeout as proof that protected external work stopped.

#### Scenario: Workflow exceeds its deadline during protected work
- **WHEN** the instance terminalizes by `CompleteWithin` while an external obligation remains ambiguous
- **THEN** the application snapshot shows `TimedOut` plus failure while advanced lease diagnostics separately show the capacity-reserving quarantine obligation, without adding running deadline/attempt fields to the application snapshot

### Requirement: Durable lease administration exposes reconciliation and trust state
Advanced host management SHALL expose the following exact pool and lease-discovery contract; these types SHALL remain outside ordinary workflow handles. Status/snapshot/result values belong to `OrcaCore.Runtime.Protocol.ResourceGovernance` in `OrcaCore.Runtime.Protocol`; `IDurableResourcePoolManagement`, `IDurableResourceLeaseRecovery`, and `IDurableResourceLeaseDiagnostics` belong to `OrcaCore.Hosting.ResourceLeases` in `OrcaCore.Durable.Hosting`. The runtime SHALL mint and commit `LeaseProtectionToken` with the queued obligation before capacity admission, while author code receives it only after grant through `StepContext<TState>.ResourceLease`.

```csharp
public enum DurableResourceLeaseObligationStatus
{
    Queued,
    PendingCommit,
    Held,
    ReviewMarked,
    AmbiguousHeld,
    Quarantined,
    Released,
    LeaseLost
}

public sealed record DurableResourceLeaseTicketSnapshot(
    string TicketId,
    ResourcePoolName Pool,
    int Units,
    long ProviderGeneration,
    DateTimeOffset ReviewDeadline,
    bool ReviewMarked);

public sealed class DurableResourceLeaseObligationSnapshot
{
    internal DurableResourceLeaseObligationSnapshot();
    public string ObligationId { get; }
    public InstanceId InstanceId { get; }
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public int Generation { get; }
    public string FiberOccurrence { get; }
    public string ScopeOccurrence { get; }
    public AuthoredLocation AuthoredLocation { get; }
    public LeaseProtectionToken ProtectionToken { get; }
    public DurableResourceLeaseObligationStatus Status { get; }
    public IReadOnlyList<DurableResourceLeaseTicketSnapshot> Tickets { get; }
    public DateTimeOffset? QuarantinedAt { get; }
    public StopConfirmationId? AcceptedConfirmationId { get; }
}

public abstract record DurableResourcePoolResizeResult
{
    private protected DurableResourcePoolResizeResult();

    public sealed record Applied(
        ResourcePoolOperationId OperationId,
        ResourcePoolName Pool,
        int Capacity,
        DurableResourcePoolSnapshot Snapshot)
        : DurableResourcePoolResizeResult;

    public sealed record Conflict(
        ResourcePoolOperationId OperationId,
        ResourcePoolName RecordedPool,
        int RecordedCapacity,
        ResourcePoolName AttemptedPool,
        int AttemptedCapacity)
        : DurableResourcePoolResizeResult;
}

public interface IDurableResourcePoolManagement
{
    ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolSnapshot> GetAsync(
        ResourcePoolName pool,
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolResizeResult> ResizeAsync(
        ResourcePoolName pool,
        int capacity,
        ResourcePoolOperationId operationId,
        CancellationToken cancellationToken = default);
}

public interface IDurableResourceLeaseDiagnostics
{
    IAsyncEnumerable<DurableResourceLeaseObligationSnapshot> EnumerateOutstandingAsync(
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourceLeaseObligationSnapshot?> GetAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default);
}
```

`ListAsync` and `GetAsync` SHALL return `DurableResourcePoolSnapshot` values whose `ConfiguredCapacity` is current capacity after replayed resizes, plus total reserved units, resize debt, queued request count, and oldest review deadline. `DurableResourcePoolDefinition.Capacity` remains immutable creation capacity used for later-host startup agreement and SHALL NOT be compared to or reset from current capacity. `GetAsync` and `ResizeAsync` SHALL throw `ResourcePoolNotConfiguredException` with code `WF-RESOURCE-POOL-NOT-CONFIGURED` for an unknown pool before mutation. `ResizeAsync` SHALL throw `ArgumentOutOfRangeException` for capacity less than one before operation-ID binding or mutation. First valid application of a resize operation SHALL persist and return `DurableResourcePoolResizeResult.Applied`; exact replay of the same operation ID, pool, and capacity SHALL return the originally recorded `Applied`; reuse of that operation ID with another pool or capacity SHALL return `Conflict` containing both recorded and attempted facts and SHALL perform no mutation.

`EnumerateOutstandingAsync` SHALL discover every queued, capacity-reserving, reconciliation-required, or `LeaseLost` obligation, including crash-before-external-label/create cases; it SHALL exclude ordinary released/cancelled history. `GetAsync` SHALL correlate by exact `LeaseProtectionToken` and MAY return a retained confirmed-release tombstone as `Released` with the accepted confirmation ID. Owner occurrence, ticket, and obligation facts SHALL be runtime-created and immutable; ticket collections SHALL reject null entries and be defensively copied. `LeaseProtectionToken` is the public correlation identity, while obligation ID, fiber/scope occurrence, ticket ID, and provider generation preserve the fuller exact identity required by certification. This public advanced interface has no caller-identity or per-call authorization parameter and is a trusted in-process host-management diagnostic seam, not ordinary workflow bulk management. V1 exposes no remote diagnostics endpoint; any host adapter that adds one SHALL separately authorize access, redact protection/ownership facts, and return no partial snapshot on denial.

Trusted stop proof SHALL use the separate `IDurableResourceLeaseRecovery` confirmation interface and its exhaustive typed statuses. Evaluation SHALL use total precedence: an ID bound to another token is `ConfirmationConflict`; the same ID already accepted for this token or a token released by accepted confirmation is `AlreadyConfirmed`; a live `PendingCommit`, `Held`, `ReviewMarked`, or `AmbiguousHeld` owner is `NotConfirmable`; a quarantined token with an unused ID is `Released`; and a normally released, unknown, or purged token is `TokenNotFound`. No ordinary workflow handle, author renewal, force-release API, or deletion/time/terminality shortcut SHALL exist.

#### Scenario: Operator inspects a marked ambiguous obligation
- **WHEN** owner reconciliation cannot prove release safe after a review timestamp
- **THEN** the pool snapshot still includes its reserved units and review deadline while `IDurableResourceLeaseDiagnostics` returns an immutable token-correlated owner/ticket projection outside the ordinary application facade

#### Scenario: Operator discovers an orphan without an external label
- **WHEN** a crash leaves a pending, ambiguous, quarantined, or `LeaseLost` obligation before an integration persisted its external label
- **THEN** `EnumerateOutstandingAsync` still exposes its exact protection token, workflow owner, status, pool/unit tickets, review state, and confirmation state for trusted reconciliation

#### Scenario: Operator requests release without stop proof
- **WHEN** owner resume is fenced but protected work is not causally stopped or end-to-end fenced
- **THEN** the obligation remains capacity-reserving quarantine and no waiter is granted

#### Scenario: Trusted integration confirms stop
- **WHEN** a trusted reconciler calls `IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync` with the exact `LeaseProtectionToken` and a fresh idempotent `StopConfirmationId` after proving protected work terminal, absent, or fenced
- **THEN** compare-and-act returns `Released`, `AlreadyConfirmed`, `NotConfirmable`, `TokenNotFound`, or `ConfirmationConflict` according to the exhaustive confirmation matrix and releases at most once

#### Scenario: Confirmation ID is already bound elsewhere
- **WHEN** a confirmation ID bound to another token is presented for a live, confirmed, normally released, unknown, or quarantined target token
- **THEN** `ConfirmationConflict` wins before target-token state evaluation and no capacity changes

#### Scenario: Downward resize creates debt
- **WHEN** configured capacity falls below reserved units
- **THEN** management reports `max(0, reserved - configured)`, revokes nothing, and reports no new grant until debt is zero and the next request fits

#### Scenario: Resize operation is replayed or conflicts
- **WHEN** one `ResourcePoolOperationId` is repeated with the same pool/capacity or reused with different intent
- **THEN** exact replay returns the originally persisted `Applied`, while changed intent returns `Conflict` with recorded/attempted facts and performs no mutation

#### Scenario: Pool is missing or resize capacity is nonpositive
- **WHEN** management names an unconfigured pool or requests zero/negative capacity
- **THEN** the typed unknown-pool or range exception occurs before operation-ID binding, aggregate mutation, queue change, or provider append

### Requirement: DAG terminal waiting is notification-driven
`DagRunHandle` SHALL expose `ValueTask<DagRunSnapshot> WaitForTerminalAsync(CancellationToken cancellationToken = default)`. The wait SHALL register a terminal notification before rechecking authoritative DAG state, complete immediately or after notification with one terminal snapshot, and SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT request DAG-run or child cancellation.

#### Scenario: DAG terminal commit races waiter registration
- **WHEN** the DAG run terminalizes before, during, or immediately after `WaitForTerminalAsync` registers its notification
- **THEN** subscribe-then-recheck returns the committed terminal snapshot without a missed wakeup or polling

#### Scenario: DAG terminal wait is locally cancelled
- **WHEN** the caller cancellation token wins while the DAG remains nonterminal
- **THEN** only that waiting call is cancelled, while DAG admission, running children, and run cancellation state remain unchanged
