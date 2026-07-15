using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class FiberReducerTests
{
    [Fact]
    public void FiberLifecycle_TransitionsThroughRunnableBlockedAndTerminalPhases()
    {
        var runnable = FiberRecord.CreateRoot(
            InstanceId.New(),
            generation: 0,
            new InstructionId("instruction:root/0:Init"));

        var blocked = FiberReducer.Block(
            runnable,
            FiberBlockedReason.Wait,
            obligationId: "wait-1");
        var resumed = FiberReducer.Resume(blocked);
        var completed = FiberReducer.Complete(resumed, resultPayload: [1, 2, 3]);
        var failed = FiberReducer.Fail(runnable, new FiberFailure("step-failed", "Step failed."));
        var cancelled = FiberReducer.Cancel(blocked, "scope-cancelled");

        runnable.Phase.Should().Be(FiberPhase.Runnable);
        blocked.Phase.Should().Be(FiberPhase.Blocked);
        blocked.Blocked.Should().Be(new FiberBlock(FiberBlockedReason.Wait, "wait-1"));
        resumed.Phase.Should().Be(FiberPhase.Runnable);
        resumed.Blocked.Should().BeNull();
        completed.Phase.Should().Be(FiberPhase.Completed);
        completed.ResultPayload.Should().Equal(1, 2, 3);
        failed.Phase.Should().Be(FiberPhase.Failed);
        failed.Failure.Should().Be(new FiberFailure("step-failed", "Step failed."));
        cancelled.Phase.Should().Be(FiberPhase.Cancelled);
        cancelled.CancellationReason.Should().Be("scope-cancelled");
    }
}
