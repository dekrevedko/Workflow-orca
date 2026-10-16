using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Engine;

[Collection(nameof(SqlServerCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "SqlServer")]
public sealed class EngineSqlServerIntegrationTests(SqlServerOrcaFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-EP-013")]
    public async Task INT_EP_013_SqlServerDurableWaitSurvivesProcessorRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        await using var restartedStore = await fixture.CreateStoreAsync();
        var restarted = await fixture.CreateProcessorAsync(restartedStore);
        var result = await restarted.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var events = await restartedStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }
}
