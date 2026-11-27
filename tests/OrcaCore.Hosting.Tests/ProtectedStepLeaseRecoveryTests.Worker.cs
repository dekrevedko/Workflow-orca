using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class ProtectedStepLeaseRecoveryTests
{
    private sealed record Admission(Guid Instance, string Lease, string Operation, int Attempt, int Pid);
    private sealed class ProtectedStep(Func<Admission, Task> run) : IStep<int>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            // This submitter deliberately ignores its workflow cancellation token.
            Assert.NotNull(context.ResourceLease);
            var admission = new Admission(context.Execution.WorkflowInstanceId.Value, context.ResourceLease.ProtectionToken.Value,
                context.Execution.OperationId.Value, context.Execution.AttemptNumber, Environment.ProcessId);
            await run(admission);
            context.ReplaceState(99);
            return new StepResult.Completed();
        }
    }

    private static DurableWorkflowDefinition<int, int> Definition() =>
        Workflow.Durable<int>(DefinitionId.Parse("d5634778-ee93-46e7-b90a-c7ac984b317b"), DefinitionVersion.Initial)
            .Init<int>(value => value)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(Pool)), lease => lease.Then<ProtectedStep>())
            .End(state => state.Value).Build();

    private static async Task<ServiceProvider> HostAsync(string provider, string connection, ProtectedStep step,
        DurableWorkflowDefinition<int, int> definition, CancellationToken token)
    {
        var services = new ServiceCollection();
        Type options;
        if (provider == "PostgreSql")
        {
            services.AddOrcaCorePostgreSqlDurableProvider(new(connection, "public"));
            options = typeof(PostgreSqlDurableProviderOptions);
        }
        else
        {
            Assert.Equal("SqlServer", provider);
            services.AddOrcaCoreSqlServerDurableProvider(new(connection, "protected_step"));
            options = typeof(SqlServerDurableProviderOptions);
        }
        services.AddSingleton(step);
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = Partition,
                Pools = [DurableResourcePoolDefinition.Create(Pool, 1, TimeSpan.FromHours(1))] }
        }).AddWorkflow(definition);
        var host = services.BuildServiceProvider();
        // Facade drives only. No other executor can submit for the stopped owner after the worker exits.
        await host.GetServices<IHostedService>().Single(service => service.GetType().Assembly == options.Assembly).StartAsync(token);
        return host;
    }

    public static bool IsOwnerWorker => Environment.GetEnvironmentVariable("ORCA_PROTECTED_STEP_DIRECTORY") is not null;

    [Fact(Explicit = true, SkipUnless = nameof(IsOwnerWorker), Skip = "Invoked by the protected-step overlap controller.")]
    public async Task OwnerWorker()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(4));
        var token = deadline.Token;
        var directory = Environment.GetEnvironmentVariable("ORCA_PROTECTED_STEP_DIRECTORY")!;
        var provider = Environment.GetEnvironmentVariable("ORCA_PROTECTED_STEP_PROVIDER")!;
        var connection = Environment.GetEnvironmentVariable("ORCA_PROTECTED_STEP_CONNECTION")!;
        var resource = new JobResource(provider, connection);
        Admission? entered = null;
        var step = new ProtectedStep(async admission =>
        {
            Assert.Null(entered);
            entered = admission;
            var path = Path.Combine(directory, "admitted.json");
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(admission), token);
            File.Move(path + ".tmp", path);
            while (!File.Exists(Path.Combine(directory, "submit")))
                await Task.Delay(25, token);
            await resource.SubmitAsync(admission, token);
        });
        var definition = Definition();
        // A bounded drive can stop before user admission. A fresh facade re-drives the same key.
        do
        {
            await using var host = await HostAsync(provider, connection, step, definition, token);
            var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference);
            var started = (await handle.StartOrGetAsync(1, StartIdempotencyKey.Create("owner"), token)).GetHandleOrThrow();
            if (entered is not null) Assert.Equal(entered.Instance, started.InstanceId.Value);
        } while (entered is null);
        await File.WriteAllTextAsync(Path.Combine(directory, "returned.json"), JsonSerializer.Serialize(entered), token);
    }

    private sealed class OwnerProcess : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string directory;
        private readonly Task<string> output;
        private readonly Task<string> errors;
        internal OwnerProcess(string provider, string connection, string directory)
        {
            this.directory = directory;
            var info = new ProcessStartInfo("dotnet")
            {
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            var type = typeof(ProtectedStepLeaseRecoveryTests);
            foreach (var argument in new[] { type.Assembly.Location, "-explicit", "only", "-method", $"{type.FullName}.{nameof(OwnerWorker)}" })
                info.ArgumentList.Add(argument);
            info.Environment["ORCA_PROTECTED_STEP_DIRECTORY"] = directory;
            info.Environment["ORCA_PROTECTED_STEP_PROVIDER"] = provider;
            info.Environment["ORCA_PROTECTED_STEP_CONNECTION"] = connection;
            process = Process.Start(info)!;
            output = process.StandardOutput.ReadToEndAsync();
            errors = process.StandardError.ReadToEndAsync();
        }

        internal async Task<Admission> AtGateAsync(CancellationToken token)
        {
            var path = Path.Combine(directory, "admitted.json");
            while (!File.Exists(path))
            {
                if (process.HasExited) throw new InvalidOperationException($"Worker exited before gate: {await output} {await errors}");
                await Task.Delay(25, token);
            }
            var admission = JsonSerializer.Deserialize<Admission>(await File.ReadAllTextAsync(path, token))!;
            Assert.Equal(process.Id, admission.Pid);
            Assert.NotEqual(Environment.ProcessId, process.Id);
            return admission;
        }

        internal async Task ResumeAndJoinAsync(Admission admission, CancellationToken token)
        {
            Assert.False(process.HasExited);
            await File.WriteAllTextAsync(Path.Combine(directory, "submit"), "", token);
            await process.WaitForExitAsync(token);
            Assert.True(process.ExitCode == 0, $"{await output} {await errors}");
            Assert.Equal(admission, JsonSerializer.Deserialize<Admission>(
                await File.ReadAllTextAsync(Path.Combine(directory, "returned.json"), token)));
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            process.Dispose();
        }
    }
}
