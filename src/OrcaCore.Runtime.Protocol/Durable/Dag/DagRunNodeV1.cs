namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunNodeV1(
    int Ordinal,
    string NodeId,
    DagRunBindingV1 Workflow,
    string InputContractKey,
    string? OutputContractKey,
    IReadOnlyList<int> DirectDependencies)
{
    private IReadOnlyList<int> dependencies = Array.AsReadOnly(DirectDependencies.ToArray());

    public IReadOnlyList<int> DirectDependencies
    {
        get => dependencies;
        init => dependencies = Array.AsReadOnly(value.ToArray());
    }
}
