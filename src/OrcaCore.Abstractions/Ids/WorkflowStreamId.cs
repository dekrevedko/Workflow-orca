namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies a workflow instance or DAG run durable aggregate stream.
/// </summary>
public readonly record struct WorkflowStreamId
{
    private readonly InstanceId instanceId;

    /// <summary>
    /// Initializes a workflow stream identifier from an instance identity.
    /// </summary>
    public WorkflowStreamId(InstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        this.instanceId = instanceId;
        AggregateKind = DurableAggregateKind.Workflow;
        DagRunId = null;
    }

    private WorkflowStreamId(Guid runId)
    {
        instanceId = null!;
        AggregateKind = DurableAggregateKind.DagRun;
        DagRunId = runId;
    }

    /// <summary>
    /// Gets the workflow instance represented by this stream.
    /// </summary>
    public InstanceId InstanceId => AggregateKind == DurableAggregateKind.Workflow && instanceId is not null
        ? instanceId
        : throw new InvalidOperationException("The stream does not identify a workflow instance.");

    /// <summary>Gets the aggregate family; equal GUIDs in different families remain distinct.</summary>
    public DurableAggregateKind AggregateKind { get; }

    /// <summary>Gets the DAG run identity, or null for a workflow stream.</summary>
    public Guid? DagRunId { get; }

    /// <summary>Creates a stream for a nonempty DAG run identity.</summary>
    public static WorkflowStreamId ForDagRun(Guid runId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("DAG run identifier cannot be empty.", nameof(runId));
        }

        return new WorkflowStreamId(runId);
    }

    /// <summary>
    /// Returns the stream name text.
    /// </summary>
    public override string ToString()
    {
        return AggregateKind == DurableAggregateKind.DagRun
            ? $"dag-run-{DagRunId:D}"
            : $"workflow-{InstanceId}";
    }
}
