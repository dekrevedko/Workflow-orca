using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class ReferenceLinearFiberInterpreterTests
{
    [Fact]
    public async Task Quantum_InvokesAtMostOneUserStep_AndPersistsTheNextStepPosition()
    {
        var plan = TwoStepPlan();
        var state = StructuredExecutionState.Create(
            InstanceId.Parse(Guid.CreateVersion7().ToString()),
            generation: 0,
            plan.Instructions[0].Id);
        var executor = new AdvancingExecutor();

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            executor,
            TestContext.Current.CancellationToken);

        executor.StepInvocations.Should().Be(1);
        result.EndReason.Should().Be(FiberQuantumEndReason.UserStepBudget);
        var root = result.State.Fibers[state.RootFiberId];
        root.Phase.Should().Be(FiberPhase.Runnable);
        root.InstructionId.Should().Be(plan.Instructions[2].Id);
        result.State.Scheduler.NextFiberId.Should().Be(state.RootFiberId);
    }

    [Fact]
    public async Task Suspension_BlocksOnlyTheSelectedFiber_AtItsResumePosition()
    {
        var plan = TwoStepPlan();
        var state = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, plan.Instructions[0].Id);
        var executor = new SuspendingExecutor(plan.Instructions[2].Id);

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            executor,
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.Suspended);
        var root = result.State.Fibers[state.RootFiberId];
        root.Phase.Should().Be(FiberPhase.Blocked);
        root.InstructionId.Should().Be(plan.Instructions[2].Id);
        root.Blocked.Should().Be(new FiberBlock(FiberBlockedReason.Wait, "wait-1"));
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    [Fact]
    public async Task Yield_PreservesInstruction_AndRotatesToRunnableSibling()
    {
        var plan = ParallelStepPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var start = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, start.Id);
        var scope = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var yieldingFiberId = scope.ChildFiberIds[0];
        var siblingFiberId = scope.ChildFiberIds[1];
        var originalInstruction = scope.State.Fibers[yieldingFiberId].InstructionId;

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            scope.State,
            new YieldingExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.Yielded);
        var yielded = result.State.Fibers[yieldingFiberId];
        yielded.Phase.Should().Be(FiberPhase.Runnable);
        yielded.InstructionId.Should().Be(originalInstruction);
        yielded.YieldCount.Should().Be(1);
        result.State.Scheduler.NextFiberId.Should().Be(siblingFiberId);
    }

    [Fact]
    public async Task BranchReturn_CommitsResultAndRemovesCompletedFiberFromScheduling()
    {
        var plan = ParallelStepPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var start = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, start.Id);
        var scope = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var returningFiberId = scope.ChildFiberIds[0];
        var siblingFiberId = scope.ChildFiberIds[1];

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            scope.State,
            new BranchReturningExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.BranchReturned);
        var returned = result.State.Fibers[returningFiberId];
        returned.Phase.Should().Be(FiberPhase.Completed);
        returned.ResultPayload.Should().Equal(7, 8, 9);
        result.State.Scopes[scope.ScopeId].CommittedResults[returningFiberId]
            .Should().Equal(7, 8, 9);
        result.State.Scheduler.NextFiberId.Should().Be(siblingFiberId);
    }

    [Fact]
    public async Task Failure_TerminatesSelectedFiber_AndRemovesItFromScheduling()
    {
        var plan = TwoStepPlan();
        var state = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, plan.Instructions[0].Id);

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            new FailingExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.Failed);
        var root = result.State.Fibers[state.RootFiberId];
        root.Phase.Should().Be(FiberPhase.Failed);
        root.Failure.Should().Be(new FiberFailure("step-failed", "Step failed."));
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    [Fact]
    public async Task InternalInstructionLimit_PersistsProgressAndForcesSiblingRotation()
    {
        new DefinitionCompilerOptions().MaxInternalInstructionsPerQuantum.Should().Be(1024);
        var plan = InternalBudgetPlan();
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, plan.Instructions[0].Id);
        var siblingId = new FiberId("fiber:sibling");
        var sibling = initial.Fibers[initial.RootFiberId] with { Id = siblingId };
        var fibers = new Dictionary<FiberId, FiberRecord>(initial.Fibers)
        {
            [siblingId] = sibling
        };
        var state = initial with
        {
            Fibers = fibers,
            Scheduler = FiberScheduler.Create([initial.RootFiberId, siblingId])
        };

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            new AdvancingExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.InternalInstructionBudget);
        result.InternalInstructionsExecuted.Should().Be(2);
        var root = result.State.Fibers[state.RootFiberId];
        root.InstructionId.Should().Be(plan.Instructions[2].Id);
        root.ForcedRotationCount.Should().Be(1);
        result.State.Scheduler.NextFiberId.Should().Be(siblingId);
    }

    [Fact]
    public async Task StartScope_IsReducedInternally_AndEndsTheParentQuantum()
    {
        var plan = ParallelStepPlan();
        var start = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var state = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, start.Id);

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            new RejectingExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.ScopeStarted);
        result.State.Scopes.Should().ContainSingle();
        result.State.Fibers[state.RootFiberId].Phase.Should().Be(FiberPhase.Blocked);
        result.State.Scheduler.RunnableFiberIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task RootEnd_CompletesTheRootAndEmptiesTheRunnableQueue()
    {
        var plan = ImmediatePlan();
        var state = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, plan.Instructions[0].Id);

        var result = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            state,
            new CompletingExecutor(),
            TestContext.Current.CancellationToken);

        result.EndReason.Should().Be(FiberQuantumEndReason.WorkflowCompleted);
        result.State.Fibers[state.RootFiberId].Phase.Should().Be(FiberPhase.Completed);
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    private static CompiledWorkflowPlan TwoStepPlan()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState(value))
            .Then<NoOpStep>()
            .Then<NoOpStep>()
            .End()
            .Build()
            .CompiledPlan;
    }

    private static CompiledWorkflowPlan ImmediatePlan()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState(value))
            .End()
            .Build()
            .CompiledPlan;
    }

    private static CompiledWorkflowPlan ParallelStepPlan()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new TestState(value))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", parent => new BranchState(parent.Value.Value), branch => branch
                        .Then<BranchNoOpStep>()
                        .Return(state => state.Value.Value))
                    .Branch<BranchState>("second", parent => new BranchState(parent.Value.Value), branch => branch
                        .Then<BranchNoOpStep>()
                        .Return(state => state.Value.Value)),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
    }

    private static CompiledWorkflowPlan InternalBudgetPlan()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 2
            })
            .Init<string>(value => new TestState(value))
            .If(_ => true, _ => { })
            .End()
            .Build()
            .CompiledPlan;
    }

    private sealed class AdvancingExecutor : ICompiledInstructionExecutor
    {
        internal int StepInvocations { get; private set; }

        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            if (instruction.Kind == CompiledInstructionKind.Step)
            {
                StepInvocations++;
            }

            return ValueTask.FromResult<InstructionExecutionResult>(new InstructionExecutionResult.Advance());
        }
    }

    private sealed class SuspendingExecutor(InstructionId resumeAt) : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            InstructionExecutionResult result = instruction.Kind == CompiledInstructionKind.Step
                ? new InstructionExecutionResult.Suspend(FiberBlockedReason.Wait, "wait-1", resumeAt)
                : new InstructionExecutionResult.Advance();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class YieldingExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<InstructionExecutionResult>(new InstructionExecutionResult.Yield());
        }
    }

    private sealed class BranchReturningExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            InstructionExecutionResult result = instruction.Kind == CompiledInstructionKind.BranchReturn
                ? new InstructionExecutionResult.BranchReturn([7, 8, 9])
                : new InstructionExecutionResult.Advance();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailingExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            InstructionExecutionResult result = instruction.Kind == CompiledInstructionKind.Step
                ? new InstructionExecutionResult.Fail(new FiberFailure("step-failed", "Step failed."))
                : new InstructionExecutionResult.Advance();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RejectingExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Structural scope instructions must not reach the executor.");
        }
    }

    private sealed class CompletingExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            InstructionExecutionResult result = instruction.Kind == CompiledInstructionKind.End
                ? new InstructionExecutionResult.WorkflowCompleted()
                : new InstructionExecutionResult.Advance();
            return ValueTask.FromResult(result);
        }
    }

    private sealed record TestState(string Value);

    private sealed record BranchState(string Value);

    private sealed class NoOpStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BranchNoOpStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
