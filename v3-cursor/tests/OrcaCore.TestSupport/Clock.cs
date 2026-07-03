using Microsoft.Extensions.Time.Testing;

namespace OrcaCore.TestSupport;

/// <summary>
/// Deterministic clock harness for tests. Wraps <see cref="FakeTimeProvider"/>.
/// </summary>
public sealed class Clock
{
    private readonly FakeTimeProvider _provider;

    public Clock(DateTimeOffset start)
    {
        _provider = new FakeTimeProvider(start);
    }

    public DateTimeOffset Now => _provider.GetUtcNow();

    public TimeProvider Provider => _provider;

    public void Advance(TimeSpan amount) => _provider.Advance(amount);
}
