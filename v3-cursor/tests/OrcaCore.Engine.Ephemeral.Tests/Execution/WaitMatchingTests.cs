using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class WaitMatchingTests
{
    private static readonly DefinitionId DefinitionId = new(Guid.Parse("55555555-5555-7555-8555-555555555555"));
    private static readonly DefinitionVersion Version = new(1);

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

    private sealed class CaptureResumedEventStep(List<EventEnvelope?> sink) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            sink.Add(context.ResumedEvent);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static Interpreter<OrderState> CreateInterpreter(Clock clock) => new(clock.Provider);

    [Fact]
    public async Task Run_WaitResult_RegistersActiveWaitAndSetsWaiting()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var correlationId = new CorrelationId("order-1");
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock)
            .RunAsync(definition, 0, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Waiting);
        instance.ActiveWait.Should().NotBeNull();
        instance.ActiveWait!.EventName.Should().Be("Approved");
        instance.ActiveWait!.CorrelationId.Should().Be(correlationId);
        instance.ActiveWait!.RegisteredAt.Should().Be(clock.Now);
        instance.ActiveWait!.Status.Should().Be(WaitStatus.Active);
        instance.ActiveWait!.Mode.Should().Be(WaitMode.Resident);
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ResumesAndClearsWait()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var correlationId = new CorrelationId("order-1");
        var sink = new List<string>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new RecordToSinkStep(sink, "after-wait"))
            .End("Done")
            .Build(DefinitionId, Version);

        var engine = new EphemeralWorkflowEngine(clock.Provider);
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(DefinitionId, 0, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);
        started.ActiveWaits.Should().ContainSingle();

        var envelope = new EventEnvelope(EventId.New(), "Approved", correlationId, "payload", clock.Now);
        var result = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        var resumed = result.Should().BeOfType<RaiseEventResult.Resumed>().Subject;
        resumed.Snapshot.Status.Should().Be(WorkflowStatus.Completed);
        resumed.Snapshot.EndOutcomeName.Should().Be("Done");
        resumed.Snapshot.ActiveWaits.Should().BeEmpty();
        sink.Should().Equal("after-wait");
    }

    [Fact]
    public async Task RaiseEventAsync_MatchingEvent_ProvidesPayloadToNextStepOnly()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var correlationId = new CorrelationId("order-1");
        var captured = new List<EventEnvelope?>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new CaptureResumedEventStep(captured))
            .Then(new CaptureResumedEventStep(captured))
            .End()
            .Build(DefinitionId, Version);

        var engine = new EphemeralWorkflowEngine(clock.Provider);
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(DefinitionId, 0, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "Approved", correlationId, "payload", clock.Now);
        var result = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        result.Should().BeOfType<RaiseEventResult.Resumed>();
        captured.Should().HaveCount(2);
        captured[0].Should().Be(envelope);
        captured[1].Should().BeNull();
    }

    [Fact]
    public async Task RaiseEventAsync_WrongNameOrCorrelation_LeavesInstanceWaiting()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var correlationId = new CorrelationId("order-1");
        var sink = new List<string>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new RecordToSinkStep(sink, "after-wait"))
            .End("Done")
            .Build(DefinitionId, Version);

        var engine = new EphemeralWorkflowEngine(clock.Provider);
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(DefinitionId, 0, TestContext.Current.CancellationToken);

        var wrongName = new EventEnvelope(EventId.New(), "Rejected", correlationId, null, clock.Now);
        var wrongCorrelation = new EventEnvelope(EventId.New(), "Approved", new CorrelationId("other-order"), null, clock.Now);

        var firstResult = await engine.RaiseEventAsync<OrderState>(started.InstanceId, wrongName, TestContext.Current.CancellationToken);
        var secondResult = await engine.RaiseEventAsync<OrderState>(started.InstanceId, wrongCorrelation, TestContext.Current.CancellationToken);

        firstResult.Should().BeOfType<RaiseEventResult.NoMatch>();
        secondResult.Should().BeOfType<RaiseEventResult.NoMatch>();
        sink.Should().BeEmpty();

        var matching = new EventEnvelope(EventId.New(), "Approved", correlationId, null, clock.Now);
        var thirdResult = await engine.RaiseEventAsync<OrderState>(started.InstanceId, matching, TestContext.Current.CancellationToken);

        thirdResult.Should().BeOfType<RaiseEventResult.Resumed>();
        sink.Should().Equal("after-wait");
    }

    [Fact]
    public async Task RaiseEventAsync_TwoConcurrentMatches_OnlyOneContinuationCommits()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var correlationId = new CorrelationId("order-1");
        var sink = new List<string>();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new WaitStep("Approved", correlationId))
            .Then(new RecordToSinkStep(sink, "after-wait"))
            .End()
            .Build(DefinitionId, Version);

        var engine = new EphemeralWorkflowEngine(clock.Provider);
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<int, OrderState>(DefinitionId, 0, TestContext.Current.CancellationToken);

        var first = new EventEnvelope(EventId.New(), "Approved", correlationId, null, clock.Now);
        var second = new EventEnvelope(EventId.New(), "Approved", correlationId, null, clock.Now);

        var firstTask = engine.RaiseEventAsync<OrderState>(started.InstanceId, first, TestContext.Current.CancellationToken);
        var secondTask = engine.RaiseEventAsync<OrderState>(started.InstanceId, second, TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(firstTask, secondTask);

        results.OfType<RaiseEventResult.Resumed>().Should().ContainSingle();
        results.OfType<RaiseEventResult.NoMatch>().Should().ContainSingle();
        sink.Should().Equal("after-wait");
    }
}
