using System.Diagnostics.Metrics;
using AwesomeAssertions;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Diagnostics;

public sealed class DurableOperationalTelemetryTests
{
    private static readonly string[] RequiredLongGauges =
    [
        OrcaCoreMetrics.InstancesActive,
        OrcaCoreMetrics.InstancesStuck,
        OrcaCoreMetrics.WaitsActive,
        OrcaCoreMetrics.OutboxPending,
        OrcaCoreMetrics.StreamEvents,
        OrcaCoreMetrics.CheckpointsCount,
        OrcaCoreMetrics.CheckpointsLag,
        OrcaCoreMetrics.ResourcePoolWaiters,
        OrcaCoreMetrics.ResourcePoolTickets,
        OrcaCoreMetrics.ResourcePoolReservedUnits,
        OrcaCoreMetrics.ResourcePoolOverCapacityDebt,
        OrcaCoreMetrics.ResourcePoolReconciliationDue,
        OrcaCoreMetrics.GovernanceConfiguredLimit,
        OrcaCoreMetrics.GovernanceActiveSlots,
        OrcaCoreMetrics.GovernanceWaitDepth,
        OrcaCoreMetrics.GovernanceCancellations,
        OrcaCoreMetrics.ContinuationPendingCount,
        OrcaCoreMetrics.ExternalOutboxPendingCount,
        OrcaCoreDurableDiagnostics.QuarantinedUnitsInstrumentName,
        OrcaCoreDurableDiagnostics.FencedBodiesRunningInstrumentName
    ];

    private static readonly string[] RequiredDoubleGauges =
    [
        OrcaCoreDurableDiagnostics.OldestQuarantinedAgeInstrumentName
    ];

    private static readonly string[] RequiredCounters =
    [
        OrcaCoreMetrics.CommandsProcessed,
        OrcaCoreMetrics.EventsApplied,
        OrcaCoreMetrics.StepsCompleted,
        OrcaCoreMetrics.StepsFailed,
        OrcaCoreMetrics.OutboxDispatched,
        OrcaCoreMetrics.ResourcePoolReconciliations,
        OrcaCoreMetrics.LifecycleEvents,
        OrcaCoreMetrics.InboxDuplicates,
        OrcaCoreMetrics.DriverPoisonCount,
        OrcaCoreMetrics.DriverRegistrationConflictCount
    ];

    private static readonly string[] RequiredHistograms =
    [
        OrcaCoreMetrics.CommandsDuration,
        OrcaCoreMetrics.StepsDuration,
        OrcaCoreMetrics.ProviderCommitDuration,
        OrcaCoreMetrics.OutboxDispatchDuration,
        OrcaCoreMetrics.WaitsDuration,
        OrcaCoreMetrics.DriverSegmentDuration,
        OrcaCoreMetrics.ContinuationLag
    ];

    private static readonly string[] RequiredInstrumentNames =
        RequiredLongGauges
            .Concat(RequiredDoubleGauges)
            .Concat(RequiredCounters)
            .Concat(RequiredHistograms)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void ActiveListener_ObservesExactlyTheCompleteCanonicalDurableMetricCatalog()
    {
        var published = new Dictionary<string, Instrument>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName)
                {
                    published[instrument.Name] = instrument;
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.Start();
        _ = OrcaCoreDurableDiagnostics.Meter;
        var throttleCoordinator = new DurableStepThrottleCoordinator();

        published.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Should().Equal(RequiredInstrumentNames);
        RequiredLongGauges.Should().OnlyContain(name => published[name] is ObservableGauge<long>);
        RequiredDoubleGauges.Should().OnlyContain(name => published[name] is ObservableGauge<double>);
        RequiredCounters.Should().OnlyContain(name => published[name] is Counter<long>);
        RequiredHistograms.Should().OnlyContain(name => published[name] is Histogram<double>);
        GC.KeepAlive(throttleCoordinator);
    }

