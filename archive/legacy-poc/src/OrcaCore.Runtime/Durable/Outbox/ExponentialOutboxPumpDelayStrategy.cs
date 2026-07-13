namespace OrcaCore.Runtime.Durable.Outbox;

public sealed class ExponentialOutboxPumpDelayStrategy : IOutboxPumpDelayStrategy
{
    private readonly Random _random;

    public ExponentialOutboxPumpDelayStrategy(
        TimeSpan maxDelay,
        double jitterFactor = 0,
        Random? random = null)
    {
        if (maxDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxDelay));

        if (jitterFactor < 0 || jitterFactor > 1)
            throw new ArgumentOutOfRangeException(nameof(jitterFactor));

        MaxDelay = maxDelay;
        JitterFactor = jitterFactor;
        _random = random ?? new Random();
    }

    public TimeSpan MaxDelay { get; }

    public double JitterFactor { get; }

    public TimeSpan GetDelay(OutboxPumpDelayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ConsecutiveFailures <= 0)
            return context.PollingInterval;

        var multiplier = Math.Pow(2, context.ConsecutiveFailures - 1);
        var delayTicks = Math.Min(
            MaxDelay.Ticks,
            (long)Math.Ceiling(context.PollingInterval.Ticks * multiplier));

        if (JitterFactor <= 0)
            return TimeSpan.FromTicks(delayTicks);

        var minFactor = 1d - JitterFactor;
        var maxFactor = 1d + JitterFactor;
        var jitteredFactor = minFactor + (_random.NextDouble() * (maxFactor - minFactor));
        var jitteredTicks = Math.Max(1L, (long)Math.Ceiling(delayTicks * jitteredFactor));
        return TimeSpan.FromTicks(Math.Min(MaxDelay.Ticks, jitteredTicks));
    }
}
