using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Observes durable driver segments and continuation claims without affecting execution.
/// </summary>
public interface IDurableDriverObserver
{
    /// <summary>
    /// Called after one advancement segment reaches an outcome.
    /// </summary>
    ValueTask OnSegmentCompletedAsync(
        DurableDriverSegmentObservation observation,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called when the continuation pump begins processing one durable signal.
    /// </summary>
    ValueTask OnContinuationStartedAsync(
        DurableContinuationObservation observation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Telemetry-safe summary of one driver advancement segment.
/// </summary>
public sealed record DurableDriverSegmentObservation(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string Outcome,
    TimeSpan Duration);

/// <summary>
/// Telemetry-safe summary of one continuation start.
/// </summary>
public sealed record DurableContinuationObservation(
    string ProviderName,
    string Outcome,
    TimeSpan Lag);

internal sealed class NullDurableDriverObserver : IDurableDriverObserver
{
    internal static NullDurableDriverObserver Instance { get; } = new();

    private NullDurableDriverObserver()
    {
    }

    public ValueTask OnSegmentCompletedAsync(
        DurableDriverSegmentObservation observation,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask OnContinuationStartedAsync(
        DurableContinuationObservation observation,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
