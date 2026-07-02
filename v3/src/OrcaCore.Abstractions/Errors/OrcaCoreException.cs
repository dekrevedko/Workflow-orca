namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Base type for all exceptions raised by OrcaCore. Every derived exception states, in its
/// message, what the caller should do.
/// </summary>
public class OrcaCoreException : Exception
{
    public OrcaCoreException(string message)
        : base(message)
    {
    }

    public OrcaCoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