    [Fact]
    public async Task ActiveListener_ObservesAuthoritativeGroupedStatisticsWithExactTags()
    {
        var observed = new List<ObservedMeasurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            observed.Add(new ObservedMeasurement(instrument.Name, value, tags.ToArray())));
        listener.Start();
        var definitionId = global::OrcaCore.DefinitionId.Parse("00000000-0000-0000-0000-000000000171");
        var instanceId = global::OrcaCore.InstanceId.Parse("00000000-0000-0000-0000-000000000172");
        var waitEventName = global::OrcaCore.EventName.Create("operator-wait");
        var statistics = new WorkflowOperatorStatistics
        {
            ProviderName = "certified",
            Groups =
            [
                new WorkflowOperatorStatisticsGroup(
                    definitionId,
                    global::OrcaCore.DefinitionVersion.Initial,
                    global::OrcaCore.WorkflowInstanceStatus.Waiting,
                    3)
            ],
            StuckGroups = [new WorkflowOperatorStuckGroup(definitionId, 1)],
            ActiveWaitGroups = [new WorkflowOperatorActiveWaitGroup(definitionId, waitEventName, 2)],
            Pressure = new WorkflowOperationalPressure
            {
                ActiveInstanceCount = 3,
                StuckInstanceCount = 1,
                ActiveWaitCount = 2,
                StreamEventCount = 11,
                CheckpointCount = 4,
                CheckpointLag = 7,
                ContinuationPendingCount = 5,
                ContinuationPoisonedCount = 1,
                ExternalOutboxRetryableCount = 6,
                ExternalOutboxClaimedCount = 2
            }
        };
        var pool = new ResourcePoolSnapshot(
            "operator-pool",
            10,
            5,
            [
                new ResourcePoolTicket(
                    Guid.Parse("00000000-0000-0000-0000-000000000173"),
                    "operator-pool",
                    3,
                    instanceId,
                    "held-ticket",
                    DateTimeOffset.UnixEpoch,
                    null),
                new ResourcePoolTicket(
                    Guid.Parse("00000000-0000-0000-0000-000000000174"),
                    "operator-pool",
                    2,
                    instanceId,
                    "review-ticket",
                    DateTimeOffset.UnixEpoch,
                    null)
                {
                    ReviewMarked = true
                }
            ],
            []);

