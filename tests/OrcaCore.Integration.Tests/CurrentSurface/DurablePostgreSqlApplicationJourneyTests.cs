using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting;
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
    public async Task CorrelationEventAcceptedBeforeStart_IsAppliedWhenPostgreSqlWaitRegisters()
    {
        await using var host = await DurableTestHosts.StartPostgreSqlAsync(
            container.GetConnectionString(),
            TestContext.Current.CancellationToken);
        var definitionId = DefinitionId.New();
        var eventContract = WorkflowEventContract.Create(
            EventName.Create("postgres-before-wait"),
            new EventContractVersion(2));
        var correlation = CorrelationId.Create("postgres-before-wait");
        var definition = Workflow.Durable<JourneyState>(definitionId, DefinitionVersion.Initial)
            .Init<JourneyInput>(input => new JourneyState(input.Value))
            .Wait(eventContract, _ => correlation)
            .End(WorkflowOutcomeName.Create("finished"))
            .Build();
        var handle = host.Services.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var eventId = EventId.Create("postgres-before-wait");

        var accepted = await host.Services.GetRequiredService<IWorkflowEventIngress>().AcceptAsync(
            WorkflowInboundEvent.Create(
                eventContract,
                eventId,
                correlation,
                causationEventId: null,
                DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
                new WorkflowEventRoute.Correlation(definitionId)),
            TestContext.Current.CancellationToken);
        var started = await handle.StartOrGetAsync(
            new JourneyInput(1),
            StartIdempotencyKey.Create("postgres-before-wait"),
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await started.GetHandleOrThrow().GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        var inbox = await host.Services.GetRequiredService<IWorkflowInboxStore>()
            .GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);
        inbox.Value.State.Should().Be(InboxRecordState.Applied);
        inbox.Value.InstanceId.Should().Be(started.GetHandleOrThrow().InstanceId);
        inbox.Value.AcceptanceSequence.Should().BePositive();
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
            .Wait(WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial), _ => correlation)
            .End(
                state => new JourneyOutput(state.Value.Value + 1),
                WorkflowOutcomeName.Create("finished"))
            .Build();
        var startKey = StartIdempotencyKey.Create("durable-postgresql-replacement");
        InstanceId firstInstanceId;

        await using (var firstHost = await DurableTestHosts.StartPostgreSqlAsync(
                         container.GetConnectionString(),
                         TestContext.Current.CancellationToken))
        {
            var definitions = firstHost.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
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
            await DurableTestHosts.StartPostgreSqlAsync(
                container.GetConnectionString(),
                TestContext.Current.CancellationToken);
        var replacementDefinitions =
            replacementHost.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
        var replacementHandle = replacementDefinitions.Register(definition).GetHandleOrThrow();
        var ingress = replacementHost.Services.GetRequiredService<IWorkflowEventIngress>();

        var replay = await replacementHandle.StartOrGetAsync(
            new JourneyInput(41),
            startKey,
            TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<JourneyOutput>>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(firstInstanceId);

        var pendingEventId = EventId.Create("durable-postgresql-pending-before-terminal");
        var pending = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(
                    EventName.Create("not-currently-awaited"),
                    EventContractVersion.Initial),
                pendingEventId,
                CorrelationId.Create("durable-postgresql-pending-before-terminal"),
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(firstInstanceId)),
            TestContext.Current.CancellationToken);

        var delivery = await ingress.AcceptAsync(
                WorkflowInboundEvent.Create(
                    WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial),
                    EventId.Create("durable-postgresql-continue"),
                    correlation,
                    causationEventId: null,
                    DateTimeOffset.UtcNow,
                    new WorkflowEventRoute.Direct(firstInstanceId)),
                TestContext.Current.CancellationToken);

        delivery.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        (await replay.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new JourneyOutput(42));
        var completed = await replay.GetHandleOrThrow().GetSnapshotAsync(
            TestContext.Current.CancellationToken);
        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        completed.Outcome.Should().Be(WorkflowOutcomeName.Create("finished"));
        pending.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        var inboxStore = replacementHost.Services.GetRequiredService<IWorkflowInboxStore>();
        var poisoned = await inboxStore.GetByEventIdAsync(
            pendingEventId,
            TestContext.Current.CancellationToken);
        poisoned.Value.State.Should().Be(InboxRecordState.Poisoned);
        poisoned.Value.PoisonCode.Should().Be("target-terminal");

        var providerRejectedEventId = EventId.Create("durable-postgresql-provider-after-terminal");
        var providerRejected = await inboxStore.AcceptAsync(
            new InboxAcceptance(
                new DurableEventEnvelope
                {
                    EventId = providerRejectedEventId,
                    EventName = continueEvent.Value,
                    EventContractVersion = EventContractVersion.Initial.Value,
                    CorrelationId = correlation,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Route = new DurableEventRouteEnvelope
                    {
                        Kind = "direct",
                        InstanceId = firstInstanceId
                    }
                },
                "durable-postgresql-provider-after-terminal",
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);
        providerRejected.Disposition.Should().Be(InboxAcceptanceCommitDisposition.DirectInstanceTerminal);
        (await inboxStore.GetByEventIdAsync(
                providerRejectedEventId,
                TestContext.Current.CancellationToken))
            .HasValue.Should().BeFalse();

        var afterTerminal = await ingress.AcceptAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(continueEvent, EventContractVersion.Initial),
                EventId.Create("durable-postgresql-after-terminal"),
                correlation,
                causationEventId: null,
                DateTimeOffset.UtcNow,
                new WorkflowEventRoute.Direct(firstInstanceId)),
            TestContext.Current.CancellationToken);
        afterTerminal.Should().BeOfType<WorkflowEventAcceptanceResult.Rejected>()
            .Which.Reason.Should().BeOfType<WorkflowEventAcceptanceRejection.DirectInstanceTerminal>();
    }

    private sealed record JourneyInput(int Value);

    private sealed record JourneyState(int Value);

    private sealed record JourneyOutput(int Value);
}
