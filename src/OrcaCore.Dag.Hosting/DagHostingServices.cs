namespace OrcaCore.Dag.Hosting;

internal sealed class DagDefinitionRegistry;

internal sealed class DagCoordinator
{
    public DagCoordinator(DagDefinitionRegistry definitions, int maxConcurrentNodes)
    {
        Definitions = definitions;
        MaxConcurrentNodes = maxConcurrentNodes;
    }

    internal DagDefinitionRegistry Definitions { get; }

    internal int MaxConcurrentNodes { get; }
}
