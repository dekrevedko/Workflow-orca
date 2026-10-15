namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies the closed family of a durable aggregate stream.
/// </summary>
public enum DurableAggregateKind
{
    /// <summary>A workflow instance stream.</summary>
    Workflow = 0,

    /// <summary>A DAG run stream, distinct from every workflow instance.</summary>
    DagRun = 1
}
