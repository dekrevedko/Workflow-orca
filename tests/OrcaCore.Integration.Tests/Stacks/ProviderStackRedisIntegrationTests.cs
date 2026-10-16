using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Stacks;

[Collection(nameof(OrcaStackCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "PostgreSql")]
[Trait(Traits.Container, "Redis")]
public sealed class ProviderStackRedisIntegrationTests(OrcaStackFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-ST-005")]
    [Trait("AC", "PR-013")]
    public async Task INT_ST_005_PostgreSqlProjectionCopiedToRedis_IsReadableBySeparateStore()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var redis = new RedisOrcaFixture();
        await redis.InitializeAsync();
        try
        {
            await redis.ResetAsync();
            await using var store = await fixture.PostgreSql.CreateStoreAsync();
            var processor = new DurableCommandProcessor(store);
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await processor.ProcessAsync(
                IntegrationCommands.WaitRegistered(1, 10, 2),
                TestContext.Current.CancellationToken);
            var pgSnapshots = await store.ListAsync(
                new WorkflowProjectionQuery { InstanceId = IntegrationIds.Instance(1) },
                TestContext.Current.CancellationToken);
            var snapshot = pgSnapshots.Should().ContainSingle().Subject;

            var writer = redis.CreateProjectionStore();
            var reader = redis.CreateProjectionStore();
            await writer.ApplyAsync(
                [
                    new ProjectionWrite(snapshot.InstanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = snapshot
                    }
                ],
                TestContext.Current.CancellationToken);

            var redisSnapshots = await reader.ListAsync(
                new WorkflowProjectionQuery
                {
                    ActiveWaitEventName = "Approved",
                    ActiveWaitCorrelationId = CorrelationId.Create("order-1")
                },
                TestContext.Current.CancellationToken);

            redisSnapshots.Should().ContainSingle()
                .Which.InstanceId.Should().Be(IntegrationIds.Instance(1));
        }
        finally
        {
            await redis.DisposeAsync();
        }
    }
}
