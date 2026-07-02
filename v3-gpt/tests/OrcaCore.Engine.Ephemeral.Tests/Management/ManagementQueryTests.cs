using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

public sealed class ManagementQueryTests
{
    [Fact]
    public async Task Where_StatusFilter_ListAndCountReturnSameSelection()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var query = engine.Management.All().Where(instance => instance.Status == WorkflowStatus.Waiting);

        var listed = query.List();
        var counted = query.Count();

        listed.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Waiting);
        counted.Should().Be(listed.Count);
    }

    [Fact]
    public async Task Get_ReturnsSnapshotCopy_NotLiveInstance()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var snapshot = engine.Management.Instance(started.InstanceId).Get();
        await engine.RaiseEventAsync<TestState>(
            started.InstanceId,
            Event("Ready", "wait", "resumed"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        engine.Management.Instance(started.InstanceId).Get().Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task GetState_ReturnsCopy_ExternalMutationDoesNotAffectEngineState()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "original",
            TestContext.Current.CancellationToken);

        var state = engine.Management.Instance(started.InstanceId).GetState<TestState>();
        state.Name = "mutated";
        state.Values.Add("outside");

        var reread = engine.Management.Instance(started.InstanceId).GetState<TestState>();
        reread.Name.Should().Be("original");
        reread.Values.Should().Equal(["started"]);
    }

    [Fact]
    public async Task GetState_WrongType_ReturnsClearFailure()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "original",
            TestContext.Current.CancellationToken);

        var act = () => engine.Management.Instance(started.InstanceId).GetState<OtherState>();

        act.Should().Throw<WorkflowDefinitionException>()
            .WithMessage("*state type*OtherState*");
    }

    [Fact]
    public async Task GetActiveWaits_ReturnsActiveWaitSnapshotsOnly()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var waits = engine.Management.Instance(started.InstanceId).GetActiveWaits();
        await engine.RaiseEventAsync<TestState>(
            started.InstanceId,
            Event("Ready", "wait", "resumed"),
            TestContext.Current.CancellationToken);

        waits.Should().ContainSingle()
            .Which.EventName.Should().Be("Ready");
        engine.Management.Instance(started.InstanceId).GetActiveWaits().Should().BeEmpty();
    }

    [Fact]
    public async Task Statistics_GroupsByDefinitionVersionAndStatus()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var statistics = engine.Management.All().Statistics();

        statistics.Groups.Should().BeEquivalentTo(
        [
            new
            {
                completed.DefinitionId,
                completed.DefinitionVersion,
                Status = WorkflowStatus.Completed,
                Count = 1
            },
            new
            {
                waiting.DefinitionId,
                waiting.DefinitionVersion,
                Status = WorkflowStatus.Waiting,
                Count = 1
            }
        ]);
    }

    [Fact]
    public async Task BulkGet_ByIdsOrFilter_UsesSingleRegistryOperation()
    {
        var registry = new CountingInstanceRegistry();
        var engine = new EphemeralWorkflowEngine(TimeProvider.System, registry, new InstanceExecutionLane());
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var first = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);
        registry.ResetCounts();

        var snapshots = engine.Management.Instances([first.InstanceId, second.InstanceId]).List();

        snapshots.Should().HaveCount(2);
        registry.GetManyCount.Should().Be(1);
        registry.TryGetCount.Should().Be(0);
        registry.ListCount.Should().Be(0);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> CompletedDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Then(() => new AddValueStep("started"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => new CorrelationId(state.Name))
            .Then(() => new CapturePayloadStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(string eventName, string correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = eventName,
            CorrelationId = new CorrelationId(correlationId),
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Values { get; set; } = [];
    }

    private sealed class OtherState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AddValueStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CapturePayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Values.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountingInstanceRegistry : IInstanceRegistry
    {
        private readonly Dictionary<InstanceId, object> instances = [];

        internal int GetManyCount { get; private set; }

        internal int ListCount { get; private set; }

        internal int TryGetCount { get; private set; }

        public void Save<TState>(WorkflowInstance<TState> instance)
        {
            instances[instance.InstanceId] = instance;
        }

        public bool TryGet(InstanceId instanceId, out object? instance)
        {
            TryGetCount++;
            return instances.TryGetValue(instanceId, out instance);
        }

        public IReadOnlyCollection<object> GetMany(IReadOnlyCollection<InstanceId> instanceIds)
        {
            GetManyCount++;
            return instanceIds
                .Where(instances.ContainsKey)
                .Select(instanceId => instances[instanceId])
                .ToArray();
        }

        public IReadOnlyCollection<object> List()
        {
            ListCount++;
            return instances.Values.ToArray();
        }

        internal void ResetCounts()
        {
            GetManyCount = 0;
            ListCount = 0;
            TryGetCount = 0;
        }
    }
}