        OrcaCoreDurableDiagnostics.RefreshOperatorStatistics(statistics, [pool]);
        listener.RecordObservableInstruments();

        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesActive &&
            measurement.Value == 3 &&
            measurement.HasTag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode) &&
            measurement.HasTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()) &&
            measurement.HasTag(OrcaCoreDiagnostics.StatusKey, global::OrcaCore.WorkflowInstanceStatus.Waiting.ToString()));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesStuck &&
            measurement.Value == statistics.Pressure.StuckInstanceCount &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode),
                Tag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString())));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.WaitsActive &&
            measurement.Value == statistics.Pressure.ActiveWaitCount &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode),
                Tag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()),
                Tag(OrcaCoreDiagnostics.WaitEventNameKey, waitEventName.Value)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StreamEvents &&
            measurement.Value == statistics.Pressure.StreamEventCount &&
            measurement.HasExactTags(Tag(OrcaCoreDiagnostics.ProviderNameKey, statistics.ProviderName)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ContinuationPendingCount &&
            measurement.Value == statistics.Pressure.ContinuationPoisonedCount &&
            measurement.HasTag(OrcaCoreDiagnostics.QueueLaneKey, OrcaCoreDiagnostics.ContinuationQueueLane) &&
            measurement.HasTag(OrcaCoreDiagnostics.OutboxStateKey, OrcaCoreDiagnostics.PoisonedOutboxState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ExternalOutboxPendingCount &&
            measurement.Value == statistics.Pressure.ExternalOutboxRetryableCount &&
            measurement.HasTag(OrcaCoreDiagnostics.QueueLaneKey, OrcaCoreDiagnostics.ExternalOutboxQueueLane) &&
            measurement.HasTag(OrcaCoreDiagnostics.OutboxStateKey, OrcaCoreDiagnostics.RetryableOutboxState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolTickets &&
            measurement.Value == 1 &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name),
                Tag(OrcaCoreDiagnostics.ResourcePoolStateKey, OrcaCoreDiagnostics.HeldResourceState)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolReservedUnits &&
            measurement.Value == 3 &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name),
                Tag(OrcaCoreDiagnostics.ResourcePoolStateKey, OrcaCoreDiagnostics.HeldResourceState)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolTickets &&
            measurement.Value == 1 &&
            measurement.HasTag(
                OrcaCoreDiagnostics.ResourcePoolStateKey,
                OrcaCoreDiagnostics.ReviewMarkedResourceState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolReservedUnits &&
            measurement.Value == 2 &&
            measurement.HasTag(
                OrcaCoreDiagnostics.ResourcePoolStateKey,
                OrcaCoreDiagnostics.ReviewMarkedResourceState));

    }

    [Fact]
    public async Task RuntimeObservation_UsesAggregateVersionAndRealStepIdentity()
    {
        var runtimeObserver = new RecordingRuntimeObserver();
        var definitionId = global::OrcaCore.DefinitionId.Parse("00000000-0000-0000-0000-000000000171");
        var instanceId = global::OrcaCore.InstanceId.Parse("00000000-0000-0000-0000-000000000172");
        var eventStore = new RecordingEventStore
        {
            Tail =
            [
                new WorkflowStartedEvent
                {
                    EventId = global::OrcaCore.EventId.Create("00000000-0000-0000-0000-000000000175"),
                    InstanceId = instanceId,
                    CommandId = new CommandId(Guid.Parse("00000000-0000-0000-0000-000000000176")),
                    CausationId = new CausationId(Guid.Parse("00000000-0000-0000-0000-000000000177")),
                    OccurredAt = DateTimeOffset.UnixEpoch,
                    DefinitionId = definitionId,
                    DefinitionVersion = global::OrcaCore.DefinitionVersion.Initial
                }
            ]
        };
        var processor = new DurableCommandProcessor(eventStore, runtimeObserver: runtimeObserver);
        await processor.ProcessAsync(
            new DurableStepCompletedCommand(
                new CommandId(Guid.Parse("00000000-0000-0000-0000-000000000178")),
                instanceId,
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                "workflow:$/n:00000000",
                TestEnvelopes.Envelope("application/octet-stream", [1], instanceId: instanceId))
            {
                StepOperationId = StepOperationId.Parse("0:root:n:00000000:0"),
                StepAttemptNumber = 3
            },
            TestContext.Current.CancellationToken);

        var observation = runtimeObserver.Observations.Should().ContainSingle().Which;
        observation.ExpectedStreamVersion.Should().Be(new StreamVersion(1));
        var step = observation.Events.Should().ContainSingle().Which;
        step.StepOperationId.Should().Be(StepOperationId.Parse("0:root:n:00000000:0"));
        step.StepAttemptNumber.Should().Be(3);
    }

    private static KeyValuePair<string, object?> Tag(string key, object value) => new(key, value);

    private sealed record ObservedMeasurement(
        string Name,
        long Value,
        KeyValuePair<string, object?>[] Tags)
    {
        internal bool HasTag(string key, object value) =>
            Tags.Any(tag => string.Equals(tag.Key, key, StringComparison.Ordinal) && Equals(tag.Value, value));

        internal bool HasExactTags(params KeyValuePair<string, object?>[] expected) =>
            Tags.Length == expected.Length && expected.All(item => HasTag(item.Key, item.Value!));
    }

    private sealed class RecordingRuntimeObserver : IWorkflowRuntimeObserver
    {
        internal List<WorkflowRuntimeObservation> Observations { get; } = [];

        public ValueTask OnCommandCompletedAsync(
            WorkflowRuntimeObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnProviderCommitFailedAsync(
            WorkflowProviderCommitFailureObservation observation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingEventStore : IWorkflowEventStore
    {
        internal required IReadOnlyList<DurableWorkflowEvent> Tail { get; init; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            global::OrcaCore.InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Option<CheckpointWrite>.None);
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(batch.ExpectedVersion.Next())));
        }

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Tail);
        }
    }
}
