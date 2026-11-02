namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunGraphV1(
    DagRunBindingV1 Definition,
    string RunInputContractKey,
    IReadOnlyList<DagRunNodeV1> Nodes)
{
    private IReadOnlyList<DagRunNodeV1> nodes = DagRunValidation.GraphNodes(Definition, RunInputContractKey, Nodes);

    public IReadOnlyList<DagRunNodeV1> Nodes
    {
        get => nodes;
        init => nodes = DagRunValidation.GraphNodes(Definition, RunInputContractKey, value);
    }
}
