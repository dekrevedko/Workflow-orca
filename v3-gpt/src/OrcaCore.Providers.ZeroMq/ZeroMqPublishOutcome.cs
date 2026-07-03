namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Describes provider-local ZeroMQ send outcomes.
/// </summary>
public enum ZeroMqPublishOutcome
{
    /// <summary>
    /// The peer accepted the message for socket-level send.
    /// </summary>
    Accepted,

    /// <summary>
    /// No peer was available before the configured send timeout.
    /// </summary>
    PeerUnavailable
}
