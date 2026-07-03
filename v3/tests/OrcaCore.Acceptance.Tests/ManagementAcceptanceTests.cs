using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

/// <summary>
/// T1-13 (MG-001, MG-002, MG-003, MG-005, MG-010, EV-013): public-API-only acceptance coverage
/// for the ephemeral management query baseline.
/// </summary>
public class ManagementAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class RecordResumeStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add("resumed");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CompleteStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private static WorkflowDefinition<OrderState> ApprovalDefinition(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new RecordResumeStep());
        builder.End("Approved");
        return builder.Build(definitionId, new DefinitionVersion(1));
    }

    private static WorkflowDefinition<OrderState> CompletingDefinition(DefinitionId definitionId)
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new CompleteStep());
        builder.End("Done");
        return builder.Build(definitionId, new DefinitionVersion(1));
    }

    [Trait("AC", "AC-501")]
    [Fact]
    public async Task WhereOverSnapshots_ListsAndCountsMatchingInstances()
    {
        var engine = new EphemeralWorkflowEngine();
        var waitingDefinitionId = new DefinitionId("mgmt-ac501-waiting");
        var completedDefinitionId = new DefinitionId("mgmt-ac501-completed");
        engine.RegisterDefinition(ApprovalDefinition(waitingDefinitionId));
        engine.RegisterDefinition(CompletingDefinition(completedDefinitionId));

        var waiting1 = await engine.StartAsync<int, OrderState>(waitingDefinitionId, 1, TestContext.Current.CancellationToken);
        var waiting2 = await engine.StartAsync<int, OrderState>(waitingDefinitionId, 2, TestContext.Current.CancellationToken);
        await engine.StartAsync<int, OrderState>(completedDefinitionId, 3, TestContext.Current.CancellationToken);

        var scope = engine.Query().All().Where(snapshot => snapshot.Status == WorkflowStatus.Waiting);

        var list = scope.List();
        var count = scope.Count();

        list.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([waiting1.InstanceId, waiting2.InstanceId]);
        count.Should().Be(2);
        list.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Waiting);
    }

    [Trait("AC", "AC-502")]
    [Fact]
    public async Task SameSelection_DrivesListAndSupportedCommand()
    {
        // MG-003 query/command symmetry, using a supported ephemeral command (event delivery
        // over a selected wait set) rather than durable-only Retry.
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("mgmt-ac502");
        engine.RegisterDefinition(ApprovalDefinition(definitionId, "ApprovalReceived", "order-ac502"));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var selection = engine.Query().All().Where(snapshot => snapshot.Status == WorkflowStatus.Waiting);
        var selected = selection.List();

        selected.Should().ContainSingle(snapshot => snapshot.InstanceId == started.InstanceId);

        var targetInstanceId = selected.Single().InstanceId;
        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-ac502"), "payload", DateTimeOffset.UnixEpoch);

        var outcome = await engine.RaiseEventAsync<OrderState>(targetInstanceId, envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);

        var afterCommand = engine.Query().Instance(targetInstanceId).Get();
        afterCommand!.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Trait("AC", "AC-503")]
    [Fact]
    public async Task Statistics_GroupCountsByDefinitionAndStatus()
    {
        var engine = new EphemeralWorkflowEngine();
        var waitingDefinitionId = new DefinitionId("mgmt-ac503-waiting");
        var completedDefinitionId = new DefinitionId("mgmt-ac503-completed");
        engine.RegisterDefinition(ApprovalDefinition(waitingDefinitionId));
        engine.RegisterDefinition(CompletingDefinition(completedDefinitionId));

        await engine.StartAsync<int, OrderState>(waitingDefinitionId, 1, TestContext.Current.CancellationToken);
        await engine.StartAsync<int, OrderState>(waitingDefinitionId, 2, TestContext.Current.CancellationToken);
        await engine.StartAsync<int, OrderState>(completedDefinitionId, 3, TestContext.Current.CancellationToken);

        var statistics = engine.Query().All().Statistics();

        var waitingGroup = statistics.Single(group => group.DefinitionId == waitingDefinitionId);
        waitingGroup.Status.Should().Be(WorkflowStatus.Waiting);
        waitingGroup.Count.Should().Be(2);

        var completedGroup = statistics.Single(group => group.DefinitionId == completedDefinitionId);
        completedGroup.Status.Should().Be(WorkflowStatus.Completed);
        completedGroup.Count.Should().Be(1);
    }

    [Trait("AC", "AC-009")]
    [Fact]
    public async Task PublicApiResults_DoNotLeakLiveInstances()
    {
        // CR-021: every public management result must be an immutable snapshot/copy - never a
        // live runtime reference. Proven by asserting the returned types are the public
        // snapshot/copy shapes and that mutating a returned business-state copy does not affect
        // what a later query observes.
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("mgmt-ac009");
        engine.RegisterDefinition(ApprovalDefinition(definitionId));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 7, TestContext.Current.CancellationToken);

        var snapshot = engine.Query().Instance(started.InstanceId).Get();
        snapshot.Should().BeOfType<WorkflowInstanceSnapshot>();

        var state = engine.Query().Instance(started.InstanceId).GetState<OrderState>();
        state!.Total.Should().Be(7);
        state.Total = 12345;

        var restate = engine.Query().Instance(started.InstanceId).GetState<OrderState>();
        restate!.Total.Should().Be(7, "returned business state must be a copy, never the live mutable instance");
    }

    [Trait("AC", "AC-115")]
    [Fact]
    public async Task BulkRetrieval_ReturnsMatchesWithoutPerInstanceRoundTrips()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("mgmt-ac115");
        engine.RegisterDefinition(ApprovalDefinition(definitionId));

        var started1 = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        var started2 = await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);
        var started3 = await engine.StartAsync<int, OrderState>(definitionId, 3, TestContext.Current.CancellationToken);

        var all = engine.Query().All().List();

        all.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo(
            [started1.InstanceId, started2.InstanceId, started3.InstanceId]);
    }
}
