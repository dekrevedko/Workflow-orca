using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Durable.Hosting;

namespace OrcaCore.Integration.Tests.CurrentSurface;

public sealed class DurableInMemoryApplicationJourneyTests
{
    [Fact]
    public async Task ParallelDuplicateWaits_AreRejectedBeforeEitherWaitBecomesRoutable()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var definitionId = DefinitionId.New();
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("ambiguous-durable-wait"),
            new EventContractVersion(2));
        var correlation = CorrelationId.Create("ambiguous-durable-wait");
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Parallel<string>(branches => branches
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("first"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventContract, _ => correlation)
                        .Return(state => state.Value.Value.ToString()))
                .Branch<JourneyBranchState>(
                    AuthoredBranchId.Create("second"),
                    parent => new JourneyBranchState(parent.Value.Value),
                    branch => branch
                        .Wait(eventContract, _ => correlation)
                        .Return(state => state.Value.Value.ToString())))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var start = async () => await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("ambiguous-durable-wait"),
            TestContext.Current.CancellationToken);

        var exception = await start.Should().ThrowAsync<AmbiguousWaitRegistrationException>();
        exception.Which.DefinitionId.Should().Be(definitionId);
        exception.Which.EventContract.Should().Be(eventContract);
        exception.Which.CorrelationId.Should().Be(correlation);

        var delivery = async () => await host.GetRequiredService<IWorkflowEventIngress>().AcceptAsync(
            WorkflowInboundEvent.Create(
                eventContract,
                EventId.Create("ambiguous-durable-wait-probe"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Correlation(definitionId)),
            TestContext.Current.CancellationToken);

        await delivery.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*task 7.28 route inbox*");
    }

    [Fact]
    public async Task PublicV2Ingress_ResumesOnlyTheMatchingV2WaitAndNeverTheV1Wait()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        using var host = DurableTestHosts.BuildInMemory(stores);
        var eventName = EventName.Create("versioned-approval");
        var correlation = CorrelationId.Create("versioned-approval");
        var definitions = host.GetRequiredService<IWorkflowDefinitionRegistry>();
        var v1 = definitions.Register(VersionedDefinition(
            DefinitionId.New(),
            EventContractVersion.Initial,
            eventName,
            correlation)).GetHandleOrThrow();
        var v2 = definitions.Register(VersionedDefinition(
            DefinitionId.New(),
            new EventContractVersion(2),
            eventName,
            correlation)).GetHandleOrThrow();
        var v1Instance = (await v1.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("versioned-v1"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var v2Instance = (await v2.StartOrGetAsync(
            new JourneyInput(2),
            StartIdempotencyKey.Create("versioned-v2"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var ingress = host.GetRequiredService<IWorkflowEventIngress>();
        var v2Contract = WorkflowEventContract.Create(eventName, new EventContractVersion(2));

        var wrongVersion = async () => await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2Contract,
                EventId.Create("versioned-v2-to-v1"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(v1Instance.InstanceId)),
            TestContext.Current.CancellationToken);

        await wrongVersion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*task 7.28 target inbox*");
        (await v1Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);

        var accepted = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                v2Contract,
                EventId.Create("versioned-v2-to-v2"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(v2Instance.InstanceId)),
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await v2Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await v1Instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Waiting);
    }

    [Fact]
    public async Task ReplacementHost_PreservesStartBindingAndCompletesThroughPublicFacade()
    {
        using var stores = DurableTestHosts.CreateSharedInMemoryStores();
        var continueEvent = EventName.Create("continue");
        var correlation = CorrelationId.Create("durable-in-memory-replacement");
        var definition = Workflow.Durable<JourneyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial), _ => correlation)
            .End(
                state => new JourneyOutput(state.Value.Value + 1),
                WorkflowOutcomeName.Create("finished"))
            .Build();
        var startKey = StartIdempotencyKey.Create("durable-in-memory-replacement");
        InstanceId firstInstanceId;

        using (var firstHost = DurableTestHosts.BuildInMemory(stores))
        {
            var definitions = firstHost.GetRequiredService<IWorkflowDefinitionRegistry>();
            var handle = definitions.Register(definition).GetHandleOrThrow();
            var firstStart = await handle.StartOrGetAsync(
                new JourneyInput(41),
                startKey,
                TestContext.Current.CancellationToken);
            firstStart.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
                .Which.WasExisting.Should().BeFalse();
            firstInstanceId = firstStart.GetHandleOrThrow().InstanceId;
        }

        using var replacementHost = DurableTestHosts.BuildInMemory(stores);
        var replacementDefinitions =
            replacementHost.GetRequiredService<IWorkflowDefinitionRegistry>();
        var replacementHandle = replacementDefinitions.Register(definition).GetHandleOrThrow();

        var incompatibleReplay = await replacementHandle.StartOrGetAsync(
            new JourneyInput(99),
            startKey,
            TestContext.Current.CancellationToken);
        incompatibleReplay.Should()
            .BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Conflict>();

        var replay = await replacementHandle.StartOrGetAsync(
            new JourneyInput(41),
            startKey,
            TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(firstInstanceId);

        var delivery = await replacementHost
            .GetRequiredService<IWorkflowEventIngress>()
            .AcceptAsync(
                WorkflowInboundEvent.Create(
                    WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial),
                    EventId.Create("durable-in-memory-continue"),
                    correlation,
                    causationEventId: null,
                    DateTimeOffset.UtcNow,
                    new WorkflowEventRoute.Direct(firstInstanceId)),
                TestContext.Current.CancellationToken);

        delivery.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await replay.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        (await replay.GetHandleOrThrow().GetSnapshotAsync(
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyBranchState(int Value);

    private sealed record JourneyOutput(int Value);

    private static DurableWorkflowDefinition<JourneyInput> VersionedDefinition(
        DefinitionId definitionId,
        EventContractVersion eventContractVersion,
        EventName eventName,
        CorrelationId correlation) =>
        Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(WorkflowEventContract.Create(eventName, eventContractVersion), _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
}
