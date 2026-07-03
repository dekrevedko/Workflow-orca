using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class ExecutionLaneTests
{
    private static readonly DefinitionId DefinitionId = new(Guid.Parse("22222222-2222-7222-8222-222222222222"));
    private static readonly DefinitionVersion Version = new(1);

    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    private sealed class RecordingStep(int increment) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Total += increment;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task RunAsync_ConcurrentCallsForSameInstance_DoNotOverlap()
    {
        var lane = new InstanceExecutionLane();
        var instanceId = InstanceId.New();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = lane.RunAsync(instanceId, async cancellationToken =>
        {
            firstStarted.TrySetResult();
            await firstRelease.Task.WaitAsync(cancellationToken);
        }, TestContext.Current.CancellationToken);

        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        secondStarted.Task.IsCompleted.Should().BeFalse("the lane must not admit a second mutation until the first exits");

        var second = lane.RunAsync(instanceId, cancellationToken =>
        {
            secondStarted.TrySetResult();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        firstRelease.TrySetResult();
        await Task.WhenAll(first, second);
        await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_OperationsForDifferentInstances_CanOverlap()
    {
        var lane = new InstanceExecutionLane();
        var firstInstanceId = InstanceId.New();
        var secondInstanceId = InstanceId.New();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = lane.RunAsync(firstInstanceId, async cancellationToken =>
        {
            firstStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }, TestContext.Current.CancellationToken);

        var second = lane.RunAsync(secondInstanceId, async cancellationToken =>
        {
            secondStarted.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(TestContext.Current.CancellationToken);

        release.TrySetResult();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task StartAsync_ConcurrentStartsForDifferentInstances_AllComplete()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new RecordingStep(1))
            .End("Done")
            .Build(DefinitionId, Version);

        var engine = new EphemeralWorkflowEngine(clock.Provider);
        engine.RegisterDefinition(definition);

        var starts = Enumerable.Range(0, 8)
            .Select(_ => engine.StartAsync<int, OrderState>(DefinitionId, 10, TestContext.Current.CancellationToken))
            .ToArray();

        var snapshots = await Task.WhenAll(starts);

        snapshots.Should().AllSatisfy(snapshot =>
        {
            snapshot.Status.Should().Be(WorkflowStatus.Completed);
            snapshot.EndOutcomeName.Should().Be("Done");
        });
        snapshots.Select(snapshot => snapshot.InstanceId).Distinct().Should().HaveCount(8);
    }

    [Fact]
    public async Task RunAsync_WhenOperationThrows_ReleasesLaneForNextOperation()
    {
        var lane = new InstanceExecutionLane();
        var instanceId = InstanceId.New();

        var failing = () => lane.RunAsync(
            instanceId,
            _ => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        await failing.Should().ThrowAsync<InvalidOperationException>();

        var ran = false;
        await lane.RunAsync(instanceId, _ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        ran.Should().BeTrue("a failed mutation must not permanently block the lane");
    }
}
