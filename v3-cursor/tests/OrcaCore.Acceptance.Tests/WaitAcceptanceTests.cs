using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public sealed class WaitAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    private sealed class WaitStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class RecordToSinkStep(List<string> sink, string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            sink.Add(tag);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureResumedPayloadStep(List<object?> sink) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            sink.Add(context.ResumedEvent?.Payload);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Trait("AC", "AC-101")]
    [Fact]
    public async Task Wait_EntersWaiting_WithInspectableActiveWait()
    {
        var correlationId = new CorrelationId("order-1");
        var definitionId = DefinitionId.New();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .End("Done")
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle();
        var wait = snapshot.ActiveWaits[0];
        wait.EventName.Should().Be("Approved");
        wait.CorrelationId.Should().Be(correlationId);
        wait.Status.Should().Be(WaitStatus.Active);
        wait.Mode.Should().Be(WaitMode.Resident);
    }

    [Trait("AC", "AC-102")]
    [Fact]
    public async Task MatchingEvent_ResumesExactlyOnce_WithPayload()
    {
        var correlationId = new CorrelationId("order-1");
        var definitionId = DefinitionId.New();
        var payloads = new List<object?>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new CaptureResumedPayloadStep(payloads))
            .End("Done")
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "Approved", correlationId, "approved-by-ops", DateTimeOffset.UtcNow);
        var result = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        var resumed = result.Should().BeOfType<RaiseEventResult.Resumed>().Subject;
        resumed.Snapshot.Status.Should().Be(WorkflowStatus.Completed);
        resumed.Snapshot.ActiveWaits.Should().BeEmpty();
        payloads.Should().Equal("approved-by-ops");

        // Re-delivering the same matching criteria after resume must not resume again (EV-023).
        var repeat = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);
        repeat.Should().BeOfType<RaiseEventResult.NoMatch>();
        payloads.Should().Equal("approved-by-ops");
    }

    [Trait("AC", "AC-103")]
    [Fact]
    public async Task NonMatchingEvent_DoesNotResume()
    {
        var correlationId = new CorrelationId("order-1");
        var definitionId = DefinitionId.New();
        var sink = new List<string>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new RecordToSinkStep(sink, "after-wait"))
            .End("Done")
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);

        var nonMatching = new EventEnvelope(EventId.New(), "Rejected", correlationId, null, DateTimeOffset.UtcNow);
        var result = await engine.RaiseEventAsync<OrderState>(started.InstanceId, nonMatching, TestContext.Current.CancellationToken);

        result.Should().BeOfType<RaiseEventResult.NoMatch>();
        sink.Should().BeEmpty();
    }

    [Trait("AC", "AC-006")]
    [Fact]
    public async Task ConcurrentResumeAttempts_ProduceOneSequentialOutcome()
    {
        var correlationId = new CorrelationId("order-1");
        var definitionId = DefinitionId.New();
        var sink = new List<string>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new RecordToSinkStep(sink, "after-wait"))
            .End("Done")
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);

        var first = new EventEnvelope(EventId.New(), "Approved", correlationId, null, DateTimeOffset.UtcNow);
        var second = new EventEnvelope(EventId.New(), "Approved", correlationId, null, DateTimeOffset.UtcNow);

        var firstTask = engine.RaiseEventAsync<OrderState>(started.InstanceId, first, TestContext.Current.CancellationToken);
        var secondTask = engine.RaiseEventAsync<OrderState>(started.InstanceId, second, TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(firstTask, secondTask);

        results.OfType<RaiseEventResult.Resumed>().Should().ContainSingle();
        results.OfType<RaiseEventResult.NoMatch>().Should().ContainSingle();
        sink.Should().Equal("after-wait");
    }
}
