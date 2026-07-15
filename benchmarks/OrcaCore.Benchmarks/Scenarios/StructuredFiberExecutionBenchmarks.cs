using System.Text.Json;
using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class StructuredFiberExecutionBenchmarks
{
    private readonly InstanceId instanceId = new(
        Guid.Parse("00000000-0000-0000-0000-000000090001"));
    private readonly JsonCodec codec = new();
    private CompiledWorkflowPlan plan = null!;
    private CompiledScopePlan outerScope = null!;
    private CompiledScopePlan nestedScope = null!;
    private StructuredExecutionState initialState = null!;
    private StructuredExecutionState mergeReadyState = null!;
    private ScopeId mergeScopeId;
    private IReadOnlyList<MaterializedBranchResult> mergeResults = null!;
    private DurableExecutionEnvelopeV2 envelope = null!;
    private IReadOnlyList<FiberId> schedulerFibers = null!;

    [GlobalSetup]
    public void Setup()
    {
        plan = BuildDefinition().CompiledPlan;
        outerScope = plan.Scopes[0];
        nestedScope = plan.Scopes[1];
        initialState = StructuredExecutionState.Create(instanceId, 0, plan.Instructions[0].Id);
        schedulerFibers = Enumerable.Range(0, 64)
            .Select(index => new FiberId($"benchmark-fiber-{index:D2}"))
            .ToArray();

        var started = ScopeReducer.StartScope(
            initialState,
            initialState.RootFiberId,
            outerScope);
        mergeScopeId = started.ScopeId;
        var results = outerScope.Branches
            .Select(branch => new MaterializedBranchResult(
                branch.Id,
                codec.Serialize(branch.Ordinal + 1, typeof(int), branch.Result.ResultSchemaIdentity)))
            .ToArray();
        var outcomes = started.ChildFiberIds
            .Select((fiberId, index) => ChildTerminalOutcome.Succeeded(fiberId, results[index].Result.Payload))
            .ToArray();
        mergeReadyState = ScopeReducer.RecordChildTerminals(
            started.State,
            started.ScopeId,
            outcomes).State;
        mergeResults = results;
        envelope = CreateEnvelope();
    }

    [Benchmark]
    public object CompileStructuredPlan()
    {
        return BuildDefinition();
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public FiberId? ScheduleQuantumRoundRobin()
    {
        var scheduler = FiberScheduler.Create(schedulerFibers);
        for (var index = 0; index < 1024; index++)
        {
            var selected = FiberScheduler.SelectNext(scheduler)!.Value;
            scheduler = FiberScheduler.CompleteTurn(scheduler, selected, requeueSelected: true);
        }

        return scheduler.NextFiberId;
    }

    [Benchmark]
    public object AdvanceNestedScope()
    {
        var outer = ScopeReducer.StartScope(initialState, initialState.RootFiberId, outerScope);
        return ScopeReducer.StartScope(outer.State, outer.ChildFiberIds[0], nestedScope);
    }

    [Benchmark]
    public object ReplayCommittedMerge()
    {
        var replacement = ScopeMergeAdapter.Execute(
            outerScope,
            new BenchmarkState(0),
            mergeResults,
            codec);
        return ScopeReducer.CompleteMerge(
            plan,
            ScopeReducer.BeginMerge(mergeReadyState, mergeScopeId),
            mergeScopeId,
            replacement.Payload);
    }

    [Benchmark]
    public int SerializeEnvelopeAndMeasureSize()
    {
        return envelope.Serialize().Length;
    }

    private static WorkflowDefinition<BenchmarkState> BuildDefinition()
    {
        return Workflow.Ephemeral<BenchmarkState>(
                new DefinitionId(Guid.Parse("00000000-0000-0000-0000-000000090002")),
                DefinitionVersion.Initial)
            .Init<int>(value => new BenchmarkState(value))
            .Parallel<int>(
                branches => branches
                    .Branch<BranchState>("nested", parent => new BranchState(parent.Value.Value), branch => branch
                        .Parallel<int>(
                            nested => nested
                                .Branch<BranchState>("a", parent => parent.Value, child =>
                                    child.Return(state => state.Value.Value))
                                .Branch<BranchState>("b", parent => parent.Value, child =>
                                    child.Return(state => state.Value.Value)),
                            (parent, results) => new BranchState(results.Sum(result => result.Value)))
                        .Return(state => state.Value.Value))
                    .Branch<BranchState>("direct", parent => new BranchState(parent.Value.Value + 1), branch =>
                        branch.Return(state => state.Value.Value)),
                (parent, results) => new BenchmarkState(results.Sum(result => result.Value)))
            .End()
            .Build();
    }

    private DurableExecutionEnvelopeV2 CreateEnvelope()
    {
        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = instanceId,
            ContinueAsNewGeneration = 0,
            RootFiberId = schedulerFibers[0].Value,
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = plan.DefinitionId,
                DefinitionVersion = plan.DefinitionVersion,
                CompilerFormatVersion = plan.FormatVersion,
                PlanFingerprint = plan.Fingerprint
            },
            StateContentType = "application/json",
            StatePayload = JsonSerializer.SerializeToUtf8Bytes(new BenchmarkState(42)),
            Fibers = schedulerFibers.Select((fiberId, index) => new DurableFiberState
            {
                FiberId = fiberId.Value,
                InstructionId = plan.Instructions[index % plan.Instructions.Count].Id.Value,
                Phase = DurableFiberPhase.Runnable,
                LoopIteration = index / 8,
                NextScopeEntrySequence = index % 4,
                LocalStatePayload = JsonSerializer.SerializeToUtf8Bytes(new BranchState(index))
            }).ToArray(),
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = schedulerFibers.Select(fiberId => fiberId.Value).ToArray(),
                NextFiberId = schedulerFibers[0].Value
            }
        };
    }

    private sealed class JsonCodec : IStructuredValueCodec
    {
        public StructuredSerializedValue Serialize(object? value, Type declaredType, string schemaIdentity)
        {
            return new StructuredSerializedValue(
                declaredType,
                schemaIdentity,
                JsonSerializer.SerializeToUtf8Bytes(value, declaredType));
        }

        public object? Deserialize(StructuredSerializedValue value)
        {
            return JsonSerializer.Deserialize(value.Payload, value.DeclaredType);
        }
    }

    private sealed record BenchmarkState(int Value);

    private sealed record BranchState(int Value);
}
