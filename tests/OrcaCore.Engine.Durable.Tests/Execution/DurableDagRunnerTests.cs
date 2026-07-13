using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableDagRunnerTests
{
    [Fact]
    [Trait("AC", "JS-AC-001")]
    public async Task ScheduleReadyAsync_SubmitsRunnableNodesAsDurableChildren()
    {
        var rootId = InstanceIdValue(10);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("C", DefinitionIdValue(2), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(Start(rootId), TestContext.Current.CancellationToken);

        var firstWave = await runner.ScheduleReadyAsync(
            Request(rootId, plan, completed: [], failed: []),
            TestContext.Current.CancellationToken);
        var secondWave = await runner.ScheduleReadyAsync(
            Request(rootId, plan, completed: ["A"], failed: []),
            TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Select(child => child.ItemSnapshot))
            .ToArray();

        firstWave.Should().ContainSingle()
            .Which.Batch.ItemSnapshots.Should().Equal("A");
        secondWave.Should().ContainSingle()
            .Which.Batch.ItemSnapshots.Should().Equal("B", "C");
        scheduled.Should().Equal("A", "B", "C");
    }

    [Fact]
    [Trait("AC", "JS-AC-001")]
    public async Task ScheduleReadyAsync_WhenNodesAreMarkedScheduled_DoesNotReDispatchInFlightWork()
    {
        var rootId = InstanceIdValue(11);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(Start(rootId), TestContext.Current.CancellationToken);

        // A completes, B is dispatched.
        await runner.ScheduleReadyAsync(
            new DurableDagScheduleRequest(rootId, plan, ["A"], [], Timestamp(2)),
            TestContext.Current.CancellationToken);
        // A re-drive (e.g. crash-restart) with B reconstructed as in-flight must not re-dispatch B.
        var reDrive = await runner.ScheduleReadyAsync(
            new DurableDagScheduleRequest(rootId, plan, ["A"], [], Timestamp(3), ScheduledNodeIds: ["B"]),
            TestContext.Current.CancellationToken);

        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Select(child => child.ItemSnapshot))
            .ToArray();

        reDrive.Should().BeEmpty();
        scheduled.Should().ContainSingle().Which.Should().Be("B");
    }

    private static DurableDagScheduleRequest Request(
        InstanceId rootId,
        WorkflowDagPlan plan,
        IReadOnlyCollection<string> completed,
        IReadOnlyCollection<string> failed)
    {
        return new DurableDagScheduleRequest(
            rootId,
            plan,
            completed,
            failed,
            Timestamp(2));
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 21, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
