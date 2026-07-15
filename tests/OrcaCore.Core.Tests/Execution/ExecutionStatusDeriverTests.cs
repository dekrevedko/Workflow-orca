using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class ExecutionStatusDeriverTests
{
    [Fact]
    public void AggregateRunnability_DerivesRunningWaitingAndRootTerminalStatuses()
    {
        var initial = StructuredExecutionState.Create(
            InstanceId.New(),
            generation: 0,
            new InstructionId("instruction:root"));
        var root = initial.Fibers[initial.RootFiberId];
        var waiting = WithRoot(
            initial,
            FiberReducer.Block(root, FiberBlockedReason.Wait, "wait-1"),
            runnable: false);
        var siblingId = new FiberId("fiber:sibling");
        var sibling = root with { Id = siblingId };
        var mixed = initial with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>
            {
                [root.Id] = FiberReducer.Block(root, FiberBlockedReason.Wait, "wait-1"),
                [siblingId] = sibling
            },
            Scheduler = FiberScheduler.Create([siblingId])
        };
        var completed = WithRoot(initial, FiberReducer.Complete(root), runnable: false);
        var failed = WithRoot(
            initial,
            FiberReducer.Fail(root, new FiberFailure("failed", "Failed.")),
            runnable: false);
        var cancelled = WithRoot(initial, FiberReducer.Cancel(root, "cancelled"), runnable: false);

        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, initial).Status
            .Should().Be(WorkflowStatus.Running);
        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, mixed).Status
            .Should().Be(WorkflowStatus.Running);
        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, waiting).Status
            .Should().Be(WorkflowStatus.Waiting);
        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, completed).Status
            .Should().Be(WorkflowStatus.Completed);
        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, failed).Status
            .Should().Be(WorkflowStatus.Failed);
        ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, cancelled).Status
            .Should().Be(WorkflowStatus.Cancelled);
    }

    [Fact]
    public void BlockingRuntimeDiagnostic_ParksOnlyDurableMode_AndIsTypedForEphemeralMode()
    {
        var state = StructuredExecutionState.Create(
            InstanceId.New(),
            generation: 0,
            new InstructionId("instruction:root"));
        var diagnostic = new StructuredRuntimeDiagnostic(
            "SFE-RUN-STATE-001",
            "Compiled plan binding is unavailable.");

        var durable = ExecutionStatusDeriver.Derive(
            WorkflowExecutionMode.Durable,
            state,
            diagnostic);
        var ephemeral = ExecutionStatusDeriver.Derive(
            WorkflowExecutionMode.Ephemeral,
            state,
            diagnostic);

        durable.Status.Should().Be(WorkflowStatus.Parked);
        durable.Failure.Should().BeNull();
        ephemeral.Status.Should().BeNull();
        ephemeral.Failure.Should().Be(new StructuredExecutionFailure(
            diagnostic.Code,
            diagnostic.Message));
    }

    private static StructuredExecutionState WithRoot(
        StructuredExecutionState state,
        FiberRecord root,
        bool runnable)
    {
        return state with
        {
            Fibers = new Dictionary<FiberId, FiberRecord> { [root.Id] = root },
            Scheduler = FiberScheduler.Create(runnable ? [root.Id] : [])
        };
    }
}
