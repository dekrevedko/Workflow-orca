using Microsoft.Extensions.Time.Testing;

namespace OrcaCore.TestSupport;

/// <summary>
/// Provides deterministic test control over the current UTC time.
/// </summary>
public sealed class Clock
{
    /// <summary>
    /// Initializes a clock at the supplied instant.
    /// </summary>
    public Clock(DateTimeOffset start)
    {
        TimeProvider = new FakeTimeProvider(start);
    }

    /// <summary>
    /// Gets the fake time provider consumed by tested code.
    /// </summary>
    public FakeTimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the current UTC instant.
    /// </summary>
    public DateTimeOffset Now => TimeProvider.GetUtcNow();

    /// <summary>
    /// Advances the clock by the exact duration supplied.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        TimeProvider.Advance(duration);
    }
}
