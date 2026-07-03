using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public class LoopWaitAcceptanceTests
{
    private const string LoopEventName = "LoopSignal";
    private static readonly CorrelationId LoopCorrelationId = new("loop-1");

    private sealed class LoopState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    /// <summary>Iteration 1 (Total == 0) waits on "OtherSignal" so a same-key "LoopSignal" event can be
    /// buffered as genuinely stale residue during its window; iteration 2+ waits on "LoopSignal".</summary>
    private sealed class FirstIterationDifferentKeyStep : IStep<LoopState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<LoopState> context, CancellationToken cancellationToken)
        {
            if (context.ResumedEvent is null)
            {
                var eventName = context.State.Total == 0 ? "OtherSignal" : LoopEventName;
                return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, LoopCorrelationId));
            }

            context.State.Total += 1;
            context.State.Executed.Add("iter");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildLoopWorkflow(DefinitionId definitionId)
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<LoopState>.Create<int>(input => new LoopState { Total = input });
        builder.While(
            state => ((LoopState)state!).Total < 2,
            body => body.Then(new FirstIterationDifferentKeyStep()));
        builder.End();
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Trait("AC", "AC-109")]
    [Fact]
    public async Task WaitInLoop_PreviousIterationEvent_CannotResumeLaterIteration()
    {
        var (engine, definitionId) = BuildLoopWorkflow(new DefinitionId("loop-wait-workflow-109"));
        var started = await engine.StartAsync<int, LoopState>(definitionId, 0, TestContext.Current.CancellationToken);

        started.Status.Should().Be(WorkflowStatus.Waiting, "iteration 1 waits on OtherSignal");

        // A "LoopSignal" event arrives while iteration 1's wait (OtherSignal) is active. It does
        // not match iteration 1's key, so it must buffer rather than resume anything.
        var staleLoopSignal = new EventEnvelope(EventId.New(), LoopEventName, LoopCorrelationId, "stale-from-iter1", DateTimeOffset.UnixEpoch);
        var bufferOutcome = await engine.RaiseEventAsync<LoopState>(started.InstanceId, staleLoopSignal, TestContext.Current.CancellationToken);
        bufferOutcome.Should().Be(RaiseEventOutcome.NoMatch);

        // Resume iteration 1 via its real key (OtherSignal), advancing into iteration 2, which
        // registers a fresh wait on "LoopSignal". EV-043: the stale "LoopSignal" entry buffered
        // during iteration 1's window must NOT auto-resume iteration 2 merely because the key now
        // coincides with iteration 2's wait.
        var otherSignalMatch = new EventEnvelope(EventId.New(), "OtherSignal", LoopCorrelationId, "iter1-real", DateTimeOffset.UnixEpoch);
        var resumeOutcome = await engine.RaiseEventAsync<LoopState>(started.InstanceId, otherSignalMatch, TestContext.Current.CancellationToken);
        resumeOutcome.Should().Be(RaiseEventOutcome.Resumed);

        // Iteration 2's wait must still be genuinely open (not silently pre-consumed by the stale
        // entry): a fresh "LoopSignal" delivery now must legitimately resume it and complete the
        // workflow. If Bug #2 were present, iteration 2 would have already been auto-resumed by
        // the stale entry above, so this delivery would return NoMatch (nothing left waiting).
        var iteration2Match = new EventEnvelope(EventId.New(), LoopEventName, LoopCorrelationId, "iter2-real", DateTimeOffset.UnixEpoch);
        var finalOutcome = await engine.RaiseEventAsync<LoopState>(started.InstanceId, iteration2Match, TestContext.Current.CancellationToken);
        finalOutcome.Should().Be(RaiseEventOutcome.Resumed, "iteration 2's own wait must still be resumable — it must not have been silently consumed already by iteration 1's stale mailbox residue (EV-043)");
    }
}
