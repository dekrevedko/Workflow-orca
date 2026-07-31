using OrcaCore.Engine.Durable.Driver;

namespace OrcaCore.Hosting;

/// <summary>
/// Configures OrcaCore background hosted services.
/// </summary>
internal sealed class DurableHostedServiceOptions
{
    /// <summary>
    /// Gets or sets how often pending durable outbox records are claimed and dispatched.
    /// </summary>
    public TimeSpan OutboxPumpInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum number of outbox records claimed per pump iteration.
    /// </summary>
    public int OutboxPumpBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a durable outbox claim is reserved before another worker may retry it.
    /// </summary>
    public TimeSpan OutboxClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how often pending durable continuation records are claimed and driven
    /// (DR-034). This bounds the recovery latency after a crash between commit and continuation.
    /// </summary>
    public TimeSpan ContinuationPumpInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum number of continuation records claimed per pump iteration.
    /// </summary>
    public int ContinuationPumpBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a continuation claim is reserved before another worker may retry it.
    /// </summary>
    public TimeSpan ContinuationClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long an in-flight continuation batch may keep committing after a stop
    /// request before it is cancelled (DR-033 graceful drain).
    /// </summary>
    public TimeSpan ContinuationDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of instances advanced concurrently by one continuation pump.
    /// </summary>
    public int ContinuationWorkerConcurrency { get; set; } = DurableContinuationPump.DefaultMaxDegreeOfParallelism;

    /// <summary>
    /// Gets or sets the durable consecutive failure threshold before an instance parks as poison.
    /// </summary>
    public int ContinuationMaxDriveAttemptsBeforePark { get; set; } =
        DurableContinuationPump.DefaultMaxDriveAttemptsBeforePark;

    /// <summary>
    /// Gets or sets the initial durable backoff after a continuation advancement failure.
    /// </summary>
    public TimeSpan ContinuationInitialFailureBackoff { get; set; } =
        DurableContinuationPump.DefaultInitialFailureBackoff;

    /// <summary>
    /// Gets or sets the hard command-admission limit for one advancement segment (DR-051).
    /// </summary>
    public int MaxCommandsPerSegment { get; set; } = DurableDriverBudget.Default.MaxCommandsPerSegment;

    /// <summary>
    /// Gets or sets the elapsed admission deadline checked between commands and steps (DR-051).
    /// </summary>
    public TimeSpan MaxSegmentDuration { get; set; } = DurableDriverBudget.Default.MaxSegmentDuration;

    /// <summary>
    /// Gets or sets how often due durable timers are claimed and fired.
    /// </summary>
    public TimeSpan TimerSweepInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum number of due durable timers claimed per sweep.
    /// </summary>
    public int TimerSweepBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets how long a durable timer claim is reserved before another worker may retry it.
    /// </summary>
    public TimeSpan TimerClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how often operational maintenance sweeps run.
    /// </summary>
    public TimeSpan OperationalSweepInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long a hosted service waits before retrying after one transient cycle failure.
    /// </summary>
    public TimeSpan TransientFailureBackoff { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Throws when any option would make a hosted-service loop invalid.
    /// </summary>
    public void Validate()
    {
        if (OutboxPumpInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Outbox pump interval must be positive.");
        }

        if (OutboxPumpBatchSize <= 0)
        {
            throw new InvalidOperationException("Outbox pump batch size must be positive.");
        }

        if (OutboxClaimLeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Outbox claim lease duration must be positive.");
        }

        if (ContinuationPumpInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Continuation pump interval must be positive.");
        }

        if (ContinuationPumpBatchSize <= 0)
        {
            throw new InvalidOperationException("Continuation pump batch size must be positive.");
        }

        if (ContinuationClaimLeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Continuation claim lease duration must be positive.");
        }

        if (ContinuationDrainTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Continuation drain timeout must be positive.");
        }

        if (ContinuationWorkerConcurrency <= 0)
        {
            throw new InvalidOperationException("Continuation worker concurrency must be positive.");
        }

        if (ContinuationMaxDriveAttemptsBeforePark <= 0)
        {
            throw new InvalidOperationException("Continuation poison attempt threshold must be positive.");
        }

        if (ContinuationInitialFailureBackoff <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Continuation failure backoff must be positive.");
        }

        if (MaxCommandsPerSegment <= 0)
        {
            throw new InvalidOperationException("Maximum commands per segment must be positive.");
        }

        if (MaxSegmentDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Maximum segment duration must be positive.");
        }

        if (TimerSweepInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Timer sweep interval must be positive.");
        }

        if (TimerSweepBatchSize <= 0)
        {
            throw new InvalidOperationException("Timer sweep batch size must be positive.");
        }

        if (TimerClaimLeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Timer claim lease duration must be positive.");
        }

        if (OperationalSweepInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Operational sweep interval must be positive.");
        }

        if (TransientFailureBackoff <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Transient failure backoff must be positive.");
        }
    }
}
