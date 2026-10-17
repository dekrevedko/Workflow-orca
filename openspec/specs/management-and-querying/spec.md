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

### Requirement: Ephemeral terminal retention requires physical settlement
Automatic ephemeral terminal retention SHALL protect every accepted active, waiting, delayed, or cancellation-requested instance and every terminal instance with runtime-owned physical attempts, callbacks, pending commits, continuations, or cleanup still unsettled. The runtime SHALL register per-instance root/branch/item physical ownership before dispatch and remove it on definitive completion, independently of whether throttles or transient pools are configured. Terminal status alone, elapsed time, or a free logical path token SHALL NOT be settlement proof. Retirement SHALL NOT cancel work, release a physical governance slot early, change immutable terminal status, or claim application-spawned external effects stopped.

An unaccepted provisional start SHALL remain runtime-owned until either atomic facade acceptance or definitive abort. Caller cancellation of `StartOrGetAsync` observed after runtime creation and before acceptance, or an exception after `Init` before acceptance, SHALL abort that provisional unit. Before surfacing that cancellation/exception, abort handling SHALL fence the unit through its owning mutation lane to `Terminated`, or preserve an already committed immutable terminal status, using cleanup authority independent of the cancelled caller token. The unit SHALL have no published facade binding, start-key binding, or reachable application handle, SHALL remain outside the accepted terminal-retention budget, and SHALL be released eagerly once every owned physical attempt/callback/commit has settled, even with `TerminalRetention` omitted. If acceptance wins the acceptance/abort race, the start SHALL return `Accepted`; subsequent cancellation of that start token SHALL NOT abort the accepted workflow. The caller-visible abort SHALL retain the original cancellation/exception. No cancellation/exception/not-found result SHALL imply that the workflow never ran or that retrying its unbound key is safe for external effects.

#### Scenario: Timeout fences an uncooperative attempt
- **WHEN** a runtime-owned task remains incomplete after the workflow becomes `TimedOut`, `Cancelled`, or `Terminated`
- **THEN** the runtime instance, facade binding, payload and start binding remain protected beyond age/count limits until physical settlement is established

#### Scenario: Active work is parked
- **WHEN** an instance is waiting on an event, delay, retry, governance admission, or a structured join
- **THEN** automatic terminal retention leaves its runtime state and key bound regardless of current memory pressure or elapsed age

#### Scenario: All owned work eventually settles
- **WHEN** terminal root/branch/item attempts and owned callbacks/commits finish
- **THEN** the published instance becomes eligible through the same lifetime protocol without a public eviction call or a claim about untracked external work

### Requirement: Ephemeral terminal retention retires one complete ownership unit
An enabled policy SHALL retire runtime instance/state/output/failure ownership, facade instance binding, exact start binding, and obsolete instance-owned routing/timer/notification/retention records together, including every entry for the instance in the internal event router's `accepted` event-deduplication map. It SHALL preserve definition registrations and SHALL NOT replace removed records with unbounded retired-ID or key tombstones. The host SHALL stamp monotonic settlement age once after both physical settlement and facade publication, plus a unique increasing settlement ordinal. At publication/settlement, final admitted-operation pin release, and each non-overlapping sweep, it SHALL first retire eligible units at or beyond maximum age, then retire eligible units by oldest settlement ordinal until their host-wide count is at most `MaxRetainedInstances`. The triggering lifetime operation SHALL enforce the count before completion. Reads and key replay SHALL NOT refresh age/order. Age retirement SHALL run during idle hosts on the first sweep at/after expiry, subject to scheduling delay. Active/unsettled and transiently pinned units SHALL be protected in addition to that eligible-terminal budget; after all work and admitted operations settle, owned per-instance payloads and metadata SHALL plateau at the configured retained count. The policy SHALL promise neither a minimum retention duration under count pressure nor a total byte/process-memory ceiling.

#### Scenario: Count pressure spans definitions
- **WHEN** more than the configured count of eligible terminal units exists across several registered definitions
- **THEN** the oldest settlement ordinals retire first and runtime/facade/start/payload records obey the same host-wide bound

#### Scenario: Idle age expiry follows settlement
- **WHEN** monotonic age reaches `MaxAge` with no new application calls
- **THEN** the next scheduled sweep retires the unit, while wall-clock changes and repeated reads/replays never extend its settlement age

#### Scenario: Workflow completes before start publication
- **WHEN** runtime execution completes synchronously before the facade has published its accepted instance/start binding
- **THEN** retention observes one coherent published unit and cannot leave a start binding or handle pointing at already removed runtime data

#### Scenario: A provisional start fails
- **WHEN** a start creates runtime-owned records but fails or is aborted before facade acceptance
- **THEN** the owning lane fences the unaccepted unit to `Terminated` unless it is already immutably terminal, no facade/start binding or handle is published, and the unit is released outside the retained budget immediately after physical settlement even when the policy is disabled

