namespace OrcaCore;

/// <summary>Configures one bounded root item fan-out.</summary>
public sealed class ForEachOptions
{
    private ForEachOptions(int maxItems, int? maxConcurrency)
    {
        MaxItems = maxItems;
        MaxConcurrency = maxConcurrency;
    }

    /// <summary>Gets the maximum finite item count.</summary>
    public int MaxItems { get; }

    /// <summary>Gets the optional tighter item-admission ceiling.</summary>
    public int? MaxConcurrency { get; }

    /// <summary>Creates validated finite item options.</summary>
    public static ForEachOptions Create(int maxItems, int? maxConcurrency = null)
    {
        if (maxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), maxItems, "MaxItems must be positive.");
        }

        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency), maxConcurrency, "MaxConcurrency must be positive when supplied.");
        }

        return new ForEachOptions(maxItems, maxConcurrency);
    }
}

/// <summary>Provides one detached item and its stable zero-based index.</summary>
public sealed record ForEachItemInput<TItem>(int Index, TItem Item);

/// <summary>Provides one successful fixed-branch result.</summary>
public sealed record BranchResult<TResult>(AuthoredBranchId BranchId, TResult Result);

/// <summary>Provides one successful dynamic-item result.</summary>
public sealed record ForEachItemResult<TResult>(int Index, TResult Result);

/// <summary>Identifies the runtime-created root, fixed-branch, or dynamic-item failure occurrence.</summary>
public abstract record FailureOccurrence
{
    private protected FailureOccurrence()
    {
    }

    /// <summary>Identifies the root workflow occurrence.</summary>
    public sealed record Root : FailureOccurrence
    {
        internal Root()
        {
        }
    }

    /// <summary>Identifies one authored fixed-branch occurrence.</summary>
    public sealed record Branch : FailureOccurrence
    {
        internal Branch(AuthoredBranchId branchId)
        {
            ArgumentNullException.ThrowIfNull(branchId);
            BranchId = branchId;
        }

        /// <summary>Gets the stable authored branch identity.</summary>
        public AuthoredBranchId BranchId { get; }
    }

    /// <summary>Identifies one selected dynamic-item occurrence.</summary>
    public sealed record Item : FailureOccurrence
    {
        internal Item(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index cannot be negative.");
            }

            Index = index;
        }

        /// <summary>Gets the stable selected item index.</summary>
        public int Index { get; }
    }
}

/// <summary>Provides detached workflow failure data.</summary>
public sealed class WorkflowFailure
{
    internal WorkflowFailure(
        string code,
        string message,
        AuthoredLocation authoredLocation,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(authoredLocation);
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(causes);
        Code = code;
        Message = message;
        AuthoredLocation = authoredLocation;
        Occurrence = occurrence;
        Causes = Array.AsReadOnly(causes.ToArray());
    }

    /// <summary>Gets the stable machine-readable failure code.</summary>
    public string Code { get; }

    /// <summary>Gets the detached diagnostic message.</summary>
    public string Message { get; }

    /// <summary>Gets the canonical authored location that produced this failure.</summary>
    public AuthoredLocation AuthoredLocation { get; }

    /// <summary>Gets the runtime-created root, fixed-branch, or dynamic-item occurrence.</summary>
    public FailureOccurrence Occurrence { get; }

    /// <summary>Gets ordered nested failures.</summary>
    public IReadOnlyList<WorkflowFailure> Causes { get; }
}

/// <summary>Represents success or failure for one fixed branch.</summary>
public abstract record BranchOutcome<TResult>
{
    private protected BranchOutcome(AuthoredBranchId branchId)
    {
        ArgumentNullException.ThrowIfNull(branchId);
        BranchId = branchId;
    }

    /// <summary>Gets the authored branch identity.</summary>
    public AuthoredBranchId BranchId { get; }

