using OrcaCore.Abstractions.Providers;

namespace OrcaCore.TestSupport.Providers;

public sealed class FakeMessageDispatcher(params DispatchResult[] results) : IMessageDispatcher
{
    private readonly Queue<DispatchResult> results = new(results);
    private readonly List<OutboxWrite> dispatched = [];

    public IReadOnlyList<OutboxWrite> Dispatched => dispatched;

    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        dispatched.Add(record);
        return Task.FromResult(results.Count > 0
            ? results.Dequeue()
            : DispatchResult.Success);
    }
}
