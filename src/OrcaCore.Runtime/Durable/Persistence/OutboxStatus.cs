namespace OrcaCore.Runtime.Durable.Persistence;

public enum OutboxStatus
{
    Pending,
    Leased,
    Dispatched,
    Poisoned
}
