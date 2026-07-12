using System.Text.Json;
using OrcaCore.Abstractions.Errors;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Creates detached management copies of ephemeral workflow business state.
/// </summary>
public interface IEphemeralStateSnapshotter
{
    /// <summary>
    /// Creates a detached copy of <paramref name="state"/>.
    /// </summary>
    TState Snapshot<TState>(TState state);
}

/// <summary>
/// Uses Microsoft <see cref="JsonSerializer"/> to create detached management state copies.
/// Replace this through <see cref="EphemeralWorkflowEngineOptions.StateSnapshotter"/> when state
/// uses types or constructors that are not supported by System.Text.Json.
/// </summary>
public sealed class SystemTextJsonEphemeralStateSnapshotter : IEphemeralStateSnapshotter
{
    /// <summary>
    /// Gets the shared default snapshotter.
    /// </summary>
    public static SystemTextJsonEphemeralStateSnapshotter Instance { get; } = new();

    /// <inheritdoc />
    public TState Snapshot<TState>(TState state)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(state);
            return JsonSerializer.Deserialize<TState>(serialized)
                ?? throw new WorkflowDefinitionException(
                    $"Workflow state type '{typeof(TState).Name}' could not be copied.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new WorkflowDefinitionException(
                $"Workflow state type '{typeof(TState).Name}' cannot be copied by the configured " +
                $"{nameof(IEphemeralStateSnapshotter)}.",
                exception);
        }
    }
}
