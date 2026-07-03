namespace OrcaCore.Hosting;

/// <summary>
/// Configures OrcaCore background hosted services.
/// </summary>
public sealed class OrcaCoreHostedServiceOptions
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
