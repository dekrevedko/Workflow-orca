namespace OrcaCore.Abstractions.Messaging;

public interface IMessageDispatcher
{
    Task<Result<DispatchOutcome>> DispatchAsync(
        DispatchMessage message,
        CancellationToken cancellationToken);
}
