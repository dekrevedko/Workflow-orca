using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class LoopWaitAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-109")]
    public async Task WaitInLoop_PreviousIterationEvent_CannotResumeLaterIteration()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current => current.Iteration < 2,
                body => body
                    .Wait("Tick", current => IterationCorrelation(current.Iteration))
                    .Then(() => new CaptureAndIncrementStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var firstWait = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "first"),
            TestContext.Current.CancellationToken);

        var stale = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(0), "stale"),
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<TestState>(
            firstWait.InstanceId,
            Event("Tick", IterationCorrelation(1), "second"),
            TestContext.Current.CancellationToken);

        stale.Status.Should().Be(WorkflowStatus.Waiting);
        completed.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["first", "second"]);
    }

    private static CorrelationId IterationCorrelation(int iteration)
    {
        return new CorrelationId($"iteration-{iteration}");
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = name,
            CorrelationId = correlationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public int Iteration { get; set; }

        public List<string> Payloads { get; } = [];
    }

    private sealed class CaptureAndIncrementStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Payloads.Add(payload);
            }

            context.State.Iteration++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
