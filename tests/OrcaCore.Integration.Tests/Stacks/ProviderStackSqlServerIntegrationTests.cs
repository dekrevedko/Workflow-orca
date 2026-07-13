using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Stacks;

[Collection(nameof(SqlServerCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "SqlServer")]
[Trait(Traits.Container, "RabbitMq")]
public sealed class ProviderStackSqlServerIntegrationTests(SqlServerOrcaFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-ST-013")]
    public async Task INT_ST_013_SqlServerOutboxAndTimerStack()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var rabbit = new RabbitMqOrcaFixture();
        await rabbit.InitializeAsync();
        try
        {
            await using var store = await fixture.CreateStoreAsync();
            var processor = new DurableCommandProcessor(store);
            var pump = new DurableOutboxPump(store, rabbit.CreateDispatcher());
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await processor.ProcessAsync(
                IntegrationCommands.ScheduleTimer(1, 10, 2, IntegrationIds.Timestamp(0)),
                TestContext.Current.CancellationToken);
            await store.AppendAsync(
                IntegrationCommands.OutboxOnlyBatch(
                    2,
                    new OutboxWrite(IntegrationIds.Outbox(1), RabbitMqOrcaFixture.RoutingKey, [4, 5, 6])),
                TestContext.Current.CancellationToken);

            var dispatched = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
            var messages = new List<byte[]>();
            for (var i = 0; i < dispatched; i++)
            {
                if (await rabbit.BasicGetAsync(TestContext.Current.CancellationToken) is { } message)
                {
                    messages.Add(message.Body.ToArray());
                }
            }

            var due = await store.ClaimDueAsync(
                IntegrationIds.Timestamp(1),
                10,
                TestContext.Current.CancellationToken);
            foreach (var command in due)
            {
                await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
            }

            await store.UpsertPoolAsync(
                IntegrationCommands.Pool("sql-db", 1),
                TestContext.Current.CancellationToken);
            var acquired = await store.AcquireAsync(
                new ResourcePoolAcquireRequest(
                    IntegrationIds.Instance(3),
                    "sql-holder",
                    [IntegrationCommands.Requirement("sql-db")],
                    IntegrationIds.Timestamp(3),
                    IntegrationIds.Timestamp(30)),
                TestContext.Current.CancellationToken);
            await using var restartedStore = await fixture.CreateStoreAsync();
            var restartedPool = await restartedStore.GetPoolAsync("sql-db", TestContext.Current.CancellationToken);

            var events = await store.LoadTailAsync(
                new WorkflowStreamId(IntegrationIds.Instance(1)),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken);
            dispatched.Should().BeGreaterThanOrEqualTo(1);
            messages.Exists(payload => payload.Length == 3 && payload[0] == 4 && payload[1] == 5 && payload[2] == 6)
                .Should().BeTrue();
            events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle();
            acquired.Status.Should().Be(ResourcePoolAcquireStatus.Granted);
            restartedPool.HasValue.Should().BeTrue();
            restartedPool.Value.HeldTickets.Should().ContainSingle()
                .Which.HolderKey.Should().Be("sql-holder");
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }
}
