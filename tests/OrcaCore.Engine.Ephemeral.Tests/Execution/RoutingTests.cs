using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class RoutingTests
{
    private static readonly CorrelationId Correlation = CorrelationId.Create("shared");

    [Fact]
    public async Task RaiseByCorrelationAsync_OneActiveWait_ResumesThatInstance()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state, "Approved", Correlation);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);

        var snapshot = await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["payload"]);
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_ZeroActiveWaits_ReturnsNoActiveWait()
    {
        var engine = new EphemeralWorkflowEngine();

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*no active wait*");
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_MultipleActiveWaits_ReturnsAmbiguousWithoutDelivery()
    {
        var firstState = new TestState();
        var secondState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var first = Definition(firstState, "Approved", Correlation);
        var second = Definition(secondState, "Approved", Correlation);
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);
        await StartAsync(engine, first);
        await StartAsync(engine, second);

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*ambiguous*");
        firstState.Payloads.Should().BeEmpty();
        secondState.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_BranchTarget_SelectsOnlyMatchingInstance()
    {
        var firstState = new TestState();
        var secondState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var first = ParallelDefinition(firstState, "target", "other-a");
        var second = ParallelDefinition(secondState, "other", "other-b");
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);
        await StartAsync(engine, first);
        await StartAsync(engine, second);

        var snapshot = await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload", branchId: "0:target"),
            TestContext.Current.CancellationToken);
        snapshot.ActiveWaits.Should().ContainSingle(wait => wait.EventName == "other-a");
        snapshot = await engine.RaiseEventAsync<TestState>(
            snapshot.InstanceId,
            Event("other-a", CorrelationId.Create("other-a"), null),
            TestContext.Current.CancellationToken);
        var firstResult = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();

        snapshot.DefinitionId.Should().Be(first.DefinitionId);
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        firstResult.Payloads.Should().Equal(["payload"]);
        secondState.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseByDefinitionAsync_MultipleDefinitions_DeliversOnlyTargetDefinition()
    {
        var targetState = new TestState();
        var otherState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var target = Definition(targetState, "Approved", Correlation);
        var other = Definition(otherState, "Approved", Correlation);
        engine.RegisterDefinition(target);
        engine.RegisterDefinition(other);
        await StartAsync(engine, target);
        await StartAsync(engine, other);

        var snapshots = await engine.RaiseEventByDefinitionAsync<TestState>(
            target.DefinitionId,
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.DefinitionId.Should().Be(target.DefinitionId);
        targetState.Payloads.Should().Equal(["payload"]);
        otherState.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitRegistration_DuplicateCorrelationAcrossInstances_Succeeds()
    {
        var engine = new EphemeralWorkflowEngine();
        var first = Definition(new TestState(), "Approved", Correlation);
        var second = Definition(new TestState(), "Approved", Correlation);
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);

        var firstSnapshot = await StartAsync(engine, first);
        var secondSnapshot = await StartAsync(engine, second);

        firstSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        secondSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    public async Task WaitMatch_RemovesWaitFromCorrelationIndex()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state, "Approved", Correlation);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);
        await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "again"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*no active wait*");
    }

    [Fact]
    [Trait("AC", "AC-115")]
    public async Task RaiseByCorrelationAsync_UsesIndexedLookupInsteadOfRegistryWideScan()
    {
        var registry = new CountingInstanceRegistry();
        var engine = new EphemeralWorkflowEngine(TimeProvider.System, registry, new InstanceExecutionLane());
        var state = new TestState();
        var definition = Definition(state, "Approved", Correlation);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);
        registry.ResetCounts();

        var snapshot = await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        registry.GetManyCount.Should().BeGreaterThan(0);
        registry.ListCount.Should().Be(0);
    }

    private static Task<WorkflowInstanceSnapshot> StartAsync(
        EphemeralWorkflowEngine engine,
        OrcaCore.Core.Definitions.WorkflowDefinition<TestState> definition)
    {
        return engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        TestState state,
        string eventName,
        CorrelationId correlationId)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait(eventName, _ => correlationId)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build();
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> ParallelDefinition(
        TestState state,
        string matchingBranchName,
        string otherEventName)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Parallel<string>(
                branches => branches
                    .Branch<BranchPayloadState>(
                        matchingBranchName,
                        _ => new BranchPayloadState(),
                        branch => branch
                            .Wait("Approved", _ => Correlation)
                            .Return(_ => "payload"))
                    .Branch<BranchPayloadState>(
                        "residual",
                        _ => new BranchPayloadState(),
                        branch => branch
                            .Wait(otherEventName, _ => CorrelationId.Create(otherEventName))
                            .Return(_ => string.Empty)),
                (parent, results) =>
                {
                    var merged = new TestState();
                    merged.Payloads.AddRange(parent.Value.Payloads);
                    merged.Payloads.AddRange(results
                        .Select(result => result.Value)
                        .Where(payload => !string.IsNullOrEmpty(payload)));
                    return merged;
                })
            .End()
            .Build();
    }

    private static EventEnvelope Event(
        string name,
        CorrelationId correlationId,
        object? payload,
        string? branchId = null)
    {
        return new EventEnvelope
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            EventName = name,
            CorrelationId = correlationId,
            BranchId = branchId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public List<string> Payloads { get; init; } = [];
    }

    private sealed class CapturePayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Payloads.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BranchPayloadState;

    private sealed class CountingInstanceRegistry : IInstanceRegistry
    {
        private readonly Dictionary<InstanceId, object> instances = [];

        internal int GetManyCount { get; private set; }

        internal int ListCount { get; private set; }

        public void Save<TState>(WorkflowInstance<TState> instance)
        {
            instances[instance.InstanceId] = instance;
        }

        public bool TryGet(InstanceId instanceId, out object? instance)
        {
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

        public bool Remove(InstanceId instanceId)
        {
            return instances.Remove(instanceId);
        }

        internal void ResetCounts()
        {
            GetManyCount = 0;
            ListCount = 0;
        }
    }
}
