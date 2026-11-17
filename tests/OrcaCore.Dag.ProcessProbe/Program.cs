using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;
using OrcaCore.TestSupport;

namespace OrcaCore.Dag.ProcessProbe;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var phase = args[0];
        var provider = args[1];
        var schema = args[2];
        var statePath = args[3];
        var connection = Environment.GetEnvironmentVariable("ORCA_DAG_PROBE_CONNECTION")
            ?? throw new InvalidOperationException("A test connection is required.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        RestartInput.DropOnRead = phase == "incompatible";
        var child = Workflow.Durable<int>(DefinitionId.Parse("1b6a91fa-b922-4bae-9d36-6ff54f5b6e02"), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<Gate>().End(value => value.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<RestartInput>(DefinitionId.Parse("76ad7561-c26b-4cab-ac27-18fbc1e092fb"), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("pending"), child.Reference).MapInput(value => value.RunInput.Value);
        var plan = dag.Build();
        using var host = Host.CreateDefaultBuilder().ConfigureLogging(log => log.ClearProviders()).ConfigureServices(services =>
        {
            if (provider == "postgres") services.AddOrcaCorePostgreSqlDurableProvider(new(connection, schema));
            else services.AddOrcaCoreSqlServerDurableProvider(new(connection, schema));
            services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("dag-restart-probe"), Pools = [] }
            }).AddWorkflow(child);
            services.AddSingleton<Gate>();
            services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 1 });
        }).Build();
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var events = host.Services.GetRequiredService<IWorkflowEventStore>();
        if (phase == "create")
        {
            await host.StartAsync(token);
            var run = (await definition.StartOrGetAsync(new() { Value = 17 }, StartIdempotencyKey.Create("restart"), token)).GetHandleOrThrow();
            while ((await run.GetSnapshotAsync(token)).Nodes[0].Status != DagNodeStatus.Running) await Task.Delay(25, token);
            await DagFacadeWire.FacadeTransitionAsync(events, run.RunId.Value, true, token);
            (await run.GetSnapshotAsync(token)).Status.Should().Be(DagRunStatus.Running);
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new RestartState(run.RunId.ToString(), Environment.ProcessId)), token);
            await host.StopAsync(token);
        }
        else
        {
            var state = JsonSerializer.Deserialize<RestartState>(await File.ReadAllTextAsync(statePath, token))!;
            Environment.ProcessId.Should().NotBe(state.CreatorPid);
            var id = DagRunId.Parse(state.RunId);
            if (phase == "incompatible")
            {
                Func<Task> reopen = () => definition.GetRunAsync(id, token).AsTask();
                await reopen.Should().ThrowAsync<DagRunInputContractMismatchException>();
                (await DagFacadeWire.FacadeReadAsync(events, id.Value, token)).State.Should().Be(OrcaCore.Abstractions.Durable.Dag.DagRunStateV1.IntegrityFault);
            }
            else
            {
                var run = await definition.GetRunAsync(id, token);
                var expected = phase == "inspect-cancel" ? DagRunStatus.CancellationRequested : DagRunStatus.Running;
                var snapshot = await run.GetSnapshotAsync(token);
                snapshot.Status.Should().Be(expected); snapshot.CompletedAt.Should().BeNull();
                Func<Task> wait = () => run.WaitForTerminalAsync(token).AsTask();
                await wait.Should().ThrowAsync<DagRunFaultedException>();
                if (phase == "cancel")
                {
                    Func<Task> reject = () => run.RequestCancellationAsync(token).AsTask();
                    await reject.Should().ThrowAsync<DagRunFaultedException>();
                    await DagFacadeWire.FacadeTransitionAsync(events, id.Value, false, token);
                    (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.Requested);
                    await DagFacadeWire.FacadeTransitionAsync(events, id.Value, true, token);
                }
                else (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.AlreadyRequested);
            }
        }
        Console.WriteLine($"DAG_RESTART_PASS phase={phase} pid={Environment.ProcessId}");
    }

    public sealed record RestartState(string RunId, int CreatorPid);
    public sealed class RestartInput
    {
        public static bool DropOnRead;
        private int value;
        public int Value { get => value; set { if (!DropOnRead) this.value = value; } }
    }
    private sealed class Gate : IStep<int>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken token)
        { await Task.Delay(Timeout.Infinite, token); return new StepResult.Completed(); }
    }
}
