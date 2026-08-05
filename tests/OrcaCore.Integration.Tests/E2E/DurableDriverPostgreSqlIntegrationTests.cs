using System.Collections.Concurrent;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.E2E;

/// <summary>
/// DR-P2 gate on PostgreSQL: the driver's restart-safe continuation signal works against the
/// real store Ã¢â‚¬â€ kind-partitioned claims, continue records in commit batches, and a second
/// host's pump finishing work the committing host never continued.
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "PostgreSql")]
public sealed class DurableDriverPostgreSqlIntegrationTests(PostgreSqlOrcaFixture fixture)
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = string.Empty;
    }

    private sealed class CountingStep(string name) : IStep<OrderState>
    {
        internal static readonly ConcurrentDictionary<string, int> Executions = new();

        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            Executions.AddOrUpdate($"{context.State.OrderId}:{name}", 1, (_, count) => count + 1);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed record HostHandle(
        DurableWorkflowRuntime Runtime,
        PostgreSqlWorkflowStore Store,
        DurableCommandProcessor Processor,
        DurableContinuationPump Pump);

    private async Task<HostHandle> CreateHostAsync(WorkflowDefinition<OrderState> definition)
    {
        var store = await fixture.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            new JsonWorkflowPayloadSerializer());
        runtime.RegisterDefinition(definition);
        var pump = new DurableContinuationPump(store, runtime, processor);
        return new HostHandle(runtime, store, processor, pump);
    }

    private static WorkflowDefinition<OrderState> Definition(DefinitionId definitionId)
    {
        return global::OrcaCore.Workflow.Durable<OrderState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(orderId => new OrderState { OrderId = orderId ?? "unset" })
            .Then(() => new CountingStep("prepare"))
            .Wait("Approved", state => CorrelationId.Create(state.OrderId))
            .Then(() => new CountingStep("ship"))
            .End("shipped")
            .Build();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-DRV-001")]
    [Trait("AC", "DR-AC-003")]
    [Trait("AC", "DR-AC-004")]
    public async Task INT_DRV_001_ContinuationPumpCompletesAfterHostDeathOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var definitionId = DefinitionId.New();
        var orderId = $"pg-drv-{Guid.CreateVersion7():N}";

        var hostA = await CreateHostAsync(Definition(definitionId));
        await using var storeA = hostA.Store;
        var start = await hostA.Runtime.StartOrGetAsync<string, OrderState>(
            orderId,
            definitionId,
            DefinitionVersion.Initial,
            orderId,
            TestContext.Current.CancellationToken);
        CountingStep.Executions.GetValueOrDefault($"{orderId}:prepare").Should().Be(1);

        // The wait-matched commit lands via the kernel; host A dies before continuing.
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var delivered = await hostA.Processor.ProcessAsync(
            new DeliverEventCommand
            {
                CommandId = CommandId.New(),
                InstanceId = start.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                Envelope = new EventEnvelope
                {
                    EventId = eventId,
                    EventName = "Approved",
                    CorrelationId = CorrelationId.Create(orderId),
                    OccurredAt = DateTimeOffset.UtcNow
                }
            },
            TestContext.Current.CancellationToken);
        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);

        // The same event is redelivered while the continuation is pending: inbox dedup.
        var duplicate = await hostA.Processor.ProcessAsync(
            new DeliverEventCommand
            {
                CommandId = CommandId.New(),
                InstanceId = start.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                Envelope = new EventEnvelope
                {
                    EventId = eventId,
                    EventName = "Approved",
                    CorrelationId = CorrelationId.Create(orderId),
                    OccurredAt = DateTimeOffset.UtcNow
                }
            },
            TestContext.Current.CancellationToken);
        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);

        var hostB = await CreateHostAsync(Definition(definitionId));
        await using var storeB = hostB.Store;
        var processed = await hostB.Pump.PumpOnceAsync(
            new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        processed.Should().BeGreaterThan(0, "the second host's pump claims the pending continuation");

        var snapshots = await hostB.Store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = start.InstanceId },
            TestContext.Current.CancellationToken);
        var snapshot = snapshots.Should().ContainSingle().Subject;
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("shipped");
        CountingStep.Executions.GetValueOrDefault($"{orderId}:ship").Should().Be(
            1, "inbox dedup + continuation idempotence yield exactly one step execution");

        // The external dispatcher partition must never see internal continuation records.
        var externalClaims = await hostB.Store.ClaimAsync(
            new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5))
            {
                KindSelector = OutboxKindSelector.Excluding(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);
        externalClaims.Should().OnlyContain(record => record.Kind != OutboxKinds.Continue);
    }
}
