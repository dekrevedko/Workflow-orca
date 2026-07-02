namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Base exception type for OrcaCore operational and authoring failures.
/// </summary>
public class OrcaCoreException : Exception
{
    /// <summary>
    /// Initializes a new OrcaCore exception with a caller-actionable message.
    /// </summary>
    public OrcaCoreException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new OrcaCore exception with a caller-actionable message and inner cause.
    /// </summary>
    public OrcaCoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
