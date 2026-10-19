namespace OrcaCore.Hosting;

/// <summary>Bounds eligible terminal instances across one ephemeral host.</summary>
public sealed class EphemeralTerminalRetentionPolicy
{
    private EphemeralTerminalRetentionPolicy(int count, TimeSpan age, TimeSpan interval)
    {
        MaxRetainedInstances = count;
        MaxAge = age;
        SweepInterval = interval;
    }

    public int MaxRetainedInstances { get; }
    public TimeSpan MaxAge { get; }
    public TimeSpan SweepInterval { get; }

    /// <summary>Creates a host policy; count pressure can retire results before their maximum age.</summary>
    public static EphemeralTerminalRetentionPolicy Create(int maxRetainedInstances, TimeSpan maxAge, TimeSpan sweepInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetainedInstances, 1);
        if (maxAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxAge));
        if (sweepInterval <= TimeSpan.Zero || sweepInterval > maxAge || sweepInterval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(sweepInterval));
        return new(maxRetainedInstances, maxAge, sweepInterval);
    }
}
