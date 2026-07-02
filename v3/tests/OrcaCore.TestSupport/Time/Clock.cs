using Microsoft.Extensions.Time.Testing;

namespace OrcaCore.TestSupport.Time;

/// <summary>
/// Deterministic clock harness for tests. Wraps a <see cref="FakeTimeProvider"/> started at a
/// known instant so timer/delay behavior is tested by advancing the clock, never by sleeping.
/// </summary>
public sealed class Clock
{
    public Clock(DateTimeOffset? start = null)
    {
        TimeProvider = new FakeTimeProvider(start ?? DateTimeOffset.UtcNow);
    }

    public FakeTimeProvider TimeProvider { get; }

    public DateTimeOffset Now => TimeProvider.GetUtcNow();

    public void Advance(TimeSpan delta) => TimeProvider.Advance(delta);
}
