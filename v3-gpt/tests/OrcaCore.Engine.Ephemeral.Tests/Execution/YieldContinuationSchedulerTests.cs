using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Governance;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class YieldContinuationSchedulerTests
{
    [Fact]
    public async Task DrainAsync_TakesInitialSnapshotThroughInstanceLane()
    {
        var instanceId = InstanceIdValue(1);
        var executionLane = new InstanceExecutionLane();
        var scheduler = new YieldContinuationScheduler(
            new ResourceGovernanceCoordinator(new EphemeralWorkflowEngineOptions()),
            executionLane);
        var instance = new SnapshotProbeInstance(instanceId);
        var laneEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLane = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = executionLane.RunAsync(
            instanceId,
            async _ =>
            {
                laneEntered.SetResult();
                await releaseLane.Task.ConfigureAwait(false);
            },
            TestContext.Current.CancellationToken);
        await laneEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var drain = scheduler.DrainAsync(
            instance,
            instanceId,
            _ => { },
            TestContext.Current.CancellationToken);

        instance.SnapshotCalls.Should().Be(0);

        releaseLane.SetResult();
        await blocker.WaitAsync(TestContext.Current.CancellationToken);
        await drain.WaitAsync(TestContext.Current.CancellationToken);

        instance.SnapshotCalls.Should().Be(1);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class SnapshotProbeInstance(InstanceId instanceId) : IWorkflowInstance
    {
        public InstanceId InstanceId => instanceId;

        public Type StateType => typeof(object);

        public object StateObject { get; } = new();

        public int SnapshotCalls { get; private set; }

        public WorkflowInstanceSnapshot Cancel(DateTimeOffset updatedAt)
        {
            throw new NotSupportedException();
        }

        public WorkflowInstanceSnapshot Terminate(DateTimeOffset updatedAt)
        {
            throw new NotSupportedException();
        }

        public WorkflowInstanceSnapshot MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold)
        {
            throw new NotSupportedException();
        }

        public bool TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation)
        {
            continuation = null;
            return false;
        }

        public CancellationTokenSource CreateLinkedExecutionToken(CancellationToken cancellationToken)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        public void SignalCancellation()
        {
        }

        public WorkflowInstanceSnapshot ToSnapshot()
        {
            SnapshotCalls++;
            return new WorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                DefinitionId = DefinitionIdValue(1),
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Running,
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch
            };
        }
    }
}
