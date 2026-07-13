namespace OrcaCore.Runtime.Durable.Outbox;

public interface IOutboxPumpDelayStrategy
{
    TimeSpan GetDelay(OutboxPumpDelayContext context);
}
