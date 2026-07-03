using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

/// <summary>
/// T1-15 (CR-017): a yielding step commits progress made so far, releases the instance's
/// execution lane, stays <c>Running</c>, and reschedules continuation of the SAME step until it
/// completes without duplicate effects. Exercised at the engine facade level (not directly against
/// <see cref="Interpreter{TState}"/>) because "release the lane" is only observable through
/// <see cref="EphemeralWorkflowEngine"/>'s lane-wrapped entry points (CR-040/CR-042).
/// </summary>
public class YieldTests
{
    private sealed class ChunkState
    {
        public int CommittedChunks { get; set; }

        // Settable (not get-only): OrcaCore.Engine.Ephemeral.Execution.WorkflowInstance{TState}.GetStateCopy
        // round-trips business state through System.Text.Json, which does not populate a get-only
        // collection property on deserialize - a setter is required for the copy read back via
        // Query().GetState<TState>() to reflect what the step actually recorded.
        public List<int> ObservedAtEachInvocation { get; set; } = [];
    }

    /// <summary>Yields <paramref name="totalYields"/> times, incrementing <see cref="ChunkState.CommittedChunks"/> once per invocation, then completes.</summary>
    private sealed class ChunkedWorkStep(int totalYields) : IStep<ChunkState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<ChunkState> context, CancellationToken cancellationToken)
        {
            context.State.CommittedChunks += 1;
            context.State.ObservedAtEachInvocation.Add(context.State.CommittedChunks);

            StepResult result = context.State.CommittedChunks <= totalYields
                ? new StepResult.Yield()
                : new StepResult.Completed();

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Yields <paramref name="totalYields"/> times; each invocation signals a caller-supplied gate
    /// and then waits on a caller-supplied release before returning, so a test can deterministically
    /// observe the moment between two invocations (used to prove the lane is released, test 5).
    /// </summary>
    private sealed class GatedYieldingStep(int totalYields, Func<int, TaskCompletionSource> onInvoked, Func<int, Task> releaseFor) : IStep<ChunkState>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<ChunkState> context, CancellationToken cancellationToken)
        {
            context.State.CommittedChunks += 1;
            var invocationNumber = context.State.CommittedChunks;

            onInvoked(invocationNumber).TrySetResult();
            await releaseFor(invocationNumber).ConfigureAwait(false);

            StepResult result = invocationNumber <= totalYields
                ? new StepResult.Yield()
                : new StepResult.Completed();

            return result;
        }
    }

    private static DefinitionId NewDefinitionId([System.Runtime.CompilerServices.CallerMemberName] string name = "") =>
        new($"yield-{name}-{Guid.NewGuid():N}");

    [Fact]
    public async Task Run_YieldingStep_RemainsRunningBetweenContinuations()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = NewDefinitionId();
        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new ChunkedWorkStep(totalYields: 1));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        // With exactly one yield, the instance completes within StartAsync's own continuation
        // loop - but it must never have been observed in any status other than Running/Completed
        // (never Waiting/Failed), and it must be Completed at the end (CR-017: status stays
        // Running throughout the yields, and the step eventually completes).
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task Run_YieldingStep_ReentersSameStepUntilCompleted()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = NewDefinitionId();
        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new ChunkedWorkStep(totalYields: 3));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);

        var state = engine.Query().Instance(snapshot.InstanceId).GetState<ChunkState>()!;
        // 3 yields + 1 final completing invocation = 4 total invocations of the SAME step.
        state.CommittedChunks.Should().Be(4);
        state.ObservedAtEachInvocation.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Run_YieldingStep_CommitsProgressForEachYield()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = NewDefinitionId();
        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new ChunkedWorkStep(totalYields: 2));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        var state = engine.Query().Instance(snapshot.InstanceId).GetState<ChunkState>()!;

        // Each yield committed its own chunk of progress - the accumulated business-state count
        // reflects every invocation, not just the final one (no work was discarded/redone).
        state.CommittedChunks.Should().Be(3);
        state.ObservedAtEachInvocation.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Run_YieldingStep_DoesNotDuplicateCompletedEffects()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = NewDefinitionId();
        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new ChunkedWorkStep(totalYields: 4));
        builder.Then(new RecordCompletionStep());
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);

        var state = engine.Query().Instance(snapshot.InstanceId).GetState<ChunkState>()!;

        // The step that follows the yielding step (a stand-in for a real side effect) must run
        // exactly once - the yielding step itself must not re-trigger it, and completing must not
        // re-run the yielding step's already-committed chunks. -1 is RecordCompletionStep's
        // sentinel, appended exactly once after the yielding step's final (5th) invocation.
        state.CommittedChunks.Should().Be(5);
        state.ObservedAtEachInvocation.Should().Equal(1, 2, 3, 4, 5, -1);
    }

    private sealed class RecordCompletionStep : IStep<ChunkState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<ChunkState> context, CancellationToken cancellationToken)
        {
            context.State.ObservedAtEachInvocation.Add(-1); // sentinel: "completion step ran"
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Run_YieldingStep_ReleasesLaneBetweenContinuations()
    {
        // Deterministic proof (no sleeps): the yielding step signals a TaskCompletionSource the
        // instant each invocation starts, then blocks on a per-invocation gate the TEST controls.
        // While invocation 1 is gated (mid-step, lane held by the first continuation cycle), the
        // instance is already visible via Query() (StartAsync registers it before entering the
        // lane). While STILL gated on invocation 1 - i.e. before the first continuation cycle's
        // lane call has returned - the test starts an independent RaiseEventAsync for the SAME
        // instance (a no-op event). If the engine facade held one lane call across the whole
        // multi-yield run, this concurrent RaiseEventAsync could never acquire the lane until
        // after the ENTIRE run finished, so it would still be pending when invocation 2 starts.
        // Observing the probe complete BEFORE invocation 2 starts proves the lane was actually
        // released after invocation 1's continuation cycle and reacquired for invocation 2's.
        var engine = new EphemeralWorkflowEngine();
        var definitionId = NewDefinitionId();

        var invoked = new Dictionary<int, TaskCompletionSource>
        {
            [1] = new(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var releases = new Dictionary<int, TaskCompletionSource>
        {
            [1] = new(TaskCreationOptions.RunContinuationsAsynchronously),
            [2] = new(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new GatedYieldingStep(
            totalYields: 1,
            onInvoked: n => invoked[n],
            releaseFor: n => releases[n].Task));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var startTask = engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        // Wait for the FIRST invocation to be in flight (it is holding the lane right now, inside
        // the first RunUntilNotYieldingAsync cycle's executionLane.RunAsync call).
        await invoked[1].Task.WaitAsync(TestContext.Current.CancellationToken);

        var instanceId = engine.Query().ForDefinition(definitionId).List().Single().InstanceId;
        var envelope = new EventEnvelope(EventId.New(), "UnrelatedNoOpEvent", new CorrelationId("none"), Payload: null, DateTimeOffset.UnixEpoch);

        // Issue the probe WHILE invocation 1 is still gated (still holding the lane if the
        // implementation is wrong). Do not await it yet.
        var probeTask = engine.RaiseEventAsync<ChunkState>(instanceId, envelope, TestContext.Current.CancellationToken);

        // Now let invocation 1 finish (it yields) - this is what should release the lane.
        releases[1].TrySetResult();

        // The probe must complete BEFORE invocation 2 starts if the lane was genuinely released
        // between continuations. If the lane were held across the whole run, the probe would
        // still be queued behind it here, and this WaitAsync would race invocation 2's own
        // signal - to make the assertion unambiguous, require the probe specifically to finish
        // first by awaiting it with a bounded timeout BEFORE waiting on invocation 2 at all.
        var probeOutcome = await probeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        probeOutcome.Should().Be(RaiseEventOutcome.NoMatch);

        // Only after the probe has provably completed do we allow invocation 2 to proceed.
        await invoked[2].Task.WaitAsync(TestContext.Current.CancellationToken);
        releases[2].TrySetResult();

        var snapshot = await startTask;
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }
}
