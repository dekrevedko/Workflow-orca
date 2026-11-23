using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Durable.Hosting.Dag;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LegacyWinner_RequiresConsistentTerminalFactsAndProvableFirstJoin(bool barrierBeforeJoin, bool conflictingTerminal)
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var store = services.GetRequiredService<IWorkflowEventStore>();
        var run = await StartTwoIndependentChildrenAsync(services, workflow, token);
        var bridge = services.GetRequiredService<IDurableDagRunBridgeV1>();
        var child = run.Nodes[0].ChildInstanceId!;
        var history = await store.LoadTailAsync(new(child), StreamVersion.Empty, token);
        var start = history.OfType<WorkflowStartedEvent>().Single();
        // Reconstruct a pre-upgrade writer's v7 winning request and separate terminal command.
        var original = CommandId.New();
        var finalizer = CommandId.New();
        var request = new WorkflowCancellationRequestedEvent
        {
            EventId = EventId.Create(Guid.NewGuid().ToString()), InstanceId = child, CommandId = original,
            CausationId = new(original.Value), OccurredAt = DateTimeOffset.UtcNow,
            ParentInstanceId = start.ParentInstanceId, RootInstanceId = start.RootInstanceId
        };
        var terminal = new WorkflowTerminalEvent
        {
            EventId = EventId.Create(Guid.NewGuid().ToString()), InstanceId = child, CommandId = finalizer,
            CausationId = new(finalizer.Value), OccurredAt = request.OccurredAt, Status = WorkflowInstanceStatus.Cancelled,
            ParentInstanceId = start.ParentInstanceId, RootInstanceId = start.RootInstanceId
        };
        var facts = new List<WorkflowEvent> { request, terminal };
        if (conflictingTerminal)
            facts.Add(terminal with { EventId = EventId.Create(Guid.NewGuid().ToString()), Status = WorkflowInstanceStatus.Terminated });
        var notice = DagPumpRunFixture.ChildWrite("dag-child-join-v1", run.RunId);
        (await store.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new(child), ExpectedVersion = new(history.Count), Events = facts, OutboxRecords = [notice]
        }, token)).IsSuccess.Should().BeTrue();
        if (!barrierBeforeJoin && !conflictingTerminal)
            (await services.GetRequiredService<IDurableDagChildHandler>().HandleAsync(notice, token)).Should().BeTrue();
        if (!conflictingTerminal)
            (await bridge.RequestRunCancellationAsync(run.RunId, token)).Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var poll = Request();
        await outbox.ClaimAsync(poll with { KindSelector = OutboxKindSelector.Including("dag-child-cancel-v1") }, token);
        var delivered = await services.GetRequiredService<DurableDagChildPump>().PumpOnceAsync(poll, token);
        var after = await DagPumpRunFixture.ReadAsync(store, run.RunId, token);
        var journal = await store.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(run.RunId), StreamVersion.Empty, token);
        var state = (await outbox.GetStateAsync(notice.OutboxRecordId, token)).Value;
        var retained = conflictingTerminal ? DagNodeStateV1.StartAuthorized : DagNodeStateV1.CancellationRequested;
        after.Nodes[1].State.Should().Be(retained);
        if (barrierBeforeJoin || conflictingTerminal)
        {
            delivered.Should().Be(0);
            state.Should().Be(OutboxRecordState.Poisoned);
            after.State.Should().Be(DagRunStateV1.IntegrityFault);
            after.Nodes[0].State.Should().Be(retained);
            journal.Should().NotContain(r => r.Kind == DagRunRecordKindV1.ChildJoined);
        }
        else
        {
            delivered.Should().Be(1);
            state.Should().Be(OutboxRecordState.Dispatched);
            after.State.Should().Be(DagRunStateV1.CancellationRequested);
            after.Nodes[0].State.Should().Be(DagNodeStateV1.Failed);
            after.Nodes[0].FailureCode.Should().Be("CHILD_CANCELLED");
            journal.Should().ContainSingle(r => r.Kind == DagRunRecordKindV1.ChildJoined);
        }
    }
}
