namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Serializes one workflow payload representation identified by a stable content type.
/// Multiple codecs may be registered behind an <see cref="IWorkflowPayloadSerializer"/> so
/// historical payloads remain readable when the preferred write representation changes.
/// </summary>
public interface IWorkflowPayloadCodec : IWorkflowPayloadSerializer
{
    /// <summary>
    /// Gets the stable content type written and accepted by this codec.
    /// </summary>
    string ContentType { get; }
}
