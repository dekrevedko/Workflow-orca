namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunStartKeyV1(string Value)
{
    public string Value { get; init; } = DagRunValidation.StartKey(Value);
}
