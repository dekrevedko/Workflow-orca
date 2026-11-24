## MODIFIED Requirements

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

`ListAsync` and `GetAsync` SHALL return `DurableResourcePoolSnapshot` values whose `ConfiguredCapacity` is the current capacity in the latest committed full state, including all accepted resizes, plus total reserved units, resize debt, queued request count, and oldest review deadline. `DurableResourcePoolDefinition.Capacity` remains immutable creation capacity used for later-host startup agreement and SHALL NOT be compared to or reset from current capacity. `GetAsync` and `ResizeAsync` SHALL throw `ResourcePoolNotConfiguredException` with code `WF-RESOURCE-POOL-NOT-CONFIGURED` for an unknown pool before mutation. `ResizeAsync` SHALL throw `ArgumentOutOfRangeException` for capacity less than one before operation-ID binding or mutation. First valid application of a resize operation SHALL persist and return `DurableResourcePoolResizeResult.Applied`; exact replay of the same operation ID, pool, and capacity SHALL return the originally recorded `Applied`; reuse of that operation ID with another pool or capacity SHALL return `Conflict` containing both recorded and attempted facts and SHALL perform no mutation.

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
