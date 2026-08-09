using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Outbox;

internal sealed record OutboxDispatchOutcome(
    DispatchResult Result,
    string? FailureCode = null,
    string? FailureDetail = null);

internal interface IDetailedOutboxMessageDispatcher
{
    Task<OutboxDispatchOutcome> DispatchDetailedAsync(
        OutboxWrite record,
        CancellationToken cancellationToken);
}
