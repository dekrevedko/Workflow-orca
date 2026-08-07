using System.Diagnostics;
using System.Runtime.CompilerServices;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.Execution;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

internal sealed class DurableCommandProcessor
{
    private const int LifecycleConflictRetryLimit = 3;

    private readonly DurableCommandRuntime runtime;
    private readonly IWorkflowEventStore eventStore;
    private readonly DurableAggregateLoader aggregateLoader;
    private readonly DurableCommitPipeline commitPipeline;
    private readonly IResourcePoolStore? resourcePoolStore;
    private readonly IWorkflowInboxStore? inboxStore;
    private readonly IWorkflowStartIdempotencyStore? startIdempotencyStore;
    private readonly DurableCommandTelemetry telemetry;

    /// <summary>
    /// Initializes a command processor with its own durable command runtime.
    /// </summary>
    /// <param name="eventStore">The durable event store used for command commits.</param>
    /// <param name="resourcePoolStore">The optional durable resource-pool store used by resource commands.</param>
    public DurableCommandProcessor(
        IWorkflowEventStore eventStore,
        IResourcePoolStore? resourcePoolStore = null,
        IWorkflowRuntimeObserver? runtimeObserver = null)
        : this(new DurableCommandRuntime(eventStore, resourcePoolStore), runtimeObserver)
    {
    }

    /// <summary>
    /// Initializes a command processor that uses a shared durable command runtime.
    /// </summary>
    /// <param name="runtime">The shared durable command runtime for the host process.</param>
    public DurableCommandProcessor(
        DurableCommandRuntime runtime,
        IWorkflowRuntimeObserver? runtimeObserver = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        this.runtime = runtime;
        telemetry = new DurableCommandTelemetry(runtimeObserver ?? NullWorkflowRuntimeObserver.Instance);
        eventStore = runtime.EventStore;
        aggregateLoader = new DurableAggregateLoader(eventStore);
        resourcePoolStore = runtime.ResourcePoolStore;
        commitPipeline = new DurableCommitPipeline(
            eventStore,
            new DurableCommitMaterializer(),
            new DurableResourcePoolCommitEffects(resourcePoolStore));
        inboxStore = eventStore as IWorkflowInboxStore;
        startIdempotencyStore = eventStore as IWorkflowStartIdempotencyStore;
    }

    internal int ActiveLaneCount => runtime.ActiveLaneCount;

    internal IWorkflowEventStore EventStore => eventStore;

    internal IDurableResourceLeaseCertificationGate LeaseCertificationGate { get; init; } =
        NullDurableResourceLeaseCertificationGate.Instance;