    /// <summary>Represents successful branch completion.</summary>
    public sealed record Succeeded : BranchOutcome<TResult>
    {
        internal Succeeded(AuthoredBranchId branchId, TResult result) : base(branchId)
        {
            Result = result;
        }

        /// <summary>Gets the detached branch result.</summary>
        public TResult Result { get; }
    }

    /// <summary>Represents failed branch completion.</summary>
    public sealed record Failed : BranchOutcome<TResult>
    {
        internal Failed(AuthoredBranchId branchId, WorkflowFailure failure) : base(branchId)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        /// <summary>Gets the detached branch failure.</summary>
        public WorkflowFailure Failure { get; }
    }
}

/// <summary>Represents success or failure for one dynamic item.</summary>
public abstract record ForEachItemOutcome<TResult>
{
    private protected ForEachItemOutcome(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Index cannot be negative.");
        Index = index;
    }

    /// <summary>Gets the stable zero-based item index.</summary>
    public int Index { get; }

    /// <summary>Represents successful item completion.</summary>
    public sealed record Succeeded : ForEachItemOutcome<TResult>
    {
        internal Succeeded(int index, TResult result) : base(index)
        {
            Result = result;
        }

        /// <summary>Gets the detached item result.</summary>
        public TResult Result { get; }
    }

    /// <summary>Represents failed item completion.</summary>
    public sealed record Failed : ForEachItemOutcome<TResult>
    {
        internal Failed(int index, WorkflowFailure failure) : base(index)
        {
            ArgumentNullException.ThrowIfNull(failure);
            Failure = failure;
        }

        /// <summary>Gets the detached item failure.</summary>
        public WorkflowFailure Failure { get; }
    }
}

/// <summary>Declares one durable logical-capacity requirement.</summary>
public sealed class ResourceLeaseRequirement
{
    private ResourceLeaseRequirement(ResourcePoolName pool, int units)
    {
        Pool = pool;
        Units = units;
    }

    /// <summary>Gets the durable resource pool.</summary>
    public ResourcePoolName Pool { get; }

    /// <summary>Gets the positive requested capacity.</summary>
    public int Units { get; }

    /// <summary>Creates one validated requirement.</summary>
    public static ResourceLeaseRequirement Require(ResourcePoolName pool, int units = 1)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units), units, "Units must be positive.");
        return new ResourceLeaseRequirement(pool, units);
    }
}

/// <summary>Declares one non-empty, duplicate-free durable capacity request.</summary>
public sealed class ResourceLeaseRequest
{
    private ResourceLeaseRequest(IReadOnlyList<ResourceLeaseRequirement> requirements)
    {
        Requirements = requirements;
    }

    /// <summary>Gets the immutable ordered requirement collection.</summary>
    public IReadOnlyList<ResourceLeaseRequirement> Requirements { get; }

    /// <summary>Creates one validated request.</summary>
    public static ResourceLeaseRequest Create(
        ResourceLeaseRequirement first,
        params ResourceLeaseRequirement[] additional)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(additional);

        var requirements = new[] { first }.Concat(additional).ToArray();
        if (requirements.Any(requirement => requirement is null))
        {
            throw new ArgumentException("Lease requirements cannot contain null.", nameof(additional));
        }

        if (requirements.Select(requirement => requirement.Pool).Distinct().Count() != requirements.Length)
        {
            throw new ArgumentException("Lease requirements cannot repeat a resource pool.", nameof(additional));
        }

        return new ResourceLeaseRequest(Array.AsReadOnly(requirements));
    }
}

/// <summary>Provides the runtime protection identity inside one durable lease scope.</summary>
public sealed class ResourceLeaseExecutionContext
{
    internal ResourceLeaseExecutionContext(LeaseProtectionToken protectionToken)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        ProtectionToken = protectionToken;
    }

    /// <summary>Gets the opaque protection token for the current occurrence.</summary>
    public LeaseProtectionToken ProtectionToken { get; }
}
