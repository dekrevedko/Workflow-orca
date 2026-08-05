using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Integration.Tests.CurrentSurface;

public sealed class DurableInMemoryApplicationJourneyTests
{
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
            .Wait(continueEvent, _ => correlation)
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
            .GetRequiredService<IWorkflowEventClient>()
            .DeliverToInstanceAsync(
                firstInstanceId,
                WorkflowEvent.Create(
                    EventId.Create("durable-in-memory-continue"),
                    continueEvent,
                    correlation,
                    DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        (await replay.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        (await replay.GetHandleOrThrow().GetSnapshotAsync(
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyOutput(int Value);
}
