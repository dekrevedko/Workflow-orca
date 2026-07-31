using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;

namespace OrcaCore.Integration.Tests.CurrentSurface;

[Trait("Category", "PostgreSql")]
[Trait("Container", "PostgreSql")]
public sealed class DurablePostgreSqlApplicationJourneyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    [Fact]
    public async Task ReplacementHost_ReopensAndCompletesPersistedWorkflow()
    {
        var continueEvent = EventName.Create("continue");
        var correlation = CorrelationId.Create("durable-postgresql-replacement");
        var definition = Workflow.Durable<JourneyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(continueEvent, _ => correlation)
            .End(
                state => new JourneyOutput(state.Value.Value + 1),
                WorkflowOutcomeName.Create("finished"))
            .Build();
        var startKey = StartIdempotencyKey.Create("durable-postgresql-replacement");
        InstanceId firstInstanceId;

        await using (var firstHost =
                     DurableTestHosts.BuildPostgreSql(container.GetConnectionString()))
        {
            await InitializeProviderAsync(firstHost);
            var definitions = firstHost.GetRequiredService<IWorkflowDefinitionRegistry>();
            var handle = definitions.Register(definition).GetHandleOrThrow();
            var start = await handle.StartOrGetAsync(
                new JourneyInput(41),
                startKey,
                TestContext.Current.CancellationToken);
            start.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
                .Which.WasExisting.Should().BeFalse();
            firstInstanceId = start.GetHandleOrThrow().InstanceId;
            (await start.GetHandleOrThrow().GetSnapshotAsync(
                    TestContext.Current.CancellationToken))
                .Status.Should().Be(WorkflowInstanceStatus.Waiting);
        }

        await using var replacementHost =
            DurableTestHosts.BuildPostgreSql(container.GetConnectionString());
        await InitializeProviderAsync(replacementHost);
        var replacementDefinitions =
            replacementHost.GetRequiredService<IWorkflowDefinitionRegistry>();
        var replacementHandle = replacementDefinitions.Register(definition).GetHandleOrThrow();

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
                    EventId.Create("durable-postgresql-continue"),
                    continueEvent,
                    correlation,
                    DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EventDeliveryStatus.Accepted);
        (await replay.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        var completed = await replay.GetHandleOrThrow().GetSnapshotAsync(
            TestContext.Current.CancellationToken);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        completed.Outcome.Should().Be(WorkflowOutcomeName.Create("finished"));
    }

    private static async Task InitializeProviderAsync(ServiceProvider provider)
    {
        await provider
            .GetRequiredService<PostgreSqlWorkflowStore>()
            .InitializeAsync(TestContext.Current.CancellationToken);
        await provider
            .GetRequiredService<PostgreSqlResourcePoolStore>()
            .InitializeAsync(TestContext.Current.CancellationToken);
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyOutput(int Value);
}
