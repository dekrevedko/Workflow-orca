using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dag;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Hosting.Tests;

// Compiled only inside a disposable copy of the Hosting tests.
public sealed partial class DagSchedulerTests
{
    private static void SaveProbe(object observation) =>
        File.AppendAllText(Environment.GetEnvironmentVariable("ORCA_WORKER_PROBE")!,
            JsonSerializer.Serialize(observation) + Environment.NewLine);

    [Fact]
    public async Task MeasureWorkerIdle()
    {
        const int count = 64;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = timeout.Token;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<GatedChildStep>().End(state => state.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var threads = new ConcurrentDictionary<int, byte>();
        dag.Node(DagNodeId.Create("gate"), child.Reference).MapInput(context =>
        {
            threads.TryAdd(Environment.CurrentManagedThreadId, 0);
            return context.RunInput;
        });
        var plan = dag.Build();
        var gauges = new ConcurrentDictionary<string, int>();
        using var meters = new MeterListener();
        meters.InstrumentPublished = (i,l) =>
        {
            if (i.Meter.Name == "OrcaCore.Dag.Hosting") l.EnableMeasurementEvents(i);
        };
        meters.SetMeasurementEventCallback<int>((i, n, _, _) => gauges[i.Name] = n);
        meters.Start();
        using var host = CreateHost(child, 2, configure: services => services.AddSingleton(new GatedChildStep(release.Task)));
        await host.StartAsync(token);
        try
        {
            void Sample(string phase)
            {
                // Drop short-lived test allocations before comparing the same lifecycle points.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                meters.RecordObservableInstruments();
                using var process = Process.GetCurrentProcess();
                process.Refresh();
                SaveProbe(new { scenario = "idle", phase, count, threads = process.Threads.Count,
                    active = gauges["orca.dag.evaluation.lanes.active"], retired = gauges["orca.dag.evaluation.lanes.retired"],
                    mappedThreads = threads.Count, privateBytes = process.PrivateMemorySize64,
                    workingSetBytes = process.WorkingSet64, virtualBytes = process.VirtualMemorySize64 });
            }
            var bridge = host.Services.GetRequiredService<DurableDagRunBridge>();
            Sample("empty");
            var runs = new List<Guid>();
            for (var i = 0; i < count; i++) runs.Add((await CreateRunAsync(host, plan, i, token)).RunId.Value);
            foreach (var id in runs)
            {
                var snapshot = (await bridge.ReadAsync(id, token))!;
                while (snapshot.Nodes[0].State != DagNodeStateV1.StartAuthorized)
                    snapshot = await bridge.WaitForChangeAsync(id, snapshot.StreamVersion, token);
            }
            Assert.Equal(count, threads.Count);
            Sample("admitted");
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            Sample("idle5s");
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            Sample("idle10s");
            foreach (var id in runs)
                Assert.Equal(DagNodeStateV1.StartAuthorized, (await bridge.ReadAsync(id, token))!.Nodes[0].State);
        }
        finally
        {
            release.TrySetResult();
            await StopHostAsync(host);
        }
    }

    [Fact]
    public async Task MeasureWorkerRapid()
    {
        const int nodes = 24;
        const int rounds = 4; // first is warm-up
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(state => state.Value).Build();
        foreach (var chain in new[]{ false, true })
        {
            var entered = new ConcurrentDictionary<(int Round,int Node), (long Tick, int Thread)>();
            var events = new ConcurrentDictionary<(Guid Run,int Node),long>();
            var joins = new ConcurrentDictionary<(Guid Run,int Node),long>();
            var probe = new DagEventStoreProbe { AfterAppend = batch =>
            {
                if (batch.DagRun is not { } run) return;
                var stamp = Stopwatch.GetTimestamp();
                foreach (var record in run.Records)
                {
                    foreach (var node in record.ChangedNodes)
                    {
                        if (record.Kind == DagRunRecordKindV1.InputAdmitted && node.State == DagNodeStateV1.StartAuthorized)
                            events.TryAdd((record.RunId,node.Ordinal),stamp);
                        if (record.Kind == DagRunRecordKindV1.ChildJoined && node.State == DagNodeStateV1.Succeeded)
                            joins.TryAdd((record.RunId,node.Ordinal),stamp);
                    }
                }
            } };
            using var host = CreateHost(child, nodes, configure: services =>
            {
                var port = services.Last(d => d.ServiceType == typeof(IWorkflowEventStore));
                services.AddSingleton<IWorkflowEventStore>(provider =>
                {
                    probe.Inner = (IWorkflowEventStore)port.ImplementationFactory!(provider);
                    return probe;
                });
                services.Configure<DurableHostedServiceOptions>(opts =>
                {
                    opts.OutboxPumpInterval = TimeSpan.FromMilliseconds(5);
                    opts.ContinuationPumpInterval = TimeSpan.FromMilliseconds(5);
                });
            });
            var graph = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
            DagNodeRef<int>? previous = null;
            for (var i=0; i<nodes; i++)
            {
                var ordinal = i;
                var node = graph.Node(DagNodeId.Create($"n{i}"), child.Reference);
                if (chain && previous is not null) node.DependsOn(previous);
                previous = node.MapInput(context =>
                {
                    var captured = (Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId);
                    Assert.True(entered.TryAdd((context.RunInput,ordinal), captured));
                    return context.RunInput;
                });
            }
            var plan = graph.Build();
            await host.StartAsync(token);
            try
            {
                for (var round = 0; round < rounds; round++)
                {
                    var started = Stopwatch.GetTimestamp();
                    var run = await CreateRunAsync(host, plan, round, token);
                    var id = run.RunId.Value;
                    var snapshot = await host.Services.GetRequiredService<DurableDagRunBridge>().WaitForTerminalAsync(id,token);
                    Assert.Equal(DagRunStateV1.Succeeded,snapshot.State);
                    var admissionMs = new List<double>();
                    var predecessorJoinToAdmissionMs = new List<double>();
                    var nextAdmissionMs = new List<double>();
                    for (var ordinal = 0; ordinal<nodes; ordinal++)
                    {
                        admissionMs.Add(Stopwatch.GetElapsedTime(entered[(round,ordinal)].Tick,events[(id,ordinal)]).TotalMilliseconds);
                        if (ordinal == 0) continue;
                        nextAdmissionMs.Add(Stopwatch.GetElapsedTime(events[(id,ordinal-1)],events[(id,ordinal)]).TotalMilliseconds);
                        if (chain) predecessorJoinToAdmissionMs.Add(Stopwatch.GetElapsedTime(joins[(id,ordinal-1)],events[(id,ordinal)]).TotalMilliseconds);
                    }
                    SaveProbe(new { scenario = chain ? "chain" : "roots", nodes, round, mappedThreads =
                        entered.Where(e => e.Key.Round == round).Select(e => e.Value.Thread).Distinct().Count(),
                        toLastAdmissionMs = Stopwatch.GetElapsedTime(started, events[(id,nodes-1)]).TotalMilliseconds,
                        admissionMs, nextAdmissionMs, predecessorJoinToAdmissionMs });
                }
            }
            finally { await StopHostAsync(host); }
        }
    }
}

public sealed partial class DagSchedulerTests
{
    [Fact]
    public async Task MeasureWorkerBurst()
    {
        const int count = 48;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        for (var round = 0; round < 11; round++)
        {
            var entered = new long[count];
            var admitted = new long[count];
            var threads = new int[count];
            var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var probe = new DagEventStoreProbe { AfterAppend = batch =>
            {
                if (batch.DagRun is not { } run) return;
                foreach (var record in run.Records.Where(r => r.Kind == DagRunRecordKindV1.InputAdmitted))
                    foreach (var node in record.ChangedNodes.Where(n => n.State == DagNodeStateV1.StartAuthorized))
                    {
                        admitted[node.Ordinal] = Stopwatch.GetTimestamp();
                        if (node.Ordinal == count - 1) all.TrySetResult();
                    }
            } };
            using var host = CreateHost(child, count, configure: services =>
            {
                var port = services.Last(d => d.ServiceType == typeof(IWorkflowEventStore));
                services.AddSingleton<IWorkflowEventStore>(provider =>
                {
                    probe.Inner = (IWorkflowEventStore)port.ImplementationFactory!(provider);
                    return probe;
                });
            });
            var graph = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
            for (var i=0; i<count; i++)
            {
                var ordinal = i;
                graph.Node(DagNodeId.Create($"n{i}"), child.Reference).MapInput(context =>
                {
                    entered[ordinal] = Stopwatch.GetTimestamp();
                    threads[ordinal] = Environment.CurrentManagedThreadId;
                    return context.RunInput;
                });
            }
            var plan = graph.Build();
            var id = (await CreateRunAsync(host,plan,round,token)).RunId.Value;
            // Only discovery/dispatch is running. Child-start outbox rows stay committed
            // and unclaimed, so no child join can contend for any admission turn.
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            var pump = host.Services.GetRequiredService<DurableDagReadyPump>().RunDiscoveryAsync(stop.Token);
            try
            {
                await all.Task.WaitAsync(token);
                var snapshot = (await host.Services.GetRequiredService<DurableDagRunBridge>().ReadAsync(id,token))!;
                Assert.Equal(count,snapshot.Nodes.Count(n => n.State == DagNodeStateV1.StartAuthorized));
                var indices = Enumerable.Range(1,count-1).ToArray();
                Assert.All(entered, t => Assert.True(t != 0));
                Assert.All(admitted, t => Assert.True(t != 0));
                SaveProbe(new { scenario = "burst", nodes = count, round, mappedThreads = threads.Distinct().Count(),
                    firstToLastMs = Stopwatch.GetElapsedTime(admitted[0],admitted[^1]).TotalMilliseconds,
                    nextAdmissionMs = indices.Select(i=>Stopwatch.GetElapsedTime(admitted[i-1],admitted[i]).TotalMilliseconds).ToArray(),
                    admissionMs = indices.Select(i=>Stopwatch.GetElapsedTime(entered[i],admitted[i]).TotalMilliseconds).ToArray() });
            }
            finally
            {
                await stop.CancelAsync();
                try { await pump; } catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
            }
        }
    }
}
