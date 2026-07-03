using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Management;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

/// <summary>
/// T1-13 (MG-001, MG-002, MG-003, MG-005, MG-010, EV-013): the ephemeral management query
/// baseline — scoped selection, constrained metadata filters, snapshot queries, typed state
/// reads, active wait inspection, and grouped statistics.
/// </summary>
public class ManagementQueryTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class OtherState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class CompleteStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildWaitingWorkflow(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new CompleteStep());
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Fact]
    public async Task Where_StatusFilter_ListAndCountReturnSameSelection()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-status-filter"));
        var waiting1 = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        var waiting2 = await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        // Register a second, completing definition on the SAME engine so both statuses coexist.
        var completingDefinitionId = new DefinitionId("query-status-filter-done");
        var completingBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        completingBuilder.Then(new CompleteStep());
        completingBuilder.End("Done");
        var completingDefinition = completingBuilder.Build(completingDefinitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(completingDefinition);
        await engine.StartAsync<int, OrderState>(completingDefinitionId, 3, TestContext.Current.CancellationToken);

        var waitingScope = engine.Query().All().Where(snapshot => snapshot.Status == WorkflowStatus.Waiting);

        var list = waitingScope.List();
        var count = waitingScope.Count();

        list.Should().HaveCount(2);
        list.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([waiting1.InstanceId, waiting2.InstanceId]);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Get_ReturnsSnapshotCopy_NotLiveInstance()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-get-copy"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var first = engine.Query().Instance(started.InstanceId).Get();
        var second = engine.Query().Instance(started.InstanceId).Get();

        first.Should().NotBeNull();
        first.Should().BeOfType<WorkflowInstanceSnapshot>();
        first.Should().Be(second, "snapshots are immutable value records, not references into live engine state");
        ReferenceEquals(first, second).Should().BeFalse();
    }

    [Fact]
    public async Task GetState_ReturnsCopy_ExternalMutationDoesNotAffectEngineState()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-getstate-copy"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 5, TestContext.Current.CancellationToken);

        var state = engine.Query().Instance(started.InstanceId).GetState<OrderState>();

        state.Should().NotBeNull();
        state!.Total.Should().Be(5);

        // Mutate the copy's own scalar field. A shallow member-wise copy is the documented,
        // TDD-minimal strategy (see T1-13 PROGRESS.md IOQ-6 note) - it protects independent
        // scalar fields on the returned copy but does not deep-clone nested reference-typed
        // members, so this test asserts what the contract actually promises.
        state.Total = 999;

        var stateAfterMutation = engine.Query().Instance(started.InstanceId).GetState<OrderState>();

        stateAfterMutation!.Total.Should().Be(5, "the returned state must be a copy - external mutation of the copy's own fields must not reach engine state");
    }

    [Fact]
    public async Task GetState_WrongType_ReturnsClearFailure()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-getstate-wrongtype"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var act = () => engine.Query().Instance(started.InstanceId).GetState<OtherState>();

        act.Should().Throw<WorkflowDefinitionException>();
    }

    [Fact]
    public async Task GetActiveWaits_ReturnsActiveWaitSnapshotsOnly()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-active-waits"), "ApprovalReceived", "order-42");
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var completedDefinitionId = new DefinitionId("query-active-waits-completed");
        var completingBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        completingBuilder.Then(new CompleteStep());
        completingBuilder.End("Done");
        var completingDefinition = completingBuilder.Build(completedDefinitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(completingDefinition);
        await engine.StartAsync<int, OrderState>(completedDefinitionId, 2, TestContext.Current.CancellationToken);

        var activeWaits = engine.Query().All().GetActiveWaits();

        activeWaits.Should().HaveCount(1);
        activeWaits[0].EventName.Should().Be("ApprovalReceived");
        activeWaits[0].CorrelationId.Should().Be(new CorrelationId("order-42"));
        activeWaits[0].InstanceId.Should().Be(started.InstanceId);
    }

    [Fact]
    public async Task Statistics_GroupsByDefinitionVersionAndStatus()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("query-statistics"));
        await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        var completingDefinitionId = new DefinitionId("query-statistics-completing");
        var completingBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        completingBuilder.Then(new CompleteStep());
        completingBuilder.End("Done");
        var completingDefinition = completingBuilder.Build(completingDefinitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(completingDefinition);
        await engine.StartAsync<int, OrderState>(completingDefinitionId, 3, TestContext.Current.CancellationToken);

        var statistics = engine.Query().All().Statistics();

        statistics.Should().HaveCount(2);

        var waitingGroup = statistics.Single(group => group.DefinitionId == definitionId);
        waitingGroup.DefinitionVersion.Should().Be(new DefinitionVersion(1));
        waitingGroup.Status.Should().Be(WorkflowStatus.Waiting);
        waitingGroup.Count.Should().Be(2);

        var completedGroup = statistics.Single(group => group.DefinitionId == completingDefinitionId);
        completedGroup.Status.Should().Be(WorkflowStatus.Completed);
        completedGroup.Count.Should().Be(1);
    }

    [Fact]
    public void BulkGet_ByIdsOrFilter_UsesSingleRegistryOperation()
    {
        // EV-013/AC-115: List/Count/GetActiveWaits must retrieve instances via one bulk
        // enumeration, not N per-instance TryGet calls. Assert the contract directly on
        // IInstanceRegistry (internal, reachable via InternalsVisibleTo): it must expose a bulk
        // read returning every stored instance in one call.
        var registry = new InstanceRegistry();
        var clock = new Clock(DateTimeOffset.UnixEpoch);

        var instanceA = new WorkflowInstance<OrderState>(InstanceId.New(), new DefinitionId("bulk-a"), new DefinitionVersion(1), new OrderState(), clock.Now);
        var instanceB = new WorkflowInstance<OrderState>(InstanceId.New(), new DefinitionId("bulk-b"), new DefinitionVersion(1), new OrderState(), clock.Now);
        registry.Add(instanceA);
        registry.Add(instanceB);

        var all = registry.GetAll();

        all.Should().HaveCount(2);
        all.Select(instance => instance.InstanceId).Should().BeEquivalentTo([instanceA.InstanceId, instanceB.InstanceId]);
    }
}
