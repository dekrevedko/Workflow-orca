using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

/// <summary>
/// T1-15 (CR-017, AC-013): public-API-only acceptance coverage for the ephemeral portion of
/// <c>Yield</c> commits-progress-and-completes-exactly-once behavior.
/// </summary>
public class YieldAcceptanceTests
{
    private sealed class ChunkState
    {
        public int CommittedChunks { get; set; }

        // Settable (not get-only): the state copy returned by Query().GetState<TState>() round-trips
        // through System.Text.Json (WorkflowInstance{TState}.GetStateCopy), which does not populate
        // a get-only collection property on deserialize.
        public List<int> ObservedAtEachInvocation { get; set; } = [];
    }

    /// <summary>Yields <paramref name="totalYields"/> times, committing one chunk per invocation, then completes.</summary>
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

    [Trait("AC", "AC-013")]
    [Fact]
    public async Task Yield_CommitsProgressAndCompletesExactlyOnce()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("yield-acceptance-013");
        var builder = WorkflowBuilder<ChunkState>.Create<int>(_ => new ChunkState());
        builder.Then(new ChunkedWorkStep(totalYields: 3));
        builder.End("Done");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, ChunkState>(definitionId, 0, TestContext.Current.CancellationToken);

        // The instance stays Running throughout every yield and only reaches Completed once the
        // step's final (non-yielding) invocation returns Completed - never Failed, never stuck
        // Waiting for something nobody will ever send.
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("Done");

        var state = engine.Query().Instance(snapshot.InstanceId).GetState<ChunkState>();
        state.Should().NotBeNull();

        // 3 yields + 1 completing invocation = 4 total invocations, each committing its own
        // chunk of progress in order, with no chunk skipped, redone, or duplicated.
        state!.CommittedChunks.Should().Be(4);
        state.ObservedAtEachInvocation.Should().Equal(1, 2, 3, 4);
    }
}
