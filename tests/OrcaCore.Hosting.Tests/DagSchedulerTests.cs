using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DagSchedulerTests
{
    [Fact]
    public async Task ShippedHost_SlowMapperDoesNotBlockAnotherRunOrHoldTurn()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var slowCalls = 0;
        var plan = OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        plan.Node(DagNodeId.Create("first"), child.Reference).MapInput(input =>
        {
            if (input.RunInput == 7)
            {
                Interlocked.Increment(ref slowCalls);
                entered.TrySetResult();
                release.Wait(token);
            }
            return input.RunInput;
        });
        var compiled = plan.Build();
        using var host = Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddOrcaCoreInMemoryDurableProvider();
            services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = ResourceGovernancePartitionId.Create("dag-scheduler-tests"), Pools = []
                }
            }).AddWorkflow(child);
            services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 1 });
        }).Build();
        await host.StartAsync(token);
        try
        {
            var bridge = host.Services.GetRequiredService<DurableDagRunBridge>();
            // Only the test reaches the unshipped Task 8.6 entry point. The
            // scheduler, provider, mappers and all runtime dispatch are real.
            var assembly = typeof(DagHostOptions).Assembly;
            var coordinatorType = assembly.GetType("OrcaCore.Dag.Hosting.DagCoordinator", throwOnError: true)!;
            var create = coordinatorType.GetMethod("CreateOrReopenAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(int));
            var coordinator = host.Services.GetRequiredService(coordinatorType);
            var slow = Guid.NewGuid();
            var fast = Guid.NewGuid();
            (await Create(slow, 7)).Outcome.Should().Be(DurableDagCreateOutcomeV1.Created);
            await entered.Task.WaitAsync(token);
            (await Create(fast, 42)).Outcome.Should().Be(DurableDagCreateOutcomeV1.Created);
            var finished = await bridge.WaitForTerminalAsync(fast, token);
            finished.State.Should().Be(DagRunStateV1.Succeeded);
            finished.Nodes[0].Output!.Bytes.Should().Equal("42"u8.ToArray());
            (await bridge.RequestRunCancellationAsync(slow, token)).Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
            var cancelled = await bridge.WaitForTerminalAsync(slow, token);
            cancelled.State.Should().Be(DagRunStateV1.Cancelled);
            cancelled.Nodes[0].Input.Should().BeNull();
            Volatile.Read(ref slowCalls).Should().Be(1);
            release.Set();

            ValueTask<DurableDagCreateResultV1> Create(Guid id, int input) =>
                (ValueTask<DurableDagCreateResultV1>)create.Invoke(coordinator, [compiled, id, id.ToString("D"), input, token])!;
        }
        finally
        {
            release.Set();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StopAsync(stop.Token);
        }
    }
}
