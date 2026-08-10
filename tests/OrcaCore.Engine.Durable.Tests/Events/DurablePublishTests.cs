using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Events;

public sealed class DurablePublishTests
{
    private static readonly DateTimeOffset PublishedAt =
        new(2026, 8, 7, 20, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task Publish_CommitsProgressionAndCompleteRuntimeOwnedOutboundEventAtomically()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store, new FixedTimeProvider(PublishedAt));
        var eventContract = WorkflowEventContract<PublishedPayload>.Create(
            EventName.Create("order-approved"),
            new EventContractVersion(2));
        var correlationId = CorrelationId.Create("order-42");
        var definition = Workflow.Durable<PublishState>(DefinitionId.New(), new DefinitionVersion(3))
            .Init<string>(value => new PublishState(value))
            .Publish(eventContract, _ => correlationId, state => new PublishedPayload(state.Value.Value))
            .End()
            .Build();
        runtime.RegisterDefinition(definition);

        var started = await runtime.StartOrGetAsync<string, PublishState>(
            "publish-atomic-commit",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "accepted",
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(
            new OutboxClaimRequest(10, PublishedAt, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent)
            },
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetAsync(
            started.InstanceId,
            TestContext.Current.CancellationToken);

        snapshot.Value.Status.Should().Be(WorkflowInstanceStatus.Completed);
        var record = claimed.Should().ContainSingle().Subject;
        record.Kind.Should().Be(OutboxKinds.WorkflowEvent);
        var outbound = DurableWorkflowOutboundEventCodec.Decode(record.Payload);
        outbound.EventName.Should().Be(eventContract.EventName.Value);
        outbound.EventContractVersion.Should().Be(2);
        outbound.PayloadTypeName.Should().Be(typeof(PublishedPayload).AssemblyQualifiedName);
        outbound.PayloadSchemaIdentity.Should().Be(typeof(PublishedPayload).AssemblyQualifiedName);
        outbound.EventId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(outbound.EventId, out var eventId).Should().BeTrue();
        eventId.Should().NotBe(Guid.Empty);
        outbound.CorrelationId.Should().Be(correlationId.Value);
        outbound.CausationEventId.Should().BeNull();
        outbound.OccurredAt.Should().Be(PublishedAt);
        outbound.OriginInstanceId.Should().Be(started.InstanceId.Value.ToString());
        outbound.OriginDefinitionId.Should().Be(definition.DefinitionId.Value.ToString());
        outbound.OriginDefinitionVersion.Should().Be(3);
        var applicationEvent = global::OrcaCore.Engine.Durable.Internal
            .DurableContractAdapter.WorkflowOutboundEvent(outbound);
        applicationEvent.GetPayload(eventContract).Should().Be(new PublishedPayload("accepted"));
    }

    [Fact]
    public async Task Publish_AfterWaitCarriesTheConsumedInboundEventAsCausation()
    {
        var store = new InMemoryWorkflowProvider();
        using var services = new ServiceCollection()
            .AddTransient<ObserveApprovalStep>()
            .BuildServiceProvider();
        var runtime = CreateRuntime(store, new FixedTimeProvider(PublishedAt), services);
        var inboundContract = WorkflowEventContract.Create(
            EventName.Create("approval-received"),
            EventContractVersion.Initial);
        var outboundContract = WorkflowEventContract.Create(
            EventName.Create("approval-observed"),
            EventContractVersion.Initial);
        var correlationId = CorrelationId.Create("approval-7");
        var definition = Workflow.Durable<PublishState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new PublishState(value))
            .Wait(inboundContract, _ => correlationId)
            .Then<ObserveApprovalStep>()
            .Publish(outboundContract, _ => correlationId)
            .End()
            .Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, PublishState>(
            "publish-causation",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "accepted",
            TestContext.Current.CancellationToken);
        var inboundEventId = EventId.Create("approval-event-7");
        var ingress = new DurableWorkflowEventIngressCore(runtime, store, store);

