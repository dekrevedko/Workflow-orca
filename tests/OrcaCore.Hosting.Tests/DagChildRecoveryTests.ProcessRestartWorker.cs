using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    public static bool IsRestartWorker => Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PHASE") is "seed" or "recover";

    [Fact(Explicit = true, SkipUnless = nameof(IsRestartWorker), Skip = "Run through the database restart tests.")]
    public async Task DatabaseRestartWorker()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_DIRECTORY")!;
        var loseAck = bool.Parse(Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_SCHEDULE")!);
        var workflow = Workflow.Durable<int>(DefinitionId.Parse("824a2e15-67cb-4fa6-8695-725c7266cd28"), DefinitionVersion.Initial)
            .Init<int>(value => value).End(state => state.Value).Build();
        await using var services = await CreateRestartServicesAsync(workflow, token);

        var marker = Path.Combine(directory, "seed.json");
        if (Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PHASE") == "seed")
        {
            var coordinates = await SeedRestartAsync(services, workflow, loseAck, token);
            await File.WriteAllTextAsync(marker + ".tmp", JsonSerializer.Serialize(coordinates), token);
            File.Move(marker + ".tmp", marker);
            await Task.Delay(Timeout.Infinite, token); // The controller hard-kills this process.
            return;
        }
        var seed = JsonSerializer.Deserialize<RestartCoordinates>(await File.ReadAllTextAsync(marker, token))!;
        seed.SeedPid.Should().NotBe(Environment.ProcessId);
        var observation = await VerifyAfterRestartAsync(services, seed, loseAck, token);
        await File.WriteAllTextAsync(Path.Combine(directory, "recovered.json"), JsonSerializer.Serialize(observation), token);
    }

    private static async Task<ServiceProvider> CreateRestartServicesAsync(DurableWorkflowDefinition<int, int> workflow,
        CancellationToken token, Action<IServiceCollection>? configure = null)
    {
        var collection = new ServiceCollection();
        var connection = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_CONNECTION")!;
        var provider = Environment.GetEnvironmentVariable("ORCA_DAG_RESTART_PROVIDER");
        Type providerOptions;
        switch (provider)
        {
            case nameof(RestartDatabase.PostgreSql):
                collection.AddOrcaCorePostgreSqlDurableProvider(new(connection, "public"));
                providerOptions = typeof(PostgreSqlDurableProviderOptions);
                break;
            case nameof(RestartDatabase.SqlServer):
                collection.AddOrcaCoreSqlServerDurableProvider(new(connection, "dag_restart"));
                providerOptions = typeof(SqlServerDurableProviderOptions);
                break;
            default:
                throw new InvalidOperationException($"Unknown DAG restart provider '{provider}'.");
        }
        configure?.Invoke(collection);
        collection.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("dag-restart"), Pools = [] }
        }).AddWorkflow(workflow);
        var services = collection.BuildServiceProvider();
        // Initialize the provider schema while keeping competing background pumps stopped.
        await services.GetServices<IHostedService>().Single(service =>
            service.GetType().Assembly == providerOptions.Assembly).StartAsync(token);
        return services;
    }

    private static async Task<RestartCoordinates> SeedRestartAsync(ServiceProvider services,
        DurableWorkflowDefinition<int, int> workflow, bool loseAck, CancellationToken token)
    {
        var run = await StartTwoIndependentChildrenAsync(services, workflow, token);
        run.Nodes.Should().HaveCount(2).And.OnlyContain(node =>
            node.State == DagNodeStateV1.StartAuthorized && node.ChildInstanceId != null);
        var child = await services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference)
            .GetInstanceAsync(run.Nodes[0].ChildInstanceId!, token);
        await child.RequestCancellationAsync(token);
        (await child.GetSnapshotAsync(token)).Status.Should().Be(WorkflowInstanceStatus.Cancelled);

        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        if (loseAck)
        {
            var claim = (await outbox.ClaimAsync(Request() with
            {
                KindSelector = OutboxKindSelector.Including("dag-child-join-v1"), LeaseDuration = TimeSpan.FromSeconds(2)
            }, token)).Should().ContainSingle().Which;
            claim.DispatchAttempt.Should().Be(1);
            claim.Payload.Should().Equal(DagPumpRunFixture.ChildWrite("dag-child-join-v1", run.RunId).Payload);
            (await services.GetRequiredService<IDurableDagChildHandler>().HandleAsync(claim, token)).Should().BeTrue();
            (await outbox.GetStateAsync(claim.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Claimed);
            var joined = await DagPumpRunFixture.ReadAsync(events, run.RunId, token);
            joined.State.Should().Be(DagRunStateV1.Running);
            joined.Nodes[0].FailureCode.Should().Be("CHILD_CANCELLED");
            return new(Environment.ProcessId, run.RunId, run.Nodes[1].ChildInstanceId!.Value, claim.OutboxRecordId.Value);
        }
        await RequestParentCancellationAsync(services, run.RunId, token);
        var history = await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(run.RunId), StreamVersion.Empty, token);
        history.Select(record => record.Kind).Should().Contain(DagRunRecordKindV1.CancellationRequested)
            .And.NotContain(DagRunRecordKindV1.ChildJoined);
        return new(Environment.ProcessId, run.RunId, run.Nodes[1].ChildInstanceId!.Value, null);
    }

    private static async Task<RecoveryObservation> VerifyAfterRestartAsync(ServiceProvider services, RestartCoordinates seed,
        bool loseAck, CancellationToken token)
    {
        var events = services.GetRequiredService<IWorkflowEventStore>();
        var outbox = services.GetRequiredService<IWorkflowOutboxStore>();
        var runId = seed.RunId;
        if (loseAck)
        {
            (await outbox.GetStateAsync(new(seed.OriginalClaimId!.Value), token)).Value.Should().Be(OutboxRecordState.Claimed);
            await RequestParentCancellationAsync(services, runId, token);
        }
        var barrier = await DagPumpRunFixture.ReadAsync(events, runId, token);
        barrier.State.Should().Be(DagRunStateV1.CancellationRequested);
        barrier.Nodes[1].State.Should().Be(DagNodeStateV1.CancellationRequested);
        barrier.Nodes[1].ChildInstanceId!.Value.Should().Be(seed.SiblingChildId);

        // Hold the cancellation notices under a separate claim while the recovery pump handles A's join.
        (await outbox.ClaimAsync(Request() with
        {
            KindSelector = OutboxKindSelector.Including("dag-child-cancel-v1"), LeaseDuration = TimeSpan.FromMinutes(5)
        }, token)).Should().HaveCount(loseAck ? 1 : 2);
        var recorder = new RecordingChildHandler(services.GetRequiredService<IDurableDagChildHandler>());
        var pump = new DurableDagChildPump(outbox, recorder);
        int delivered;
        // Reclaim only when the real lease expires. Each request uses the current clock.
        do
        {
            delivered = await pump.PumpOnceAsync(Request(), token);
            if (recorder.Handled.Count == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, token);
        } while (recorder.Handled.Count == 0);
        var notice = recorder.Handled.Should().ContainSingle().Which;
        notice.Kind.Should().Be("dag-child-join-v1");
        notice.Payload.Should().Equal(DagPumpRunFixture.ChildWrite("dag-child-join-v1", runId).Payload);
        notice.DispatchAttempt.Should().Be(loseAck ? 2 : 1);
        if (loseAck) notice.OutboxRecordId.Value.Should().Be(seed.OriginalClaimId!.Value);

        var history = await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(runId), StreamVersion.Empty, token);
        var joins = history.Where(record => record.Kind == DagRunRecordKindV1.ChildJoined).ToArray();
        var join = joins.Should().ContainSingle().Which;
        var change = join.ChangedNodes.Should().ContainSingle().Which;
        var after = await DagPumpRunFixture.ReadAsync(events, runId, token);
        var dispatch = (await outbox.GetDispatchSnapshotAsync(notice.OutboxRecordId, token)).Value;
        using var assertions = new AssertionScope();
        (join.StreamVersion.Value < barrier.StreamVersion.Value).Should().Be(loseAck);
        change.Ordinal.Should().Be(0);
        change.ChildInstanceId.Should().Be(barrier.Nodes[0].ChildInstanceId);
        change.State.Should().Be(DagNodeStateV1.Failed);
        change.FailureCode.Should().Be("CHILD_CANCELLED");
        after.Nodes[0].State.Should().Be(DagNodeStateV1.Failed);
        after.Nodes[0].FailureCode.Should().Be("CHILD_CANCELLED");
        after.Nodes[1].Should().BeEquivalentTo(barrier.Nodes[1]);
        after.State.Should().Be(DagRunStateV1.CancellationRequested);
        history.Select(record => record.Kind).Should().NotContain(DagRunRecordKindV1.IntegrityFaulted);
        delivered.Should().Be(1);
        dispatch.State.Should().Be(OutboxRecordState.Dispatched, $"dispatch recorded {dispatch.PoisonCode}: {dispatch.PoisonDetail}");
        return new(Environment.ProcessId, joins.Length, after.Nodes[1].ChildInstanceId!.Value, notice.DispatchAttempt, dispatch.State);
    }

    private static async Task RequestParentCancellationAsync(ServiceProvider services, Guid runId, CancellationToken token) =>
        (await services.GetRequiredService<IDurableDagRunBridgeV1>().RequestRunCancellationAsync(runId, token))
            .Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
}
