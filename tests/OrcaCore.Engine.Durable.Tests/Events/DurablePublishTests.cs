using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
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
        outbound.CorrelationId.Should().Be(correlationId.Value);
        outbound.CausationEventId.Should().BeNull();
        outbound.OccurredAt.Should().Be(PublishedAt);
        outbound.OriginInstanceId.Should().Be(started.InstanceId.Value.ToString());
        outbound.OriginDefinitionId.Should().Be(definition.DefinitionId.Value.ToString());
        outbound.OriginDefinitionVersion.Should().Be(3);
        JsonSerializer.Deserialize<PublishedPayload>(outbound.Payload)
            .Should().Be(new PublishedPayload("accepted"));
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
        DurableWorkflowOutboundEventCodec.Decode(claimed.Should().ContainSingle().Subject.Payload)
            .CausationEventId.Should().BeNull();
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
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
