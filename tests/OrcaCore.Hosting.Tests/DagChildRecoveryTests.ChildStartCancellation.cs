using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Hosting.Services;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    private const string StartNotice = "dag-child-start-v1";
    private const string CancelNotice = "dag-child-cancel-v1";
    private const string JoinNotice = "dag-child-join-v1";

    private sealed record ChildStartSeed(int SeedPid, Guid RunId, Guid ChildId, Guid StartId, Guid CancelId, Guid ContinueId);

    private sealed class MarkerStep(string path) : IStep<int>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken token)
        {
            // Count physical body entry even if a stale dispatch supplied an already-cancelled token.
            await File.AppendAllLinesAsync(path, [$"{Environment.ProcessId} {context.Execution.WorkflowInstanceId}"], CancellationToken.None);
            context.ReplaceState(context.State + 1);
            return new StepResult.Completed();
        }
    }

    [Fact(Explicit = true, SkipUnless = nameof(IsRestartWorker), Skip = "Invoked by the SQL child-start restart tests.")]
    public async Task ChildStartCancellationWorker()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_DIRECTORY")!;
        var marker = Path.Combine(directory, "seed.json");
        var calls = Path.Combine(directory, "step-calls");
        var seeding = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PHASE") == "seed";
        var definition = Workflow.Durable<int>(DefinitionId.Parse("8fcfe3b1-51d9-4af7-9c1e-b170645835a6"), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<MarkerStep>().End(state => state.Value).Build();

        var committed = new TaskCompletionSource<ProviderCommitBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        await using var services = await CreateRestartServicesAsync(definition, token, collection =>
        {
            collection.AddSingleton(new MarkerStep(calls));
            if (!seeding) return;
            var sql = collection.Last(descriptor => descriptor.ServiceType == typeof(IWorkflowEventStore));
            collection.AddSingleton<IWorkflowEventStore>(sp => new DagEventStoreProbe
            {
                Inner = (IWorkflowEventStore)sql.ImplementationFactory!(sp),
                AfterAppend = batch =>
                {
                    if (batch.Events.Any(e => e is WorkflowStartedEvent) && committed.TrySetResult(batch))
                        resume.Wait(token); // Hold the owning cancel handler inside its successful child-start append.
                }
            });
        });

        if (seeding)
        {
            try
            {
                var seed = await MaterializeDuringCancelAsync(services, definition, committed.Task, token);
                File.Exists(calls).Should().BeFalse();
                await File.WriteAllTextAsync(marker + ".tmp", JsonSerializer.Serialize(seed), token);
                File.Move(marker + ".tmp", marker);
                await Task.Delay(Timeout.Infinite, token); // Controller kills this worker with the handler still blocked.
            }
            finally { resume.Set(); }
            return;
        }

        var boundary = JsonSerializer.Deserialize<ChildStartSeed>(await File.ReadAllTextAsync(marker, token))!;
        boundary.SeedPid.Should().NotBe(Environment.ProcessId);
        var continuationFirst = bool.Parse(Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_SCHEDULE")!);
        await RecoverChildStartAsync(services, boundary, continuationFirst, calls, directory, token);
    }

    private static async Task<ChildStartSeed> MaterializeDuringCancelAsync(ServiceProvider services,
        DurableWorkflowDefinition<int, int> definition, Task<ProviderCommitBatch> committed, CancellationToken token)
    {
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var admitted = await AdmitIndependentChildrenAsync(services, definition, 1, token);
        admitted.Nodes.Select(n => n.State).Should().Equal(DagNodeStateV1.StartAuthorized, DagNodeStateV1.Ready);
        var runId = admitted.RunId;
        var child = admitted.Nodes[0].ChildInstanceId!;
        child.Value.Should().Be(DagPumpRunFixture.ChildId(runId, 0));
        await RequestParentCancellationAsync(services, runId, token);
        var barrier = await CheckWaitingForJoinAsync(events, runId, token);
        barrier.Nodes[0].ChildInstanceId.Should().Be(child);
        (await events.LoadTailAsync(new(child), StreamVersion.Empty, token)).Should().BeEmpty();

        // Another claimant holds the original start, so only the real cancellation handler can materialize A.
        var heldStart = (await outbox.ClaimAsync(Request() with
        {
            KindSelector = OutboxKindSelector.Including(StartNotice), LeaseDuration = TimeSpan.FromMinutes(5)
        }, token)).Should().ContainSingle().Which;
        CheckNotice(heldStart, StartNotice, runId);
        var recorder = new RecordingChildHandler(services.GetRequiredService<IDurableDagChildHandler>());
        var pump = new DurableDagChildPump(outbox, recorder);
        var dispatch = Task.Run(() => pump.PumpOnceAsync(Request() with { LeaseDuration = TimeSpan.FromSeconds(2) }, token), token);
        if (await Task.WhenAny(committed, dispatch).WaitAsync(token) == dispatch)
            throw new InvalidOperationException($"Cancel dispatch returned {await dispatch} before the child-start commit.");
        var batch = await committed;
        batch.StreamId.Should().Be(new WorkflowStreamId(child));
        var continuation = batch.OutboxRecords.Should().ContainSingle(w => w.Kind == "continue").Which;
        var cancel = recorder.Handled.Should().ContainSingle().Which;
        CheckNotice(cancel, CancelNotice, runId);
        cancel.DispatchAttempt.Should().Be(1);
        (await outbox.GetStateAsync(cancel.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Claimed);
        (await outbox.GetStateAsync(continuation.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Pending);
        (await outbox.ReleaseAsync(heldStart.OutboxRecordId, heldStart.DispatchAttempt, token)).Should().BeTrue();
        await CheckOnlyStartAsync(events, child, runId, token);
        return new(Environment.ProcessId, runId, child.Value, heldStart.OutboxRecordId.Value,
            cancel.OutboxRecordId.Value, continuation.OutboxRecordId.Value);
    }

    private static async Task RecoverChildStartAsync(ServiceProvider services, ChildStartSeed seed, bool continuationFirst,
        string calls, string directory, CancellationToken token)
    {
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var child = InstanceId.Parse(seed.ChildId.ToString("D"));
        var barrier = await CheckWaitingForJoinAsync(events, seed.RunId, token);
        barrier.Nodes[0].ChildInstanceId.Should().Be(child);
        await CheckOnlyStartAsync(events, child, seed.RunId, token);
        File.Exists(calls).Should().BeFalse();
        (await outbox.GetStateAsync(new(seed.ContinueId), token)).Value.Should().Be(OutboxRecordState.Pending);
        (await outbox.GetStateAsync(new(seed.CancelId), token)).Value.Should().Be(OutboxRecordState.Claimed);

        if (continuationFirst) await DrainContinuationAsync(services, seed.ContinueId, token);

        // Keep the late start and any already-committed join out of the DAG pump's claim set.
        var held = await outbox.ClaimAsync(Request() with
        {
            KindSelector = OutboxKindSelector.Including(StartNotice, JoinNotice), LeaseDuration = TimeSpan.FromMinutes(5)
        }, token);
        held.Select(w => w.Kind).Should().BeEquivalentTo(continuationFirst ? [StartNotice, JoinNotice] : new[] { StartNotice });
        held.Single(w => w.Kind == StartNotice).OutboxRecordId.Value.Should().Be(seed.StartId);
        var handler = services.GetRequiredService<IDurableDagChildHandler>();
        var recorder = new RecordingChildHandler(handler);
        var pump = new DurableDagChildPump(outbox, recorder);
        // Reclaim the seed's cancellation after the actual provider lease expires.
        while (await pump.PumpOnceAsync(Request(), token) == 0 && recorder.Handled.Count == 0)
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
        var cancel = recorder.Handled.Should().ContainSingle().Which;
        CheckNotice(cancel, CancelNotice, seed.RunId);
        cancel.OutboxRecordId.Value.Should().Be(seed.CancelId);
        cancel.DispatchAttempt.Should().Be(2);
        (await outbox.GetStateAsync(cancel.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Dispatched);

        if (!continuationFirst) await DrainContinuationAsync(services, seed.ContinueId, token);
        var actualCalls = File.Exists(calls) ? await File.ReadAllLinesAsync(calls, token) : [];
        actualCalls.Should().Equal(continuationFirst ? [$"{Environment.ProcessId} {child}"] : Array.Empty<string>());
        var childHistory = await events.LoadTailAsync(new(child), StreamVersion.Empty, token);
        var terminal = childHistory.OfType<WorkflowTerminalEvent>().Should().ContainSingle().Which;
        terminal.Status.Should().Be(continuationFirst ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Cancelled);
        childHistory.OfType<WorkflowCompletedEvent>().Should().HaveCount(continuationFirst ? 1 : 0);
        childHistory.OfType<WorkflowStartedEvent>().Should().ContainSingle();
        childHistory.OfType<WorkflowStepCompletedEvent>().Should().HaveCount(continuationFirst ? 1 : 0);
        childHistory.OfType<WorkflowCancellationRequestedEvent>().Should().HaveCount(continuationFirst ? 0 : 1);
        (await CheckWaitingForJoinAsync(events, seed.RunId, token)).StreamVersion.Should().Be(barrier.StreamVersion);

        foreach (var claim in held)
            (await outbox.ReleaseAsync(claim.OutboxRecordId, claim.DispatchAttempt, token)).Should().BeTrue();
        (await pump.PumpOnceAsync(Request(), token)).Should().Be(2);
        var late = recorder.Handled.Skip(1).ToArray();
        late.Select(w => w.Kind).Should().BeEquivalentTo([StartNotice, JoinNotice]);
        late.Single(w => w.Kind == StartNotice).OutboxRecordId.Value.Should().Be(seed.StartId);
        foreach (var notice in late)
        {
            CheckNotice(notice, notice.Kind, seed.RunId);
            (await outbox.GetStateAsync(notice.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        }
        var run = await DagPumpRunFixture.ReadAsync(events, seed.RunId, token);
        run.State.Should().Be(DagRunStateV1.Cancelled);
        var node = run.Nodes[0];
        node.State.Should().Be(continuationFirst ? DagNodeStateV1.Succeeded : DagNodeStateV1.Cancelled);
        node.ChildInstanceId.Should().Be(child);
        node.FailureCode.Should().BeNull();
        if (continuationFirst) node.Output!.Bytes.Should().Equal("8"u8.ToArray());
        else node.Output.Should().BeNull();
        run.Nodes[1].Should().BeEquivalentTo(barrier.Nodes[1]);
        var history = await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(seed.RunId), StreamVersion.Empty, token);
        history.Select(r => r.Kind).Should().Equal(DagRunRecordKindV1.Created, DagRunRecordKindV1.InputAdmitted,
            DagRunRecordKindV1.CancellationRequested, DagRunRecordKindV1.ChildJoined, DagRunRecordKindV1.Terminal);
        var joined = history.Single(r => r.Kind == DagRunRecordKindV1.ChildJoined).ChangedNodes.Should().ContainSingle().Which;
        joined.Ordinal.Should().Be(0);
        joined.ChildInstanceId.Should().Be(child);
        joined.State.Should().Be(node.State);

        foreach (var notice in recorder.Handled)
            (await handler.HandleAsync(notice, token)).Should().BeTrue();
        (await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(seed.RunId), run.StreamVersion, token)).Should().BeEmpty();
        var afterReplay = await events.LoadTailAsync(new(child), StreamVersion.Empty, token);
        afterReplay.Select(e => e.EventId).Should().Equal(childHistory.Select(e => e.EventId));
        (File.Exists(calls) ? await File.ReadAllLinesAsync(calls, token) : []).Should().Equal(actualCalls);
        var b = InstanceId.Parse(DagPumpRunFixture.ChildId(seed.RunId, 1).ToString("D"));
        (await events.LoadTailAsync(new(b), StreamVersion.Empty, token)).Should().BeEmpty();
        (await events.LoadCheckpointAsync(b, token)).HasValue.Should().BeFalse();

        await File.WriteAllTextAsync(Path.Combine(directory, "recovered.json"), JsonSerializer.Serialize(new
        {
            WorkerPid = Environment.ProcessId, continuationFirst, seed.RunId, seed.ChildId, FirstStepCalls = actualCalls.Length,
            ChildTerminal = terminal.Status.ToString(), CancelAttempt = cancel.DispatchAttempt, JoinedAs = node.State.ToString(),
            ParentTerminal = run.State.ToString(), ParentVersion = run.StreamVersion.Value
        }), token);
    }

    private static void CheckNotice(OutboxWrite notice, string kind, Guid runId)
    {
        notice.Kind.Should().Be(kind);
        notice.DagRunOwner.Should().Be(runId);
        notice.Payload.Should().Equal(DagPumpRunFixture.ChildWrite(kind, runId).Payload);
    }

    private static async Task<DagRunSnapshotV1> CheckWaitingForJoinAsync(IWorkflowEventStore events, Guid runId, CancellationToken token)
    {
        var run = await DagPumpRunFixture.ReadAsync(events, runId, token);
        run.State.Should().Be(DagRunStateV1.CancellationRequested);
        run.Nodes.Select(n => n.State).Should().Equal(DagNodeStateV1.CancellationRequested, DagNodeStateV1.Cancelled);
        run.Nodes[1].ChildInstanceId.Should().BeNull();
        run.Nodes[1].Input.Should().BeNull();
        return run;
    }

    private static async Task CheckOnlyStartAsync(IWorkflowEventStore events, InstanceId child, Guid runId, CancellationToken token)
    {
        var history = await events.LoadTailAsync(new(child), StreamVersion.Empty, token);
        var start = history.Should().ContainSingle().Which.Should().BeOfType<WorkflowStartedEvent>().Which;
        start.InstanceId.Should().Be(child);
        start.ParentInstanceId!.Value.Should().Be(runId);
        start.RootInstanceId.Should().Be(start.ParentInstanceId);
        start.InputPayload.Should().Equal("7"u8.ToArray());
    }

    private static async Task DrainContinuationAsync(ServiceProvider services, Guid continuationId, CancellationToken token)
    {
        var driver = services.GetServices<IHostedService>().OfType<OrcaCoreContinuationPumpHostedService>().Single();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        await driver.StartAsync(token);
        try
        {
            OutboxRecordState state;
            while ((state = (await outbox.GetStateAsync(new(continuationId), token)).Value) is
                   OutboxRecordState.Pending or OutboxRecordState.Claimed or OutboxRecordState.Retryable)
                await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
            state.Should().Be(OutboxRecordState.Dispatched);
        }
        finally { await driver.StopAsync(token); }
    }
}