        var accepted = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                inboundContract,
                inboundEventId,
                correlationId,
                causationEventId: null,
                PublishedAt.AddMinutes(-1),
                new WorkflowEventRoute.Direct(started.InstanceId)),
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(
            new OutboxClaimRequest(10, PublishedAt, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent)
            },
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        var projection = await store.GetAsync(started.InstanceId, TestContext.Current.CancellationToken);
        projection.Value.ErrorSummary.Should().BeNull();
        projection.Value.Status.Should().Be(WorkflowInstanceStatus.Completed);
        DurableWorkflowOutboundEventCodec.Decode(claimed.Should().ContainSingle().Subject.Payload)
            .CausationEventId.Should().Be(inboundEventId.Value);
    }

    [Fact]
    public async Task Publish_InStructuredBranchCommitsAnOutboundEvent()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store, new FixedTimeProvider(PublishedAt));
        var outboundContract = WorkflowEventContract.Create(
            EventName.Create("branch-approval-observed"),
            EventContractVersion.Initial);
        var correlationId = CorrelationId.Create("branch-approval-7");
        var definition = Workflow.Durable<PublishState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new PublishState(value))
            .Parallel<string>(
                branches => branches.Branch<PublishState>(
                    AuthoredBranchId.Create("publisher"),
                    state => state.Value,
                    branch => branch
                        .Publish(outboundContract, _ => correlationId)
                        .Return(state => state.Value.Value)))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        runtime.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, PublishState>(
            "publish-branch-causation",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "accepted",
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(
            new OutboxClaimRequest(10, PublishedAt, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent)
            },
            TestContext.Current.CancellationToken);

        var projection = await store.GetAsync(started.InstanceId, TestContext.Current.CancellationToken);
        projection.Value.Status.Should().Be(WorkflowInstanceStatus.Completed);
        var outbound = DurableWorkflowOutboundEventCodec.Decode(claimed.Should().ContainSingle().Subject.Payload);
        outbound.EventName.Should().Be(outboundContract.EventName.Value);
        outbound.EventContractVersion.Should().Be(outboundContract.Version.Value);
        outbound.CorrelationId.Should().Be(correlationId.Value);
        outbound.CausationEventId.Should().BeNull();
    }

    [Fact]
    public void DefinitionExceptionFactory_ReturnsTheApprovedDiagnosticException()
    {
        var exception = global::OrcaCore.Engine.Durable.Internal
            .DurableContractAdapter.DefinitionException("publish definition is incomplete");

        exception.Should().BeOfType<WorkflowDefinitionException>();
        exception.Diagnostics.Should().ContainSingle()
            .Which.Message.Should().Be("publish definition is incomplete");
    }

    [Theory]
    [InlineData(PublishCommitFailure.BeforeApply)]
    [InlineData(PublishCommitFailure.AfterApply)]
    public async Task HostFailureAroundPublishCommit_RecoversNeitherOrBothWithoutDuplicateOutboundRecords(
        PublishCommitFailure failure)
    {
        var store = new InMemoryWorkflowProvider();
        var faultingStore = new PublishCommitFaultStore(store, failure);
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("publish-crash-boundary"),
            EventContractVersion.Initial);
        var definition = Workflow.Durable<PublishState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new PublishState(value))
            .Publish(eventContract, state => CorrelationId.Create(state.Value.Value))
            .End()
            .Build();
        var runtime = CreateRuntime(faultingStore, store, new FixedTimeProvider(PublishedAt));
        runtime.RegisterDefinition(definition);

        Func<Task> failAroundCommit = async () => await runtime.StartOrGetAsync<string, PublishState>(
            $"publish-crash-{failure}",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "accepted",
            TestContext.Current.CancellationToken);

        await failAroundCommit.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated host failure around publish commit");
        var started = await store.GetStartedAsync(
            $"publish-crash-{failure}",
            TestContext.Current.CancellationToken);
        var committedOutboxState = await store.GetStateAsync(
            faultingStore.ObservedOutboxRecordId!.Value,
            TestContext.Current.CancellationToken);
        if (failure == PublishCommitFailure.BeforeApply)
        {
            committedOutboxState.HasValue.Should().BeFalse();
        }
        else
        {
            committedOutboxState.Value.Should().Be(OutboxRecordState.Pending);
        }

        var recoveredRuntime = CreateRuntime(store, new FixedTimeProvider(PublishedAt));
        recoveredRuntime.RegisterDefinition(definition);
        await recoveredRuntime.StartOrGetAsync<string, PublishState>(
            $"publish-crash-{failure}",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "accepted",
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(
            new OutboxClaimRequest(10, PublishedAt.AddMinutes(1), TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent)
            },
            TestContext.Current.CancellationToken);
        var afterRecovery = await store.GetAsync(
            started.Value.InstanceId,
            TestContext.Current.CancellationToken);

        afterRecovery.Value.Status.Should().Be(WorkflowInstanceStatus.Completed);
        var record = claimed.Should().ContainSingle().Subject;
        DurableWorkflowOutboundEventCodec.Decode(record.Payload).EventName
            .Should().Be(eventContract.EventName.Value);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        TimeProvider timeProvider,
        IServiceProvider? services = null)
    {
        var processor = new DurableCommandProcessor(store);
        return new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(int.MaxValue, services),
            timeProvider,
            projectionStore: store);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        IWorkflowEventStore eventStore,
        IWorkflowProjectionStore projectionStore,
        TimeProvider timeProvider)
    {
        var processor = new DurableCommandProcessor(eventStore);
        return new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(int.MaxValue),
            timeProvider,
            projectionStore: projectionStore);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    public enum PublishCommitFailure
    {
        BeforeApply,
        AfterApply
    }

    private sealed class PublishCommitFaultStore(
        InMemoryWorkflowProvider inner,
        PublishCommitFailure failure) : IWorkflowEventStore, IWorkflowStartIdempotencyStore
    {
        private int armed = 1;

        internal OutboxRecordId? ObservedOutboxRecordId { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.LoadCheckpointAsync(instanceId, cancellationToken);

        public async Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            if (batch.OutboxRecords.Any(record => record.Kind == OutboxKinds.WorkflowEvent) &&
                Interlocked.Exchange(ref armed, 0) == 1)
            {
                ObservedOutboxRecordId = batch.OutboxRecords
                    .Single(record => record.Kind == OutboxKinds.WorkflowEvent)
                    .OutboxRecordId;
                if (failure == PublishCommitFailure.BeforeApply)
                {
                    throw new InvalidOperationException("simulated host failure around publish commit");
                }

                _ = await inner.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("simulated host failure around publish commit");
            }

            return await inner.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        public Task<IReadOnlyList<global::OrcaCore.Abstractions.Durable.WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) =>
            inner.LoadTailAsync(streamId, afterVersion, cancellationToken);

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            inner.GetStartedAsync(idempotencyKey, cancellationToken);
    }

    public sealed record PublishState(string Value);
    private sealed record PublishedPayload(string Value);

    public sealed class ObserveApprovalStep : IStep<PublishState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<PublishState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
