using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Concurrency;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class ExecutionLaneTests
{
    [Fact]
    public async Task RunAsync_ConcurrentCallsForSameInstance_DoNotOverlap()
    {
        var lane = new InstanceExecutionLane();
        var instanceId = InstanceId.New();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSubmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstMayExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var log = new List<string>();
        var gate = new object();

        var firstTask = lane.RunAsync(
            instanceId,
            async () =>
            {
                lock (gate)
                {
                    log.Add("first-enter");
                }

                firstEntered.TrySetResult();

                // Prove non-overlap deterministically: the second operation must have already
                // been *submitted* to the lane (and be waiting on the gate) before the first
                // operation is allowed to exit. If the lane let them interleave, "second-enter"
                // could appear before "first-exit" below.
                await secondSubmitted.Task.ConfigureAwait(false);
                await firstMayExit.Task.ConfigureAwait(false);

                lock (gate)
                {
                    log.Add("first-exit");
                }
            },
            TestContext.Current.CancellationToken).AsTask();

        await firstEntered.Task;

        var secondTask = lane.RunAsync(
            instanceId,
            () =>
            {
                lock (gate)
                {
                    log.Add("second-enter");
                }

                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken).AsTask();

        secondSubmitted.TrySetResult();
        firstMayExit.TrySetResult();

        await Task.WhenAll(firstTask, secondTask);

        log.Should().Equal("first-enter", "first-exit", "second-enter");
    }

    [Fact]
    public async Task RunAsync_OperationsForDifferentInstances_CanOverlap()
    {
        var lane = new InstanceExecutionLane();
        var firstInstanceId = InstanceId.New();
        var secondInstanceId = InstanceId.New();
        var coordinator = new RaceCoordinator();

        var firstTask = lane.RunAsync(
            firstInstanceId,
            async () => await coordinator.ArriveAsync(TestContext.Current.CancellationToken).ConfigureAwait(false),
            TestContext.Current.CancellationToken).AsTask();

        var secondTask = lane.RunAsync(
            secondInstanceId,
            async () => await coordinator.ArriveAsync(TestContext.Current.CancellationToken).ConfigureAwait(false),
            TestContext.Current.CancellationToken).AsTask();

        // Both operations must reach the rendezvous concurrently - if they were serialized by a
        // shared lane, the second RunAsync call would never start until the first completed, and
        // RaceCoordinator.ArriveAsync would time out waiting for its partner.
        await Task.WhenAll(firstTask, secondTask);
    }

    [Fact]
    public async Task StartAsync_ConcurrentStartsForDifferentInstances_AllComplete()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("lane-smoke-workflow");
        var builder = WorkflowBuilder<int>.Create<int>(input => input);
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var starts = Enumerable.Range(0, 8)
            .Select(i => engine.StartAsync<int, int>(definitionId, i, TestContext.Current.CancellationToken))
            .ToArray();

        var snapshots = await Task.WhenAll(starts);

        snapshots.Should().HaveCount(8);
        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
        snapshots.Select(snapshot => snapshot.InstanceId).Distinct().Should().HaveCount(8);
    }

    [Fact]
    public async Task RunAsync_WhenOperationThrows_ReleasesLaneForNextOperation()
    {
        var lane = new InstanceExecutionLane();
        var instanceId = InstanceId.New();

        var act = async () => await lane.RunAsync(
            instanceId,
            () => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var ran = false;
        await lane.RunAsync(
            instanceId,
            () =>
            {
                ran = true;
                return ValueTask.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        ran.Should().BeTrue();
    }
}
