using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ManagementAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-501")]
    public async Task WhereOverSnapshots_ListsAndCountsMatchingInstances()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var query = engine.Management.All().Where(instance => instance.Status == WorkflowStatus.Waiting);

        query.List().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Waiting);
        query.Count().Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-502")]
    public async Task SameSelection_DrivesListAndSupportedCommand()
    {
        var engine = new EphemeralWorkflowEngine();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(waiting);
        var first = await engine.StartAsync<string, TestState>(
            waiting.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, TestState>(
            waiting.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);
        var selection = engine.Management.All().Where(instance => instance.Status == WorkflowStatus.Waiting);

        var beforeCommand = selection.List();
        var results = await selection.RaiseEventAsync<TestState>(
            Event("Ready", "first", "resumed"),
            TestContext.Current.CancellationToken);

        beforeCommand.Select(instance => instance.InstanceId).Should().Contain([first.InstanceId, second.InstanceId]);
        results.Should().ContainSingle()
            .Which.InstanceId.Should().Be(first.InstanceId);
        engine.Management.Instance(first.InstanceId).Get().Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(second.InstanceId).Get().Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    [Trait("AC", "AC-503")]
    public async Task Statistics_GroupCountsByDefinitionAndStatus()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var groups = engine.Management.All().Statistics().Groups;

        groups.Should().Contain(group =>
            group.DefinitionId == completed.DefinitionId &&
            group.DefinitionVersion == completed.DefinitionVersion &&
            group.Status == WorkflowStatus.Completed &&
            group.Count == 1);
        groups.Should().Contain(group =>
            group.DefinitionId == waiting.DefinitionId &&
            group.DefinitionVersion == waiting.DefinitionVersion &&
            group.Status == WorkflowStatus.Waiting &&
            group.Count == 1);
    }

    [Fact]
    [Trait("AC", "AC-009")]
    public async Task PublicApiResults_DoNotLeakLiveInstances()
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

        engine.Management.Instance(started.InstanceId).GetState<TestState>().Name.Should().Be("original");
    }

    [Fact]
    [Trait("AC", "AC-115")]
    public async Task BulkRetrieval_ReturnsMatchesWithoutPerInstanceRoundTrips()
    {
        var engine = new EphemeralWorkflowEngine();
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

        var snapshots = engine.Management.Instances([first.InstanceId, second.InstanceId]).List();

        snapshots.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([first.InstanceId, second.InstanceId]);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> CompletedDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
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
    }

    private sealed class CapturePayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
