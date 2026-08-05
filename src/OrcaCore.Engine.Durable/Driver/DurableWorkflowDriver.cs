using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

/// <summary>
/// Drives one instance's advancement segments through the interpreter (DR-001 host side,
/// in-process form). Loads committed facts and the persisted envelope, binds the definition
/// version recorded at start (DR-016), and runs segments until the instance suspends, ends,
/// or parks. Restart-safe scheduling (continuation pump) arrives with DR-P2; the facade
/// invokes this driver on every local trigger.
/// </summary>
internal sealed class DurableWorkflowDriver(
    DurableCommandProcessor processor,
    DurableDriverCatalog catalog,
    JsonWorkflowPayloadSerializer serializer,
    TimeProvider timeProvider,
    DurableDriverBudget? budget = null,
    IDurableDriverObserver? observer = null)
{
    private const int MaxConflictRetries = 5;
    private readonly DurableDriverBudget budget = budget ?? DurableDriverBudget.Default;
    private readonly DurableAggregateLoader aggregateLoader = new(processor.EventStore);
    private readonly IDurableDriverObserver observer = observer ?? NullDurableDriverObserver.Instance;

    internal Task<DurableSegmentResult> DriveAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return DriveAsync(instanceId, DurableDriveMode.Opportunistic, cancellationToken);
    }

    internal async Task<DurableSegmentResult> DriveAsync(
        InstanceId instanceId,
        DurableDriveMode mode,
        CancellationToken cancellationToken)
    {
        var conflictRetries = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checkpointOption = await processor.EventStore
                .LoadCheckpointAsync(instanceId, cancellationToken)
                .ConfigureAwait(false);
            var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (aggregate.Status is null)
            {
                return new DurableSegmentResult(
                    DurableSegmentOutcome.Suspended,
                    "Instance has no committed start fact.");
            }

            if (aggregate.IsTerminal)
            {
                return DurableSegmentResult.Terminal;
            }

            if (aggregate.Status == WorkflowStatus.CancellationRequested)
            {
                if (processor.HasRunningStep(instanceId))
                {
                    // The persisted request owns the logical race, but cooperative user code
                    // still has the physical step. Keep the continuation claim retryable.
                    return new DurableSegmentResult(
                        DurableSegmentOutcome.BudgetExhausted,
                        "Cooperative cancellation is waiting for an active step to return.",
                        CommittedProgress: false);
                }

                var cancelled = await processor.FinalizeCancellationAsync(
                    instanceId,
                    timeProvider.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                return cancelled.Outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.NoOp
                    ? DurableSegmentResult.Terminal
                    : new DurableSegmentResult(
                        DurableSegmentOutcome.Conflict,
                        cancelled.Message ?? "Cancellation finalization conflicted.");
            }

            if (aggregate.ParkReason is not null)
            {
                return new DurableSegmentResult(DurableSegmentOutcome.Parked, aggregate.ErrorSummary);
            }

            var executor = ResolveExecutor(aggregate);
            if (executor is null)
            {
                // Opportunistic drives (start-or-get, event delivery on kernel-level flows) skip
                // instances whose definition is not driver-registered — the kernel command surface
                // stays usable directly. Required advancement (a claimed continuation) parks
                // instead, per DR-016/DR-AC-009 — but only instances the driver demonstrably
                // owns (an envelope checkpoint exists). Instances advanced purely through kernel
                // commands also produce continuation records; parking those would break direct
                // kernel usage, so their claims resolve as no-ops.
                var driverOwned = checkpointOption.HasValue &&
                    checkpointOption.Value.ContentType == DurableExecutionEnvelopeV2.ContentType;
                if (mode == DurableDriveMode.Opportunistic || !driverOwned)
                {
                    return new DurableSegmentResult(
                        DurableSegmentOutcome.Suspended,
                        VersionBindingSummary(aggregate));
                }

                await ParkVersionBindingAsync(aggregate, instanceId, checkpointOption, cancellationToken)
                    .ConfigureAwait(false);
                return new DurableSegmentResult(
                    DurableSegmentOutcome.Parked,
                    VersionBindingSummary(aggregate));
            }

            DurableExecutionEnvelopeV2? fiberEnvelope = null;
            if (checkpointOption.HasValue)
            {
                var checkpoint = checkpointOption.Value;
                if (checkpoint.ContentType == DurableExecutionEnvelopeV2.ContentType)
                {
                    fiberEnvelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
                }
                else
                {
                    // DR-012/DR-AC-018: a checkpoint without a readable position envelope is
                    // never resumed under a guessed position.
                    var summary = $"Checkpoint content type '{checkpoint.ContentType}' does not carry a durable " +
                                  "execution-position envelope; reset the development instance or use operator " +
                                  "intervention. Automatic compatibility recovery is not supported.";
                    await ParkAsync(
                        instanceId,
                        DurableParkReason.RuntimeStateVersion,
                        summary,
                        checkpoint.StreamVersion,
                        aggregate.StreamVersion,
                        cancellationToken).ConfigureAwait(false);
                    return new DurableSegmentResult(DurableSegmentOutcome.Parked, summary);
                }
            }

            var context = new DurableDriverContext(
                instanceId,
                aggregate,
                fiberEnvelope,
                processor,
                serializer,
                timeProvider,
                budget);
            var segmentElapsed = Stopwatch.StartNew();
            var result = await executor.RunSegmentAsync(context, cancellationToken).ConfigureAwait(false);
            segmentElapsed.Stop();
            await ObserveSegmentAsync(aggregate, result, segmentElapsed.Elapsed, cancellationToken)
                .ConfigureAwait(false);
            switch (result.Outcome)
            {
                case DurableSegmentOutcome.Yielded:
                    conflictRetries = 0;
                    continue;
                case DurableSegmentOutcome.PolicyBoundary or DurableSegmentOutcome.ContinuedAsNew
                    when mode == DurableDriveMode.Opportunistic:
                    conflictRetries = 0;
                    continue;
                case DurableSegmentOutcome.Conflict when conflictRetries < MaxConflictRetries:
                    conflictRetries++;
                    continue;
                default:
                    // BudgetExhausted returns to the caller (DR-051/DR-AC-017): progress is
                    // committed, the last commit carried a fresh continuation record, and the
                    // worker turn is released so other instances advance fairly.
                    return result;
            }
        }
    }

    private async ValueTask ObserveSegmentAsync(
        DurableWorkflowAggregate aggregate,
        DurableSegmentResult result,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (aggregate.DefinitionId is not { } definitionId ||
            aggregate.DefinitionVersion is not { } definitionVersion)
        {
            return;
        }

        try
        {
            await observer
                .OnSegmentCompletedAsync(
                    new DurableDriverSegmentObservation(
                        definitionId,
                        definitionVersion,
                        result.Outcome.ToString(),
                        duration),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Driver observations are diagnostics and must not alter workflow outcomes.
        }
    }

    internal async Task<DurableCommandResult> RearmAsync(
        InstanceId instanceId,
        DurableRearmRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (aggregate.StreamVersion != request.ExpectedStreamVersion)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.Conflict,
                $"Expected stream version {request.ExpectedStreamVersion}, but found {aggregate.StreamVersion}.",
                aggregate.StreamVersion);
        }

        if (aggregate.ParkReason is not { } reason)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.NoOp,
                "Only a parked workflow instance can be re-armed.",
                aggregate.StreamVersion);
        }

        var preconditionFailure = await ValidateRearmPreconditionAsync(
            aggregate,
            reason,
            request,
            cancellationToken).ConfigureAwait(false);
        if (preconditionFailure is not null)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.NoOp,
                preconditionFailure,
                aggregate.StreamVersion);
        }

        return await processor
            .ProcessAsync(
                new DurableUnparkCommand(
                    CommandId.New(),
                    instanceId,
                    timeProvider.GetUtcNow(),
                    request.ExpectedStreamVersion),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private IDurableDriverExecutor? ResolveExecutor(DurableWorkflowAggregate aggregate)
    {
        return aggregate.DefinitionId is { } definitionId && aggregate.DefinitionVersion is { } definitionVersion
            ? catalog.Resolve(definitionId, definitionVersion)
            : null;
    }

    private async Task<string?> ValidateRearmPreconditionAsync(
        DurableWorkflowAggregate aggregate,
        DurableParkReason reason,
        DurableRearmRequest request,
        CancellationToken cancellationToken)
    {
        switch (reason)
        {
            case DurableParkReason.VersionBinding:
                return ResolveExecutor(aggregate) is null
                    ? "The definition version bound at start is still unavailable or incompatible."
                    : null;
            case DurableParkReason.RuntimeStateVersion:
            {
                var checkpoint = await processor.EventStore
                    .LoadCheckpointAsync(aggregate.InstanceId, cancellationToken)
                    .ConfigureAwait(false);
                if (!checkpoint.HasValue ||
                    checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
                {
                    return "The checkpoint has not been migrated to durable execution envelope format 2.";
                }

                try
                {
                    var version = DurableExecutionEnvelopeV2
                        .Deserialize(checkpoint.Value.Payload)
                        .EnvelopeVersion;
                    return version == DurableExecutionEnvelopeV2.CurrentVersion
                        ? null
                        : $"Checkpoint envelope version {version} is not supported.";
                }
                catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
                {
                    return $"The migrated checkpoint envelope is not readable: {exception.Message}";
                }
            }
            case DurableParkReason.Poison:
                return request.AcknowledgePoison
                    ? null
                    : "Poison re-arm requires explicit operator acknowledgement.";
            default:
                return $"Park reason '{reason}' is not re-armable by this runtime.";
        }
    }

    private async Task ParkVersionBindingAsync(
        DurableWorkflowAggregate aggregate,
        InstanceId instanceId,
        Abstractions.Primitives.Option<CheckpointWrite> checkpointOption,
        CancellationToken cancellationToken)
    {
        await ParkAsync(
            instanceId,
            DurableParkReason.VersionBinding,
            VersionBindingSummary(aggregate),
            checkpointOption.HasValue ? checkpointOption.Value.StreamVersion : null,
            aggregate.StreamVersion,
            cancellationToken).ConfigureAwait(false);
    }

    private static string VersionBindingSummary(DurableWorkflowAggregate aggregate)
    {
        return $"Definition '{aggregate.DefinitionId}' version '{aggregate.DefinitionVersion}' bound at " +
            "start is not registered on this host (DU-040); register the version, then explicitly re-arm the instance.";
    }

    private async Task ParkAsync(
        InstanceId instanceId,
        DurableParkReason reason,
        string errorSummary,
        StreamVersion? positionStreamVersion,
        StreamVersion expectedStreamVersion,
        CancellationToken cancellationToken)
    {
        await processor
            .ProcessAsync(
                new DurableParkCommand(
                    CommandId.New(),
                    instanceId,
                    timeProvider.GetUtcNow(),
                    reason,
                    errorSummary,
                    FailedAttemptCount: 1,
                    positionStreamVersion)
                {
                    ExpectedStreamVersion = expectedStreamVersion
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