    internal DurableCommandRuntime.StepCancellationScope EnterStep(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        runtime.EnterStep(instanceId, cancellationToken);

    internal bool HasRunningStep(InstanceId instanceId) =>
        runtime.HasRunningStep(instanceId);

    internal async Task ValidateResourcePoolsAsync(
        IReadOnlyList<ResourcePoolRequirement> requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        var missing = new List<ResourcePoolName>();
        foreach (var name in requirements
                     .Select(requirement => requirement.PoolName)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!(await RequiredResourcePoolStore()
                    .GetPoolAsync(name, cancellationToken)
                    .ConfigureAwait(false)).HasValue)
            {
                missing.Add(ResourcePoolName.Create(name));
            }
        }

        if (missing.Count > 0)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.ResourcePoolsNotConfigured(missing);
        }
    }

    internal async Task<bool> HasGrantedResourceTicketsAsync(
        InstanceId instanceId,
        string holderKey,
        IReadOnlyList<ResourcePoolRequirement> requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderKey);
        ArgumentNullException.ThrowIfNull(requirements);

        foreach (var requirement in requirements)
        {
            var snapshot = await RequiredResourcePoolStore()
                .GetPoolAsync(requirement.PoolName, cancellationToken)
                .ConfigureAwait(false);
            if (!snapshot.HasValue ||
                snapshot.Value.HeldTickets
                    .Where(ticket =>
                        ticket.HolderInstanceId.Equals(instanceId) &&
                        string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
                    .Sum(ticket => ticket.Count) != requirement.Count)
            {
                return false;
            }
        }

        return requirements.Count > 0;
    }

    internal Task<ResourcePoolReleaseResult> ReleaseConfirmedResourceHolderAsync(
        InstanceId instanceId,
        string holderKey,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderKey);
        return RequiredResourcePoolStore().ReleaseAsync(
            new ResourcePoolReleaseRequest(instanceId, holderKey, releasedAt),
            cancellationToken);
    }

    internal Task<Option<ResourcePoolReleaseEvidence>> GetResourceReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        return RequiredResourcePoolStore().GetReleaseEvidenceAsync(protectionToken, cancellationToken);
    }

    internal Task<Option<LeaseProtectionToken>> GetResourceConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmationId);
        return RequiredResourcePoolStore().GetConfirmationBindingAsync(confirmationId, cancellationToken);
    }

    internal async Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseResourceHolderAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await RequiredResourcePoolStore()
            .ConfirmAndReleaseAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    internal Task<IReadOnlyList<ResourcePoolSnapshot>> ListResourcePoolsAsync(
        CancellationToken cancellationToken) =>
        RequiredResourcePoolStore().ListPoolsAsync(cancellationToken);

    internal async Task<IReadOnlyList<ResourcePoolTicket>> GetResourceTicketsAsync(
        InstanceId instanceId,
        string holderKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderKey);
        return (await RequiredResourcePoolStore()
                .ListPoolsAsync(cancellationToken)
                .ConfigureAwait(false))
            .SelectMany(pool => pool.HeldTickets)
            .Where(ticket =>
                ticket.HolderInstanceId.Equals(instanceId) &&
                string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
            .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
            .ThenBy(ticket => ticket.ProviderGeneration)
            .ToArray();
    }

    internal async Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return startIdempotencyStore is null
            ? Option<StartedWorkflowIdempotencyRecord>.None
            : await startIdempotencyStore.GetStartedAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    public Task<DurableCommandResult> ProcessAsync(
        StartWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStart(command),
            cancellationToken,
            commandType: nameof(StartWorkflowCommand));
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableStepCompletedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStepCompleted(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableStepFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStepFailed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableFiberFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideFiberFailed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableParkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecidePark(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableUnparkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideUnpark(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableContinuationAttemptFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinuationAttemptFailed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableContinuationAttemptResetCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinuationAttemptReset(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableYieldCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideYield(command),
            cancellationToken,
            commandType: nameof(DurableYieldCommand),
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableWaitRegisteredCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideWaitRegistered(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableWaitMatchedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideWaitMatched(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        ScheduleTimerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTimerScheduled(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    public Task<DurableCommandResult> ProcessAsync(
        FireTimerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTimerFired(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        AcquireResourcePoolCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            async (aggregate, token) =>
            {
                var acquireResult = await RequiredResourcePoolStore()
                    .AcquireAsync(
                        new ResourcePoolAcquireRequest(
                            command.InstanceId,
                            command.HolderKey,
                            command.Requirements,
                            command.RequestedAt,
                            command.ExpiresAt)
                        {
                            FiberId = command.FiberId,
                            ScopeId = command.ScopeId,
                            ProtectionToken = command.LeaseProtectionToken is null
                                ? null
                                : LeaseProtectionToken.Parse(command.LeaseProtectionToken)
                        },
                        token)
                    .ConfigureAwait(false);
                if (acquireResult.Status == ResourcePoolAcquireStatus.Granted &&
                    command.LeaseObligationId is not null &&
                    command.LeaseProtectionToken is not null &&
                    command.LeaseFiberOccurrence is not null &&
                    command.LeaseScopeOccurrence is not null)
                {
                    await ReportLeaseBarrierAsync(
                        DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted,
                        command.InstanceId,
                        command.LeaseGeneration,
                        command.LeaseObligationId,
                        command.LeaseFiberOccurrence,
                        command.LeaseScopeOccurrence,
                        command.LeaseProtectionToken,
                        command.ExpectedStreamVersion?.Value ?? aggregate.StreamVersion.Value,
                        acquireResult.Tickets,
                        token).ConfigureAwait(false);
                }

                return aggregate.DecideResourcePoolAcquire(command, acquireResult);
            },
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal async ValueTask ReportLeaseBarrierAsync(
        DurableResourceLeaseCommitBarrier barrier,
        InstanceId instanceId,
        int generation,
        string obligationId,
        string fiberOccurrence,
        string scopeOccurrence,
        string protectionToken,
        long workflowVersion,
        IReadOnlyList<ResourcePoolTicket> tickets,
        CancellationToken cancellationToken)
    {
        var ticketFacts = tickets
            .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
            .ThenBy(ticket => ticket.ProviderGeneration)
            .Select(ticket => new DurableResourceLeaseTicketSnapshot(
                ticket.TicketId.ToString("N"),
                ResourcePoolName.Create(ticket.PoolName),
                ticket.Count,
                ticket.ProviderGeneration,
                ticket.ReviewDeadline ?? DateTimeOffset.MaxValue,
                ticket.ReviewMarked))
            .ToArray();
        await LeaseCertificationGate.OnPostCommitAsync(
            new DurableResourceLeaseCommitBarrierFact(
                barrier,
                ResourceGovernancePartitionId.Create("default"),
                obligationId,
                instanceId,
                generation,
                fiberOccurrence,
                scopeOccurrence,
                LeaseProtectionToken.Parse(protectionToken),
                workflowVersion,
                ticketFacts.Length == 0
                    ? 0
                    : ticketFacts.Max(ticket => ticket.ProviderGeneration),
                ticketFacts),
            cancellationToken).ConfigureAwait(false);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableLeaseStopConfirmedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideLeaseStopConfirmed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    public async Task<DurableCommandResult> ProcessAsync(
        CancelWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 0; ; attempt++)
        {
            var disposition = DurableLifecycleDisposition.None;
            var result = await RunInLaneAsync(
                command.InstanceId,
                aggregate =>
                {
                    if (aggregate.IsTerminal)
                    {
                        disposition = DurableLifecycleDisposition.AlreadyTerminal;
                        return DurableDecision.Empty;
                    }

                    if (aggregate.Status == WorkflowStatus.CancellationRequested)
                    {
                        disposition = DurableLifecycleDisposition.CancellationAlreadyRequested;
                        return DurableDecision.Empty;
                    }

                    disposition = DurableLifecycleDisposition.CancellationRequested;
                    return aggregate.DecideCancel(command);
                },
                cancellationToken).ConfigureAwait(false);

            if (result.Outcome == DurableCommandOutcome.Conflict &&
                attempt < LifecycleConflictRetryLimit)
            {
                continue;
            }

            if (result.Outcome != DurableCommandOutcome.Conflict &&
                disposition is DurableLifecycleDisposition.CancellationRequested or
                    DurableLifecycleDisposition.CancellationAlreadyRequested)
            {
                runtime.RequestStepCancellation(command.InstanceId);
                if (!runtime.HasRunningStep(command.InstanceId))
                {
                    // Waits, timers, and replacement-host recovery have no
                    // cooperative body to await. Preserve the committed request disposition
                    // for the caller while completing definite cleanup immediately.
                    _ = await FinalizeCancellationAsync(
                        command.InstanceId,
                        command.RequestedAt,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return result with { LifecycleDisposition = disposition };
        }
    }

    internal Task<DurableCommandResult> FinalizeCancellationAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        return RunInLaneAsync(
            instanceId,
            async (aggregate, token) =>
            {
                if (aggregate.Status != WorkflowStatus.CancellationRequested)
                {
                    return DurableDecision.Empty;
                }

                return aggregate.DecideTerminalLifecycle(
                    await CreateTerminalLifecycleCommandAsync(
                            CommandId.New(),
                            instanceId,
                            requestedAt,
                            WorkflowStatus.Cancelled,
                            token)
                        .ConfigureAwait(false));
            },
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DeliverEventCommand command,
        CancellationToken cancellationToken) =>
        ProcessAsync(command, inboxMatch: null, cancellationToken);

    internal Task<DurableCommandResult> ProcessAsync(
        DeliverEventCommand command,
        InboxMatchSnapshot? inboxMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideDeliverEvent(command),
            cancellationToken,
            new DurableInboxDelivery(
                command.Envelope.EventId,
                command.EnvelopeFingerprint ?? DurableEventEnvelopeFingerprint.Create(command.Envelope),
                command.Envelope)
            {
                Match = inboxMatch,
                TargetInstanceId = command.InstanceId
            });
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableCompleteCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideComplete(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableFailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideFail(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableTimeoutCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTimeout(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    public async Task<DurableCommandResult> ProcessAsync(
        TerminateWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        for (var attempt = 0; ; attempt++)
        {
            var disposition = DurableLifecycleDisposition.None;
            var result = await RunInLaneAsync(
                command.InstanceId,
                async (aggregate, token) =>
                {
                    if (aggregate.IsTerminal)
                    {
                        disposition = DurableLifecycleDisposition.AlreadyTerminal;
                        return DurableDecision.Empty;
                    }

                    disposition = DurableLifecycleDisposition.Terminated;
                    return aggregate.DecideTerminalLifecycle(
                        await CreateTerminalLifecycleCommandAsync(
                                command.CommandId,
                                command.InstanceId,
                                command.RequestedAt,
                                WorkflowStatus.Terminated,
                                token)
                            .ConfigureAwait(false));
                },
                cancellationToken).ConfigureAwait(false);

            if (result.Outcome == DurableCommandOutcome.Conflict &&
                attempt < LifecycleConflictRetryLimit)
            {
                continue;
            }

            if (result.Outcome == DurableCommandOutcome.Committed)
            {
                runtime.RequestStepCancellation(command.InstanceId);
            }

            return result with { LifecycleDisposition = disposition };
        }
    }

    public Task<DurableCommandResult> ProcessAsync(
        ContinueAsNewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinueAsNew(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> EvictIdleAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return RunInLaneAsync(
            instanceId,
            aggregate => aggregate.Snapshot.Status is null
                ? DurableDecision.Empty
                : new DurableDecision([], null, true),
            cancellationToken);
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, DurableDecision> decide,
        CancellationToken cancellationToken,
        DurableInboxDelivery? inboxDelivery = null,
        string commandType = "DurableCommand",
        StreamVersion? expectedVersion = null)
    {
        return await RunInLaneAsync(
            instanceId,
            (aggregate, _) => Task.FromResult(decide(aggregate)),
            cancellationToken,
            inboxDelivery,
            commandType,
            expectedVersion).ConfigureAwait(false);
    }

    private async Task<DurableTerminalLifecycleCommand> CreateTerminalLifecycleCommandAsync(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        WorkflowStatus status,
        CancellationToken cancellationToken)
    {
        var checkpoint = await eventStore
            .LoadCheckpointAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        if (!checkpoint.HasValue ||
            !string.Equals(
                checkpoint.Value.ContentType,
                DurableExecutionEnvelopeV2.ContentType,
                StringComparison.Ordinal))
        {
            return new DurableTerminalLifecycleCommand(
                commandId,
                instanceId,
                requestedAt,
                status,
                Envelope: null);
        }

        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
        var preserveHolderKeys = new HashSet<string>(StringComparer.Ordinal);
        var queuedCancellations = new List<DurableQueuedResourceCancellation>();
        var obligations = envelope.OwnedObligations
            .Where(obligation => obligation.Kind == DurableOwnedObligationKind.Resource)
            .Select(obligation =>
            {
                if (string.IsNullOrWhiteSpace(obligation.HolderKey))
                {
                    return obligation;
                }

                if (string.Equals(
                        obligation.LeasePhase,
                        nameof(Driver.DurableLeaseObligationPhase.Queued),
                        StringComparison.Ordinal))
                {
                    queuedCancellations.Add(new DurableQueuedResourceCancellation(
                        obligation.HolderKey,
                        new FiberId(obligation.FiberId),
                        obligation.ScopeId is null ? null : new ScopeId(obligation.ScopeId)));
                    return obligation with
                    {
                        LeasePhase = nameof(Driver.DurableLeaseObligationPhase.CancelledBeforeGrant)
                    };
                }

                if (obligation.LeasePhase is
                    nameof(Driver.DurableLeaseObligationPhase.PendingCommit) or
                    nameof(Driver.DurableLeaseObligationPhase.Held) or
                    nameof(Driver.DurableLeaseObligationPhase.ReviewMarked) or
                    nameof(Driver.DurableLeaseObligationPhase.AmbiguousHeld))
                {
                    preserveHolderKeys.Add(obligation.HolderKey);
                    return obligation with
                    {
                        LeasePhase = nameof(Driver.DurableLeaseObligationPhase.Quarantined)
                    };
                }

                if (obligation.LeasePhase is
                    nameof(Driver.DurableLeaseObligationPhase.Quarantined) or
                    nameof(Driver.DurableLeaseObligationPhase.LeaseLost))
                {
                    preserveHolderKeys.Add(obligation.HolderKey);
                }

                return obligation;
            })
            .ToArray();

        return new DurableTerminalLifecycleCommand(
            commandId,
            instanceId,
            requestedAt,
            status,
            envelope with { OwnedObligations = obligations })
        {
            PreserveResourceHolderKeys = preserveHolderKeys,
            QueuedResourceCancellations = queuedCancellations
        };
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        DurableInboxDelivery? inboxDelivery = null,
        string commandType = "DurableCommand",
        StreamVersion? expectedVersion = null)
    {
        return await runtime.RunAsync(
            instanceId,
            token => ProcessCoreAsync(instanceId, decide, token, inboxDelivery, commandType, expectedVersion),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        DurableInboxDelivery? inboxDelivery,
        string commandType,
        StreamVersion? expectedVersion = null)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.command.process");
        activity?.SetTag(OrcaCoreDiagnostics.CommandTypeKey, commandType);
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());

        try
        {
            // Stage 1: inbox preflight â€” a duplicate or discarded delivery completes without
            // touching the aggregate.
            var inboxRecord = await LoadInboxRecordAsync(
                    instanceId,
                    inboxDelivery,
                    cancellationToken)
                .ConfigureAwait(false);
            if (InboxEnvelopeConflict(inboxRecord, inboxDelivery) is { } envelopeConflict)
            {
                return await CompleteWithoutCommitAsync(
                    instanceId,
                    envelopeConflict,
                    inboxDelivery?.EventId,
                    commandType,
                    stopwatch,
                    activity,
                    inboxDuplicate: false,
                    cancellationToken).ConfigureAwait(false);
            }

            var inboxState = InboxState(inboxRecord);
            if (DurableInboxPreflight.TryCreateResult(inboxState) is { } preflightResult)
            {
                return await CompleteWithoutCommitAsync(
                    instanceId,
                    preflightResult,
                    inboxDelivery?.EventId,
                    commandType,
                    stopwatch,
                    activity,
                    IsInboxDuplicate(inboxState),
                    cancellationToken).ConfigureAwait(false);
            }

            // Stage 2: load and guard the optimistic stream version.
            var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (GuardExpectedVersion(aggregate, expectedVersion) is { } conflictResult)
            {
                return await CompleteWithoutCommitAsync(
                    instanceId,
                    conflictResult,
                    inboxDelivery?.EventId,
                    commandType,
                    stopwatch,
                    activity,
                    inboxDuplicate: false,
                    cancellationToken).ConfigureAwait(false);
            }

            // Stage 3: decide and commit (one atomic durable commit per command).
            var activeWaitsById = aggregate.Snapshot.ActiveWaits.ToDictionary(wait => wait.WaitId);
            var decision = await decide(aggregate, cancellationToken).ConfigureAwait(false);

            var providerCommitAttempted = HasProviderCommit(decision, inboxDelivery);
            var providerName = DurableCommandTelemetry.ProviderName(eventStore);
            var providerCommitStopwatch = Stopwatch.StartNew();
            var result = await CommitWithTelemetryAsync(
                    instanceId,
                    aggregate,
                    decision,
                    inboxDelivery,
                    providerCommitAttempted,
                    providerName,
                    cancellationToken)
                .ConfigureAwait(false);
            providerCommitStopwatch.Stop();
            stopwatch.Stop();
            activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
            activity?.SetTag("stream.version", result.StreamVersion.Value);

            // Stage 4: derive post-commit observed state and emit observations.
            var observed = DeriveObservedState(aggregate, decision, result, activity);
            var eventObservations = result.Outcome is DurableCommandOutcome.Committed
                ? DurableCommandTelemetry.CreateEventObservations(
                    decision.Events,
                    observed.DefinitionId,
                    activeWaitsById,
                    stopwatch.Elapsed)
                : [];
            DurableCommandTelemetry.RecordEventSpans(eventObservations, instanceId);
            DurableCommandTelemetry.RecordStepSpans(eventObservations, instanceId);

            return await telemetry.ObserveCommandCompletedAsync(
                instanceId,
                result,
                decision.Events.Count,
                decision.Checkpoint is not null,
                inboxDelivery?.EventId,
                cancellationToken,
                commandType,
                observed.DefinitionId,
                observed.DefinitionVersion,
                observed.Status,
                stopwatch.Elapsed,
                eventObservations,
                providerCommitAttempted,
                providerName,
                providerCommitStopwatch.Elapsed,
                inboxDuplicate: false)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    /// <summary>
    /// Completes a command that never reached the commit stage (inbox preflight hit or
    /// optimistic-version conflict): stops timing, tags the outcome, and observes it with
    /// zero events and no checkpoint.
    /// </summary>
    private async Task<DurableCommandResult> CompleteWithoutCommitAsync(
        InstanceId instanceId,
        DurableCommandResult result,
        EventId? inboxEventId,
        string commandType,
        Stopwatch stopwatch,
        Activity? activity,
        bool inboxDuplicate,
        CancellationToken cancellationToken)
    {
        stopwatch.Stop();
        activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
        return await telemetry.ObserveCommandCompletedAsync(
            instanceId,
            result,
            eventCount: 0,
            checkpointWritten: false,
            inboxEventId,
            cancellationToken,
            commandType,
            definitionId: null,
            definitionVersion: null,
            status: null,
            stopwatch.Elapsed,
            inboxDuplicate: inboxDuplicate)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects a command decided against a stale read: another mutator moved the stream past
    /// the version the driver observed. Rejecting here keeps committed transitions
    /// exactly-once (DU-022). Returns null when the stream is at the expected version.
    /// </summary>
    private static DurableCommandResult? GuardExpectedVersion(
        DurableWorkflowAggregate aggregate,
        StreamVersion? expectedVersion)
    {
        if (expectedVersion is not { } requiredVersion || aggregate.StreamVersion == requiredVersion)
        {
            return null;
        }

        return new DurableCommandResult(
            DurableCommandOutcome.Conflict,
            $"Expected stream version {requiredVersion.Value} but found {aggregate.StreamVersion.Value}.",
            aggregate.StreamVersion);
    }

    /// <summary>
    /// Derives the definition/status the observer should see: the committed projection
    /// snapshot when the commit landed, otherwise the aggregate's decision-time view; tags
    /// the command activity with whichever won.
    /// </summary>
    private static ObservedInstanceState DeriveObservedState(
        DurableWorkflowAggregate aggregate,
        DurableDecision decision,
        DurableCommandResult result,
        Activity? activity)
    {
        var aggregateSnapshot = aggregate.Snapshot;
        var committedSnapshot = result.Outcome == DurableCommandOutcome.Committed
            ? aggregate
                .CreateProjectionWrites(decision.Events)
                .Select(write => write.InstanceSnapshot)
                .FirstOrDefault(snapshot => snapshot is not null)
            : null;
        var observed = new ObservedInstanceState(
            committedSnapshot?.DefinitionId ?? aggregateSnapshot.DefinitionId,
            committedSnapshot?.DefinitionVersion ?? aggregateSnapshot.DefinitionVersion,
            committedSnapshot?.Status ?? aggregateSnapshot.Status);

        if (observed.DefinitionId is { } definitionId)
        {
            activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
        }

        if (observed.DefinitionVersion is { } definitionVersion)
        {
            activity?.SetTag(OrcaCoreDiagnostics.DefinitionVersionKey, definitionVersion.ToString());
        }

        if (observed.Status is { } status)
        {
            activity?.SetTag(OrcaCoreDiagnostics.StatusKey, status.ToString());
        }

        return observed;
    }

    private readonly record struct ObservedInstanceState(
        DefinitionId? DefinitionId,
        DefinitionVersion? DefinitionVersion,
        WorkflowStatus? Status);

    private async Task<DurableCommandResult> CommitWithTelemetryAsync(
        InstanceId instanceId,
        DurableWorkflowAggregate aggregate,
        DurableDecision decision,
        DurableInboxDelivery? inboxDelivery,
        bool providerCommitAttempted,
        string providerName,
        CancellationToken cancellationToken)
    {
        if (!providerCommitAttempted)
        {
            return await commitPipeline
                .CommitAsync(instanceId, aggregate, decision, inboxDelivery, cancellationToken)
                .ConfigureAwait(false);
        }

        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.provider.commit");
        activity?.SetTag(OrcaCoreDiagnostics.ProviderNameKey, providerName);
        activity?.SetTag(OrcaCoreDiagnostics.ProviderOperationKey, "append");
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());

        try
        {
            var result = await commitPipeline
                .CommitAsync(instanceId, aggregate, decision, inboxDelivery, cancellationToken)
                .ConfigureAwait(false);
            activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
            activity?.SetTag(OrcaCoreDiagnostics.StreamVersionKey, result.StreamVersion.Value);
            if (result.Outcome is DurableCommandOutcome.Conflict)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static bool HasProviderCommit(DurableDecision decision, DurableInboxDelivery? inboxDelivery)
    {
        return decision.Events.Count > 0 || decision.Checkpoint is not null || inboxDelivery is not null;
    }

    private static bool IsInboxDuplicate(Option<InboxRecordState> inboxState)
    {
        return inboxState.HasValue &&
            inboxState.Value is InboxRecordState.Applied
                or InboxRecordState.DuplicateIgnored
                or InboxRecordState.DiscardedOnResume;
    }

    private static DurableCommandResult? InboxEnvelopeConflict(
        Option<InboxRecord> inboxRecord,
        DurableInboxDelivery? inboxDelivery)
    {
        if (!inboxRecord.HasValue || inboxDelivery is not { } delivery ||
            string.Equals(
                inboxRecord.Value.EnvelopeFingerprint,
                delivery.EnvelopeFingerprint,
                StringComparison.Ordinal))
        {
            return null;
        }

        return new DurableCommandResult(
            DurableCommandOutcome.Conflict,
            "The event identity is already bound to a different durable envelope.",
            StreamVersion.Empty);
    }

    private static Option<InboxRecordState> InboxState(Option<InboxRecord> inboxRecord)
    {
        return inboxRecord.HasValue
            ? Option<InboxRecordState>.Some(inboxRecord.Value.State)
            : Option<InboxRecordState>.None;
    }

    private async Task<Option<InboxRecord>> LoadInboxRecordAsync(
        InstanceId instanceId,
        DurableInboxDelivery? inboxDelivery,
        CancellationToken cancellationToken)
    {
        if (inboxDelivery is not { } delivery)
        {
            return Option<InboxRecord>.None;
        }

        return await RequiredInboxStore()
            .GetAsync(instanceId, delivery.EventId, cancellationToken)
            .ConfigureAwait(false);
    }

    private IWorkflowInboxStore RequiredInboxStore()
    {
        return inboxStore ?? throw new InvalidOperationException(
            "Durable event delivery requires an inbox-capable provider.");
    }

    private IResourcePoolStore RequiredResourcePoolStore()
    {
        return resourcePoolStore ?? throw new InvalidOperationException(
            "Durable resource-pool acquisition requires a resource-pool-capable provider.");
    }

}

internal enum DurableCommandOutcome
{
    Committed,
    Conflict,
    Evicted,
    Poisoned,
    NoOp
}

internal sealed record DurableCommandResult(
    DurableCommandOutcome Outcome,
    string? Message,
    StreamVersion StreamVersion,
    bool Evicted = false)
{
    internal DurableLifecycleDisposition LifecycleDisposition { get; init; }
}

internal enum DurableLifecycleDisposition
{
    None,
    CancellationRequested,
    CancellationAlreadyRequested,
    Terminated,
    AlreadyTerminal
}
