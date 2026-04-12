namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record OutboxLeaseRequest(
    string LeaseOwner,
    int MaxCount,
    TimeSpan LeaseDuration);
