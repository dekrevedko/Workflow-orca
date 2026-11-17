using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
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
        using var host = CreateHost(child, 1);
        await host.StartAsync(token);
        try
        {
            var bridge = host.Services.GetRequiredService<DurableDagRunBridge>();
            var slow = (await CreateRunAsync(host, compiled, 7, token)).RunId.Value;
            await entered.Task.WaitAsync(token);
            var fast = (await CreateRunAsync(host, compiled, 42, token)).RunId.Value;
            var finished = await bridge.WaitForTerminalAsync(fast, token);
            finished.State.Should().Be(DagRunStateV1.Succeeded);
            finished.Nodes[0].Output!.Bytes.Should().Equal("42"u8.ToArray());
            (await bridge.RequestRunCancellationAsync(slow, token)).Outcome.Should().Be(DurableDagCancelOutcomeV1.Requested);
            var cancelled = await bridge.WaitForTerminalAsync(slow, token);
            cancelled.State.Should().Be(DagRunStateV1.Cancelled);
            cancelled.Nodes[0].Input.Should().BeNull();
            Volatile.Read(ref slowCalls).Should().Be(1);
            release.Set();
        }
        finally
        {
            release.Set();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StopAsync(stop.Token);
        }
    }

    [Fact]
    public async Task ShippedHost_TwoReadyRootsWithOneSlotMapAndAdmitInAuthoredOrder()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        var firstMapped = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var plan = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        plan.Node(DagNodeId.Create("first"), child.Reference).MapInput(input =>
        {
            firstMapped.TrySetResult(0);
            return input.RunInput;
        });
        plan.Node(DagNodeId.Create("second"), child.Reference).MapInput(input =>
        {
            firstMapped.TrySetResult(1);
            return input.RunInput;
        });

        using var host = CreateHost(child, 1);
        await host.StartAsync(token);
        try
        {
            var id = (await CreateRunAsync(host, plan.Build(), 42, token)).RunId.Value;
            (await firstMapped.Task.WaitAsync(token)).Should().Be(0, "the first free slot belongs to the lowest ready ordinal");
            var final = await host.Services.GetRequiredService<DurableDagRunBridge>().WaitForTerminalAsync(id, token);
            final.State.Should().Be(DagRunStateV1.Succeeded);
            (await ReadAdmissionOrderAsync(host, id, token)).Should().Equal(0, 1);
        }
        finally { await StopHostAsync(host); }
    }

    [Fact]
    public async Task ShippedHost_DiscardsHigherReceiptWhenAChildJoinMakesLowerOrdinalReady()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var releaseChild = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var higherEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHigher = new ManualResetEventSlim();
        var mapped = new ConcurrentQueue<int>();
        var higherCalls = 0;
        var initialChild = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).Then<GatedChildStep>()
            .End(state => state.Value).Build();
        var fastChild = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        var plan = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = plan.Node(DagNodeId.Create("first"), initialChild.Reference).MapInput(input =>
        {
            mapped.Enqueue(0);
            return input.RunInput;
        });
        plan.Node(DagNodeId.Create("dependent"), fastChild.Reference).DependsOn(first).MapInput(input =>
        {
            mapped.Enqueue(1);
            return input.RunInput;
        });
        plan.Node(DagNodeId.Create("independent"), fastChild.Reference).MapInput(input =>
        {
            mapped.Enqueue(2);
            var attempt = Interlocked.Increment(ref higherCalls);
            if (attempt == 1)
            {
                higherEntered.TrySetResult();
                releaseHigher.Wait(token);
            }
            return input.RunInput + attempt;
        });

        using var host = CreateHost(initialChild, 2, fastChild, services =>
            services.AddSingleton(new GatedChildStep(releaseChild.Task)));
        await host.StartAsync(token);
        try
        {
            var id = (await CreateRunAsync(host, plan.Build(), 40, token)).RunId.Value;
            await higherEntered.Task.WaitAsync(token);
            var bridge = host.Services.GetRequiredService<DurableDagRunBridge>();
            var admitted = (await bridge.ReadAsync(id, token))!;
            admitted.Nodes[1].State.Should().Be(DagNodeStateV1.Pending);
            releaseChild.SetResult();
            var joined = admitted;
            while (joined.Nodes[1].State != DagNodeStateV1.Ready)
                joined = await bridge.WaitForChangeAsync(id, joined.StreamVersion, token);
            joined.Nodes[0].State.Should().Be(DagNodeStateV1.Succeeded);
            releaseHigher.Set();

            var final = await bridge.WaitForTerminalAsync(id, token);
            final.State.Should().Be(DagRunStateV1.Succeeded);
            mapped.Should().Equal(0, 2, 1, 2);
            final.Nodes[2].Input!.Bytes.Should().Equal("42"u8.ToArray(), "the earlier receipt would have committed 41");
            (await ReadAdmissionOrderAsync(host, id, token)).Should().Equal(0, 1, 2);
        }
        finally
        {
            releaseChild.TrySetResult();
            releaseHigher.Set();
            await StopHostAsync(host);
        }
    }

    private sealed class GatedChildStep(Task release) : IStep<int>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            await release.WaitAsync(cancellationToken);
            return new StepResult.Completed();
        }
    }

    private static IHost CreateHost(DurableWorkflowDefinition<int, int> child, int limit,
        DurableWorkflowDefinition<int, int>? otherChild = null, Action<IServiceCollection>? configure = null) =>
        Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddOrcaCoreInMemoryDurableProvider();
            var engine = services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = ResourceGovernancePartitionId.Create("dag-scheduler-tests"), Pools = []
                }
            }).AddWorkflow(child);
            if (otherChild is not null) engine.AddWorkflow(otherChild);
            services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = limit });
            configure?.Invoke(services);
        }).Build();

    private static async ValueTask<DagRunHandle> CreateRunAsync(IHost host, WorkflowDagPlan<int> plan,
        int input, CancellationToken token)
    {
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var result = await definition.StartOrGetAsync(input, StartIdempotencyKey.Create(Guid.NewGuid().ToString("D")), token);
        result.Should().BeOfType<DagStartResult.Accepted>().Which.WasExisting.Should().BeFalse();
        return result.GetHandleOrThrow();
    }

    private static async Task<int[]> ReadAdmissionOrderAsync(IHost host, Guid id, CancellationToken token)
    {
        var records = await host.Services.GetRequiredService<IWorkflowEventStore>()
            .LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(id), StreamVersion.Empty, token);
        return records.Where(record => record.Kind == DagRunRecordKindV1.InputAdmitted)
            .Select(record => record.ChangedNodes.Single().Ordinal).ToArray();
    }

    private static async Task StopHostAsync(IHost host)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StopAsync(stop.Token);
    }
}
