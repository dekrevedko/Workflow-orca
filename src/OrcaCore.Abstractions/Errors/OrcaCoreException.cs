namespace OrcaCore;

/// <summary>
/// Base exception type for OrcaCore operational and authoring failures.
/// </summary>
public abstract class OrcaCoreException : Exception
{
    /// <summary>
    /// Initializes a new OrcaCore exception with a caller-actionable message.
    /// </summary>
    protected OrcaCoreException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the stable machine-readable failure code.</summary>
    public string Code { get; }
}
