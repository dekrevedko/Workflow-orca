using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Hosting;

/// <summary>
/// Selects the registered workflow payload codec used for new durable payload writes.
/// All registered codecs remain available for reading historical payloads by content type.
/// Serialization plugins register an <c>IWorkflowPayloadCodec</c> and configure this option
/// to select their content type for new writes.
/// </summary>
public sealed class WorkflowPayloadSerializationOptions
{
    /// <summary>
    /// Gets or sets the content type used for new writes.
    /// </summary>
    public string WriteContentType { get; set; } = JsonWorkflowPayloadSerializer.JsonContentType;
}
