using System.Collections.Concurrent;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting.Services;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlChildStartCancellationOverlap(bool insideBody) =>
        RunProcessRestartAsync(RestartDatabase.PostgreSql, nameof(ChildStartOverlapWorker), insideBody, nameof(ChildStartCancellationWorker));

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerChildStartCancellationOverlap(bool insideBody) =>
        RunProcessRestartAsync(RestartDatabase.SqlServer, nameof(ChildStartOverlapWorker), insideBody, nameof(ChildStartCancellationWorker));

    private sealed record AdmissionEvidence(int DriverPid, Guid ChildId, long Version, string EventId);
    private sealed record OverlapGate(AdmissionEvidence Admission, Guid[] ContinuationIds);
    private sealed record OverlapObservation(bool InsideBody, int DriverPid, long AdmissionVersion, int EntriesBeforeRelease,
        string Returned, int DurableState, int DurableStepCompletions);
    private sealed record BodyReturn(int DriverPid, Guid ChildId, string OperationId, int Attempt, string Result);

    public static bool IsOverlapWorker => Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PHASE") is "driver" or "recover";

    [Fact(Explicit = true, SkipUnless = nameof(IsOverlapWorker), Skip = "Invoked by the SQL child-start overlap tests.")]
    public async Task ChildStartOverlapWorker()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_DIRECTORY")!;
        var insideBody = bool.Parse(Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_SCHEDULE")!);
        var seed = JsonSerializer.Deserialize<ChildStartSeed>(await File.ReadAllTextAsync(Path.Combine(directory, "seed.json"), token))!;
        seed.SeedPid.Should().NotBe(Environment.ProcessId);
        if (Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PHASE") == "driver")
            await DriveToOverlapGateAsync(seed, insideBody, directory, token);
        else
            await CancelDuringDriveAsync(seed, insideBody, directory, token);
    }

    private static async Task DriveToOverlapGateAsync(ChildStartSeed seed, bool insideBody, string directory, CancellationToken token)
    {
        var admission = new TaskCompletionSource<AdmissionEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continues = new ConcurrentQueue<Guid>([seed.ContinueId]);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        void HoldDriver()
        {
            paused.SetResult();
            release.Wait(token);
        }

        var calls = Path.Combine(directory, "step-calls");
        await using var services = await CreateRestartServicesAsync(MarkerDefinition(), token, collection =>
        {
            var sql = collection.Last(descriptor => descriptor.ServiceType == typeof(IWorkflowEventStore));
            collection.AddSingleton<IWorkflowEventStore>(sp => new DagEventStoreProbe
            {
                Inner = (IWorkflowEventStore)sql.ImplementationFactory!(sp),
                AfterAppend = batch =>
                {
                    if (batch.StreamId.InstanceId.Value != seed.ChildId) return;
                    foreach (var wake in batch.OutboxRecords.Where(record => record.Kind == "continue"))
                        continues.Enqueue(wake.OutboxRecordId.Value);
                    if (batch.Checkpoint?.LastStepPath?.EndsWith(":attempt-admission", StringComparison.Ordinal) == true &&
                        batch.Events is [WorkflowExecutionPositionChangedEvent position])
                        admission.SetResult(new(Environment.ProcessId, seed.ChildId, batch.Checkpoint.StreamVersion.Value, position.EventId.Value));
                }
            });
            collection.AddSingleton(_ =>
            {
                // Named step resolution runs after admission and the segment budget check, before ExecuteAsync.
                if (!insideBody) HoldDriver();
                return new MarkerStep(calls, insideBody ? HoldDriver : null, (execution, result) =>
                    File.AppendAllLines(Path.Combine(directory, "body-returns"),
                        [JsonSerializer.Serialize(new BodyReturn(Environment.ProcessId, execution.WorkflowInstanceId.Value,
                            execution.OperationId.ToString(), execution.AttemptNumber, result.GetType().Name))]));
            });
        });

        // A budget-exhausted segment may dispatch the original wake and resume A on its successor.
        // Keep the real hosted service running through the physical gate and settle both wakes.
        var pump = services.GetServices<IHostedService>().OfType<OrcaCoreContinuationPumpHostedService>().Single();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        await pump.StartAsync(token);
        try
        {
            if (await Task.WhenAny(paused.Task, pump.ExecuteTask!).WaitAsync(token) != paused.Task)
            {
                await pump.ExecuteTask!;
                throw new InvalidOperationException("Continuation pump exited before the selected overlap gate.");
            }
            admission.Task.IsCompletedSuccessfully.Should().BeTrue("the gate must follow the committed first attempt admission");
            var path = Path.Combine(directory, "paused.json");
            var gate = new OverlapGate(await admission.Task, continues.Distinct().ToArray());
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(gate), token);
            File.Move(path + ".tmp", path);
            while (!File.Exists(Path.Combine(directory, "release-driver")))
                await Task.Delay(TimeSpan.FromMilliseconds(25), TimeProvider.System, token);
            release.Set();
            foreach (var id in gate.ContinuationIds)
            {
                OutboxRecordState state;
                while ((state = (await outbox.GetStateAsync(new(id), token)).Value) is
                       OutboxRecordState.Pending or OutboxRecordState.Claimed or OutboxRecordState.Retryable)
                    await Task.Delay(TimeSpan.FromMilliseconds(25), TimeProvider.System, token);
                state.Should().Be(OutboxRecordState.Dispatched);
            }
        }
        finally
        {
            release.Set();
            await pump.StopAsync(token);
        }
    }

    private static async Task CancelDuringDriveAsync(ChildStartSeed seed, bool insideBody, string directory, CancellationToken token)
    {
        var calls = Path.Combine(directory, "step-calls");
        var returns = Path.Combine(directory, "body-returns");
        await using var services = await CreateRestartServicesAsync(MarkerDefinition(), token,
            collection => collection.AddSingleton(new MarkerStep(calls)));
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var child = InstanceId.Parse(seed.ChildId.ToString("D"));
        var stream = new WorkflowStreamId(child);
        var barrier = await CheckSeedForRecoveryAsync(services, seed, calls, token);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        var driver = RunRestartWorkerAsync("driver", Enum.Parse<RestartDatabase>(Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PROVIDER")!),
            Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_CONNECTION")!, directory, nameof(ChildStartOverlapWorker), insideBody, deadline.Token);
        try
        {
            var path = Path.Combine(directory, "paused.json");
            while (!File.Exists(path))
            {
                if (driver.IsCompleted)
                    throw new InvalidOperationException($"Driver {await driver} exited before the overlap gate.");
                await Task.Delay(TimeSpan.FromMilliseconds(25), TimeProvider.System, token);
            }
            var (gate, continuationIds) = JsonSerializer.Deserialize<OverlapGate>(await File.ReadAllTextAsync(path, token))!;
            gate.ChildId.Should().Be(seed.ChildId);
            gate.DriverPid.Should().NotBe(seed.SeedPid).And.NotBe(Environment.ProcessId);
            var admission = (await events.LoadCheckpointAsync(child, token)).Value;
            admission.StreamVersion.Value.Should().Be(gate.Version);
            admission.LastStepPath.Should().EndWith(":attempt-admission");
            var fiber = DurableExecutionEnvelopeV2.Deserialize(admission.Payload).Fibers.Should().ContainSingle().Which;
            fiber.AttemptInFlight.Should().BeTrue();
            fiber.RetryAttempt.Should().Be(1);
            var admitted = await events.LoadTailAsync(stream, StreamVersion.Empty, token);
            admitted.Last().Should().BeOfType<WorkflowExecutionPositionChangedEvent>().Which.EventId.Value.Should().Be(gate.EventId);
            admitted.OfType<WorkflowCancellationRequestedEvent>().Should().BeEmpty();
            continuationIds.Should().Contain(seed.ContinueId);
            var wakeStates = new List<OutboxRecordState>();
            foreach (var id in continuationIds)
                wakeStates.Add((await outbox.GetStateAsync(new(id), token)).Value);
            wakeStates.Should().Contain(OutboxRecordState.Claimed);
            var atGate = File.Exists(calls) ? await File.ReadAllLinesAsync(calls, token) : [];
            atGate.Should().Equal(insideBody ? [$"{gate.DriverPid} {child}"] : Array.Empty<string>());
            File.Exists(returns).Should().BeFalse();

            var cancellation = await ReclaimOwningCancelAsync(services, seed, continuationFirst: false, token);
            var cancelled = await events.LoadTailAsync(stream, StreamVersion.Empty, token);
            cancelled.Take(admitted.Count).Select(e => e.EventId).Should().Equal(admitted.Select(e => e.EventId));
            cancelled.Skip(admitted.Count).Select(e => e.GetType()).Should().Equal(
                typeof(WorkflowCancellationRequestedEvent), typeof(WorkflowTerminalEvent));
            cancelled.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowInstanceStatus.Cancelled);
            var checkpoint = (await events.LoadCheckpointAsync(child, token)).Value;
            var state = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
            checkpoint.Status.Should().Be(WorkflowInstanceStatus.Cancelled);
            var durableState = JsonSerializer.Deserialize<int>(state.StatePayload);
            durableState.Should().Be(7);
            state.Output.Should().BeNull();
            var beforeRelease = File.Exists(calls) ? await File.ReadAllLinesAsync(calls, token) : [];
            beforeRelease.Should().Equal(atGate);
            File.Exists(returns).Should().BeFalse();
            (await CheckWaitingForJoinAsync(events, seed.RunId, token)).StreamVersion.Should().Be(barrier.StreamVersion);

            // Only the independent cancel worker publishes release, after fresh SQL reads prove the winner.
            await File.WriteAllBytesAsync(Path.Combine(directory, "release-driver"), [], token);
            (await driver).Should().Be(gate.DriverPid);
            var returned = JsonSerializer.Deserialize<BodyReturn>((await File.ReadAllLinesAsync(returns, token)).Should().ContainSingle().Which)!;
            returned.Should().Be(new BodyReturn(gate.DriverPid, seed.ChildId, fiber.LogicalOperationKey!, 1, nameof(StepResult.Completed)));
            var after = await events.LoadTailAsync(stream, StreamVersion.Empty, token);
            after.Select(e => e.EventId).Should().Equal(cancelled.Select(e => e.EventId));
            var final = (await events.LoadCheckpointAsync(child, token)).Value;
            final.StreamVersion.Should().Be(checkpoint.StreamVersion);
            final.Payload.Should().Equal(checkpoint.Payload);
            foreach (var id in continuationIds)
                (await outbox.GetStateAsync(new(id), token)).Value.Should().Be(OutboxRecordState.Dispatched);
            await VerifyJoinAndReplayAsync(services, seed, barrier, cancellation, false, gate.DriverPid, calls, directory, token,
                new(insideBody, gate.DriverPid, gate.Version, beforeRelease.Length, returned.Result,
                    durableState, after.OfType<WorkflowStepCompletedEvent>().Count()));
        }
        finally
        {
            await deadline.CancelAsync();
            // Await process-tree cleanup even when the cancellation worker failed while the driver was held.
            if (!driver.IsCompleted)
                try { await driver; } catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }
}
