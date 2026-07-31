namespace OrcaCore;

/// <summary>
/// Presents a runtime-created, read-only view of a codec-detached workflow state value.
/// </summary>
/// <typeparam name="TState">The workflow's private business-state type.</typeparam>
public sealed class ReadOnlyStateSnapshot<TState>
{
    internal ReadOnlyStateSnapshot(TState value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the detached state value captured for the current author callback.
    /// </summary>
    public TState Value { get; }
}