#### Scenario: Start cancellation occurs during the first step
- **WHEN** the start caller cancels its token after `Init` created the provisional runtime instance but before facade acceptance, while the first physical step is running
- **THEN** abort handling uses its own cleanup authority to terminally fence the unpublished unit, preserves the caller's cancellation result, protects the remaining physical work until it returns, and eagerly releases the unit after settlement instead of leaving an unreachable `Running` instance

#### Scenario: An exception escapes after Init
- **WHEN** an exception escapes a start after runtime creation but before acceptance
- **THEN** the original exception reaches the caller after the owning-lane terminal fence, existing terminal status is preserved if present, no key/handle is published, and all provisional runtime/router/timer payload records are released after physical settlement

#### Scenario: Accepted publication wins cancellation
- **WHEN** facade acceptance linearizes before the start-token cancellation can abort its provisional unit
- **THEN** the caller receives `Accepted`, its instance/key remain published, and the later start-token cancellation cannot substitute for the instance handle's explicit workflow-cancellation operation

### Requirement: Retired ephemeral handles have deterministic outcomes
Handles SHALL carry identity without extending retained payload lifetime. Operation admission and retirement SHALL share a linearization boundary. A snapshot/state/output query admitted before retirement SHALL pin a coherent unit until it captures a detached result. A pre-existing `WaitForOutputAsync` SHALL subscribe and authoritatively recheck under that protocol, retain an operation pin until detached successful output or terminal failure is captured, and settle without polling or a missed wakeup even if count/age pressure races notification. Completion/error/local caller cancellation SHALL release that pin exactly once; caller cancellation SHALL NOT cancel the workflow. Obtaining a handle alone SHALL NOT grant a pin to later calls. After retirement, `GetInstanceAsync` and every runtime operation on an existing handle SHALL throw existing `WorkflowInstanceNotFoundException`, code `WF-INSTANCE-NOT-FOUND`, carrying that handle's original ID; no replacement instance SHALL be targeted. Previously detached values and completed wait results SHALL remain caller-owned and valid. For a retained instance, existing definition/type mismatch behavior SHALL remain unchanged.

#### Scenario: A stale handle is reused after its key starts a new instance
- **WHEN** a unit has retired and a later start reuses its former key
- **THEN** all calls through the old handle fail with not-found for the old ID while the new handle addresses only the new runtime-minted ID

#### Scenario: Output notification races retirement
- **WHEN** count/age pressure selects a newly terminal instance with already admitted output waiters
- **THEN** each waiter captures its detached output or typed terminal failure before pin release and no waiter hangs or becomes spuriously not-found

#### Scenario: Caller cancels one output wait
- **WHEN** local cancellation wins for one admitted waiter
- **THEN** only its subscription/pin is released and the workflow and other waiters remain unchanged

#### Scenario: Read and retirement interleave
- **WHEN** a query races the retirement visibility boundary
- **THEN** it either completes from a pinned detached value or returns typed not-found, with no partially removed state or live payload reference

#### Scenario: Count pressure retires a result between start return and observation
- **WHEN** `StartOrGetAsync` has returned `Accepted` for a completed unit and enough other units settle to retire it by count before the caller's direct or start-result-helper `WaitForOutputAsync` is admitted
- **THEN** the wait throws `WorkflowInstanceNotFoundException` for the original ID even though that job ran and may have produced output or side effects; replay of its now-unbound key creates a fresh ID with `WasExisting = false` and may repeat side effects, so not-found after acceptance SHALL NOT be interpreted as proof the run never started or as permission for an effect-safe retry

### Requirement: Ephemeral start idempotency expires with atomic retirement
While an ephemeral unit is retained, protected, or pinned, its exact start key SHALL retain the current definition identity/version/fingerprint and fixed-codec input fingerprint binding. Compatible replay SHALL return the same instance with `WasExisting = true`; incompatible replay SHALL return the existing typed conflict and perform no start. Actual retirement SHALL remove the unit and key in the same public visibility transition. Elapsed maximum age alone SHALL NOT unbind a key before retirement. After retirement, the unbound key SHALL accept another valid registered definition/input as a new start with a fresh runtime-minted ID and `WasExisting = false`. Concurrent replay, new start, completion and retirement SHALL serialize at that transition so no bound key points to absent runtime data and no current key admits two new instances. Host-local deduplication after retirement and physical external-effect exactly-once behavior SHALL NOT be promised.

#### Scenario: Duplicate arrives within the retained set
- **WHEN** a retained/protected unit's exact key and input are replayed
- **THEN** the original instance is returned and its retention age/order is unchanged; changed definition/input yields the existing closed conflict

#### Scenario: Former key is reused after retirement
- **WHEN** a valid start uses a key whose old unit was atomically retired
- **THEN** the runtime creates one fresh instance with `WasExisting = false` and old handles remain not-found

#### Scenario: Replay races retirement
- **WHEN** a duplicate start and retirement contend for the same unit
- **THEN** replay either observes the complete retained binding or starts after complete removal; it never reopens missing runtime data or forgets an unsettled binding
