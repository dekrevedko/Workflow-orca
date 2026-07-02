namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies the durable event stream for one workflow instance.
/// </summary>
public readonly record struct WorkflowStreamId
{
    /// <summary>
    /// Initializes a workflow stream identifier from an instance identity.
    /// </summary>
    public WorkflowStreamId(InstanceId instanceId)
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// Gets the workflow instance represented by this stream.
    /// </summary>
    public InstanceId InstanceId { get; }

    /// <summary>
    /// Returns the stream name text.
    /// </summary>
    public override string ToString()
    {
        return $"workflow-{InstanceId}";
    }
}
