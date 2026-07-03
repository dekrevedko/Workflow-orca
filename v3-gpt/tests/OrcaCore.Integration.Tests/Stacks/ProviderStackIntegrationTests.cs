using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.Providers.RabbitMq;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Stacks;

[Collection(nameof(OrcaStackCollection))]
[Trait(Traits.Category, Traits.Integration)]
public sealed class ProviderStackIntegrationTests(OrcaStackFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-ST-001")]
    [Trait("AC", "AC-310")]
    public async Task INT_ST_001_PostgreSqlCommit_OutboxPump_RabbitMqDelivery()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = fixture.RabbitMq.CreateDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(1), RabbitMqOrcaFixture.RoutingKey, [4, 5, 6])),
            TestContext.Current.CancellationToken);

        var dispatched = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var message = await fixture.RabbitMq.BasicGetAsync(TestContext.Current.CancellationToken);

        dispatched.Should().Be(1);
        message.Should().NotBeNull();
        message!.Body.ToArray().Should().Equal([4, 5, 6]);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-002")]
    [Trait("AC", "DU-032")]
    public async Task INT_ST_002_RetryableDispatchLeavesOutboxRetryable()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = new RabbitMqMessageDispatcher(
            new RabbitMqClientPublisher(new RabbitMqMessageDispatcherOptions
            {
                ConnectionString = "amqp://guest:guest@127.0.0.1:1/",
                ExchangeName = RabbitMqOrcaFixture.ExchangeName,
                Mandatory = true
            }));
        var pump = new DurableOutboxPump(store, dispatcher);
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(2), RabbitMqOrcaFixture.RoutingKey, [1])),
            TestContext.Current.CancellationToken);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(IntegrationIds.Outbox(2), TestContext.Current.CancellationToken);

        state.Value.Should().Be(OutboxRecordState.Retryable);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-006")]
    [Trait("AC", "EV-050")]
    public async Task INT_ST_006_TimerOnPostgreSql_FiredThroughProcessor()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ScheduleTimer(1, 10, 2, IntegrationIds.Timestamp(0)),
            TestContext.Current.CancellationToken);

        var due = await store.ClaimDueAsync(IntegrationIds.Timestamp(1), 10, TestContext.Current.CancellationToken);
        foreach (var command in due)
        {
            await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        }

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-013")]
    public async Task INT_ST_013_SqlServerStack_BlockedUntilRealStore()
    {
        await Task.CompletedTask;
        Assert.Skip("SqlServer provider stack blocked until real SQL persistence (R5 P0).");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-005")]
    public async Task INT_ST_005_RedisProjectionCrossProcess_BlockedUntilRealRedis()
    {
        await Task.CompletedTask;
        Assert.Skip("RedisProjectionStore does not persist to Redis yet (R5 P1).");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-003")]
    [Trait("AC", "DU-032")]
    public async Task INT_ST_003_RabbitMqPermanentFailure_PoisonsOutbox()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = fixture.RabbitMq.CreateDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(3), "missing.route", [9])),
            TestContext.Current.CancellationToken);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(IntegrationIds.Outbox(3), TestContext.Current.CancellationToken);
        state.Value.Should().Be(OutboxRecordState.Poisoned);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-004")]
    [Trait("AC", "DU-032")]
    public async Task INT_ST_004_PublisherConfirmBeforeDispatched_OnRealBroker()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = fixture.RabbitMq.CreateDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(4), RabbitMqOrcaFixture.RoutingKey, [7, 8])),
            TestContext.Current.CancellationToken);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(IntegrationIds.Outbox(4), TestContext.Current.CancellationToken);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-007")]
    [Trait("AC", "AC-521")]
    public async Task INT_ST_007_ResourcePoolExpiryThroughOperationalSweep()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                IntegrationIds.Instance(1),
                "holder",
                [IntegrationCommands.Requirement("db")],
                IntegrationIds.Timestamp(0),
                IntegrationIds.Timestamp(1)),
            TestContext.Current.CancellationToken);

        var result = await pools.ExpireTicketsAsync(IntegrationIds.Timestamp(2), TestContext.Current.CancellationToken);
        result.ExpiredTickets.Should().NotBeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-008")]
    [Trait("AC", "AC-314")]
    public async Task INT_ST_008_RetentionPurgeRejectsActiveInstance()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var purge = await store.PurgeAsync(IntegrationIds.Instance(1), TestContext.Current.CancellationToken);
        purge.Purged.Should().BeFalse();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-009")]
    public async Task INT_ST_009_ZeroMqDispatcherStack_BlockedUntilProfileExists()
    {
        await Task.CompletedTask;
        Assert.Skip("ZeroMQ dispatcher host profile not configured in integration harness.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-010")]
    [Trait("AC", "DU-033")]
    public async Task INT_ST_010_MultiDispatcherRouting_BlockedUntilRouterExists()
    {
        await Task.CompletedTask;
        Assert.Skip("Outbox kind router not implemented in host yet.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-011")]
    public async Task INT_ST_011_PostgreSqlAndRabbitMqContainersStartTogether()
    {
        fixture.PostgreSql.ConnectionString.Should().NotBeNullOrWhiteSpace();
        fixture.RabbitMq.ConnectionString.Should().NotBeNullOrWhiteSpace();
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var message = await fixture.RabbitMq.BasicGetAsync(TestContext.Current.CancellationToken);
        message.Should().BeNull();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-012")]
    public async Task INT_ST_012_ConnectionStringRotation_BlockedInTestHarness()
    {
        await Task.CompletedTask;
        Assert.Skip("Connection string rotation simulation not implemented in Testcontainers fixture.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-014")]
    [Trait("AC", "PR-040")]
    public async Task INT_ST_014_InMemoryDispatcherWithPostgreSqlStore_MisconfigurationAllowed()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.PostgreSql.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        host.Services.GetRequiredService<IWorkflowEventStore>()
            .Should().BeOfType<OrcaCore.Providers.PostgreSql.PostgreSqlWorkflowStore>();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-ST-015")]
    [Trait("AC", "AC-509")]
    public async Task INT_ST_015_LifecycleOutboxDeliveredToRabbitMq()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = fixture.RabbitMq.CreateDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Complete(1, 2),
            TestContext.Current.CancellationToken);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var message = await fixture.RabbitMq.BasicGetAsync(TestContext.Current.CancellationToken);
        message.Should().NotBeNull();
    }
}
