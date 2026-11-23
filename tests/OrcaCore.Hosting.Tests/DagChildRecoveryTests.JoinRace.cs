using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Hosting.Services;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompetingCancellationRequests_AfterBarrierKeepFirstWinner(bool independentFirst)
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var run = await StartTwoIndependentChildrenAsync(services, workflow, token);
        var child = run.Nodes[0].ChildInstanceId!;
        var handle = await services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference)
            .GetInstanceAsync(child, token);
        var bridge = services.GetRequiredService<IDurableDagRunBridgeV1>();
        (await bridge.RequestRunCancellationAsync(run.RunId, token)).Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
        var poll = Request();
        var cancellations = await services.GetRequiredService<IWorkflowOutboxStore>().ClaimAsync(poll with
        {
            KindSelector = OutboxKindSelector.Including("dag-child-cancel-v1")
        }, token);
        var expected = DagPumpRunFixture.ChildWrite("dag-child-cancel-v1", run.RunId);
        var cancelA = cancellations.Single(c => c.Payload.AsSpan().SequenceEqual(expected.Payload));
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        if (independentFirst) (await handle.RequestCancellationAsync(token)).Should().Be(WorkflowCancellationRequestStatus.Requested);
        else (await handler.HandleAsync(cancelA, token)).Should().BeTrue();
        var winner = (await events.LoadTailAsync(new(child), StreamVersion.Empty, token))
            .OfType<WorkflowCancellationRequestedEvent>().Should().ContainSingle().Which;
        if (independentFirst) (await handler.HandleAsync(cancelA, token)).Should().BeTrue();
        else (await handle.RequestCancellationAsync(token)).Should().Be(WorkflowCancellationRequestStatus.AlreadyTerminal);
        var requests = (await events.LoadTailAsync(new(child), StreamVersion.Empty, token)).OfType<WorkflowCancellationRequestedEvent>();
        requests.Should().ContainSingle().Which.CommandId.Should().Be(winner.CommandId);
        (await services.GetRequiredService<DurableDagChildPump>().PumpOnceAsync(poll, token)).Should().Be(1);
        var after = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
        after.Nodes[0].State.Should().Be(independentFirst ? DagNodeStateV1.Failed : DagNodeStateV1.Cancelled);
        after.Nodes[0].FailureCode.Should().Be(independentFirst ? "CHILD_CANCELLED" : null);
        after.Nodes[1].State.Should().Be(DagNodeStateV1.CancellationRequested);
    }

    [Fact]
    public Task JoinRace_LostAckThenParentCancellation_ReplaysTheCommittedJoin() =>
        CheckIndependentChildCancellationAsync(loseAcknowledgement: true);

    [Fact]
    public Task JoinRace_ParentCancellationBeforeFirstJoin_PreservesIndependentCause() =>
        CheckIndependentChildCancellationAsync(loseAcknowledgement: false);

    private static async Task CheckIndependentChildCancellationAsync(bool loseAcknowledgement)
    {
        var token = TestContext.Current.CancellationToken;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        await using var services = Services(workflow);
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        var run = await StartTwoIndependentChildrenAsync(services, workflow, token);
        run.Nodes.Should().HaveCount(2).And.OnlyContain(node =>
            node.State == DagNodeStateV1.StartAuthorized && node.ChildInstanceId != null);
        var stream = WorkflowStreamId.ForDagRun(run.RunId);
        var childId = run.Nodes[0].ChildInstanceId!;
        var first = await services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference)
            .GetInstanceAsync(childId, token);
        await first.RequestCancellationAsync(token);
        (await first.GetSnapshotAsync(token)).Status.Should().Be(WorkflowInstanceStatus.Cancelled);
        (await DagPumpRunFixture.ReadAsync(events, run.RunId, token)).State.Should().Be(DagRunStateV1.Running);

        var poll = Request();
        OutboxWrite? originalClaim = null;
        if (loseAcknowledgement)
        {
            originalClaim = (await outbox.ClaimAsync(poll with
            {
                KindSelector = OutboxKindSelector.Including("dag-child-join-v1")
            }, token)).Should().ContainSingle().Which;
            originalClaim.DispatchAttempt.Should().Be(1);
            (await handler.HandleAsync(originalClaim, token)).Should().BeTrue();
            var joined = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
            joined.State.Should().Be(DagRunStateV1.Running);
            joined.Nodes[0].State.Should().Be(DagNodeStateV1.Failed);
            joined.Nodes[0].FailureCode.Should().Be("CHILD_CANCELLED");
            // The handler committed. Its worker stopped before MarkAsync.
            (await outbox.GetStateAsync(originalClaim.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Claimed);
        }

        var bridge = services.GetRequiredService<IDurableDagRunBridgeV1>();
        (await bridge.RequestRunCancellationAsync(run.RunId, token)).Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
        var barrier = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
        barrier.State.Should().Be(DagRunStateV1.CancellationRequested);
        barrier.Nodes[1].State.Should().Be(DagNodeStateV1.CancellationRequested);
        barrier.Nodes[1].ChildInstanceId.Should().Be(run.Nodes[1].ChildInstanceId);
        // Other workers hold the cancel notices. Only A's join will be dispatched by our worker.
        var cancels = await outbox.ClaimAsync(poll with
        {
            LeaseDuration = TimeSpan.FromMinutes(5),
            KindSelector = OutboxKindSelector.Including("dag-child-cancel-v1")
        }, token);
        cancels.Should().HaveCount(loseAcknowledgement ? 1 : 2);
        var recorder = new RecordingChildHandler(handler);
        var pump = new DurableDagChildPump(outbox, recorder);
        var delivered = await pump.PumpOnceAsync(poll with { ClaimedAt = poll.ClaimedAt + poll.LeaseDuration }, token);
        var notice = recorder.Handled.Should().ContainSingle().Which;
        notice.Kind.Should().Be("dag-child-join-v1");
        notice.DispatchAttempt.Should().Be(loseAcknowledgement ? 2 : 1);
        if (originalClaim is not null)
        {
            notice.OutboxRecordId.Should().Be(originalClaim.OutboxRecordId);
            notice.Payload.Should().Equal(originalClaim.Payload);
        }

        var after = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
        var dispatch = (await outbox.GetDispatchSnapshotAsync(notice.OutboxRecordId, token)).Value;
        var history = await events.LoadDagRunTailAsync(stream, StreamVersion.Empty, token);
        var join = history.Where(r => r.Kind == DagRunRecordKindV1.ChildJoined).Should().ContainSingle().Which;
        var change = join.ChangedNodes.Should().ContainSingle().Which;
        change.ChildInstanceId.Should().Be(childId);
        change.Ordinal.Should().Be(0);
        (join.StreamVersion.Value < barrier.StreamVersion.Value).Should().Be(loseAcknowledgement);
        after.Nodes[1].Should().BeEquivalentTo(barrier.Nodes[1]);
        using var assertions = new AssertionScope();
        change.State.Should().Be(DagNodeStateV1.Failed);
        change.FailureCode.Should().Be("CHILD_CANCELLED");
        after.Nodes[0].State.Should().Be(DagNodeStateV1.Failed);
        after.Nodes[0].FailureCode.Should().Be("CHILD_CANCELLED");
        after.State.Should().Be(DagRunStateV1.CancellationRequested);
        history.Select(r => r.Kind).Should().NotContain(DagRunRecordKindV1.IntegrityFaulted);
        delivered.Should().Be(1);
        dispatch.State.Should().Be(OutboxRecordState.Dispatched, $"the worker reported {dispatch.PoisonCode}: {dispatch.PoisonDetail}");
    }

    private static async Task<DagRunSnapshotV1> StartTwoIndependentChildrenAsync(
        IServiceProvider services, DurableWorkflowDefinition<int, int> workflow, CancellationToken token)
    {
        var plan = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var child = new DagRunBindingV1(workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint.Value);
        var key = services.GetRequiredService<DurableDagRunInputCatalog>().Install(plan, typeof(int));
        var prefix = $"workflow:{child.DefinitionId}:{child.DefinitionVersion.Value}";
        var graph = new DagRunGraphV1(plan, key,
            [new(0, "first", child, prefix + ":input", prefix + ":output", []),
             new(1, "second", child, prefix + ":input", prefix + ":output", [])]);
        var store = services.GetRequiredService<DurableDagRunStore>();
        var id = Guid.NewGuid();
        (await store.CreateOrReopenAsync(new(id, new("join-race"), graph, 5, typeof(int)), token)).Outcome
            .Should().Be(DurableDagCreateOutcomeV1.Created);
        var bridge = services.GetRequiredService<IDurableDagRunBridgeV1>();
        var binding = new DurableDagWorkflowBindingV1(workflow.DefinitionId, workflow.DefinitionVersion,
            workflow.DefinitionFingerprint, typeof(int), typeof(int));
        for (var ordinal = 0; ordinal < 2; ordinal++)
        {
            var receipt = await bridge.PrepareMappingAsync(new(new(id, ordinal), binding, []), new(7, typeof(int)), false, token);
            var turn = (await bridge.BeginTurnAsync(new(id, Guid.NewGuid(), DurableDagTurnPurposeV1.Admission), token)).Turn!;
            try
            {
                (await bridge.AdmitPreparedMappingAsync(receipt, turn.Fence, turn.Version, 2, token)).Outcome
                    .Should().Be(DurableDagMutationOutcomeV1.Committed);
            }
            finally { await bridge.EndTurnAsync(turn, token); }
        }
        (await services.GetRequiredService<DurableDagChildPump>().PumpOnceAsync(Request(), token)).Should().Be(2);
        return (await store.ReadAsync(id, token))!;
    }

    private sealed class RecordingChildHandler(IDurableDagChildHandler inner) : IDurableDagChildHandler
    {
        internal List<OutboxWrite> Handled { get; } = [];

        public Task<bool> HandleAsync(OutboxWrite record, CancellationToken token)
        {
            Handled.Add(record);
            return inner.HandleAsync(record, token);
        }

        public Task<bool> RecordFaultAsync(Guid runId, CancellationToken token) => inner.RecordFaultAsync(runId, token);
    }

    [Fact]
    public async Task JoinRace_ChangedTerminalOutputOnRedelivery_FaultsWithoutASecondJoin()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var token = timeout.Token;
        var workflow = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value + 1).Build();
        await using var services = Services(workflow);
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var (run, _) = await DagPumpRunFixture.CreateAsync(events, workflow, false, token, branches: true);
        var pump = services.GetRequiredService<DurableDagChildPump>();
        (await pump.PumpOnceAsync(Request(), token)).Should().Be(1);
        var child = run.Nodes[0].ChildInstanceId!;
        var driver = services.GetServices<IHostedService>().OfType<OrcaCoreContinuationPumpHostedService>().Single();
        await driver.StartAsync(token);
        try
        {
            var handle = await services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference)
                .GetInstanceAsync(child, token);
            (await handle.WaitForOutputAsync(token)).Should().Be(42);
        }
        finally { await driver.StopAsync(CancellationToken.None); }

        var poll = Request();
        var original = (await outbox.ClaimAsync(poll with
        {
            KindSelector = OutboxKindSelector.Including("dag-child-join-v1")
        }, token)).Should().ContainSingle().Which;
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        (await handler.HandleAsync(original, token)).Should().BeTrue();
        var joined = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
        joined.State.Should().Be(DagRunStateV1.Running);
        joined.Nodes[0].Output!.Bytes.Should().Equal("42"u8.ToArray());
        // Inject corruption after commit. The new bytes are valid for the same output schema.
        var checkpoint = (await events.LoadCheckpointAsync(child, token)).Value;
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        var changed = envelope with { Output = envelope.Output! with { Payload = "43"u8.ToArray() } };
        (await events.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new(child), ExpectedVersion = checkpoint.StreamVersion,
            Checkpoint = checkpoint with { Payload = changed.Serialize() }
        }, token)).IsSuccess.Should().BeTrue();
        (await pump.PumpOnceAsync(poll with { ClaimedAt = poll.ClaimedAt + poll.LeaseDuration }, token)).Should().Be(0);
        var disposition = (await outbox.GetDispatchSnapshotAsync(original.OutboxRecordId, token)).Value;
        disposition.State.Should().Be(OutboxRecordState.Poisoned);
        disposition.PoisonCode.Should().Be("DAG_PROTOCOL_INVALID");
        disposition.PoisonDetail.Should().Contain("joined outcome conflicts");
        var after = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
        after.State.Should().Be(DagRunStateV1.IntegrityFault);
        after.Nodes.Should().BeEquivalentTo(joined.Nodes);
        (await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(run.RunId), joined.StreamVersion, token))
            .Select(r => r.Kind).Should().Equal(DagRunRecordKindV1.IntegrityFaulted);
    }
}
