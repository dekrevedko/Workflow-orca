using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private readonly WorkflowDefinition<TState> definition;
    private readonly CompiledWorkflowPlan plan;
    private readonly DurableStructuredValueCodec codec;

    internal DurableFiberDriverExecutor(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        this.definition = definition;
        plan = definition.CompiledPlan;
        codec = new DurableStructuredValueCodec(plan.SerializerRegistry);
    }

    private async Task<InitializationResult> InitializeAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        var ownedObligations = context.FiberEnvelope?.OwnedObligations.ToList() ?? [];
        if (context.FiberEnvelope is not { } persisted)
        {
            return new InitializationResult(
                StructuredExecutionState.Create(
                    context.InstanceId,
                    context.Aggregate.ContinueAsNewGeneration,
                    plan.Instructions[0].Id),
                default!,
                ownedObligations,
                null);
        }

        var validation = DurableFiberEnvelopeValidator.Validate(persisted, plan, context.InstanceId);
        if (!validation.IsValid)
        {
            var parked = await ParkAsync(
                context,
                validation.Code == "SFE-BIND-001"
                    ? DurableParkReason.RuntimeStateVersion
                    : DurableParkReason.VersionBinding,
                $"{validation.Code}: {validation.Diagnostic}",
                cancellationToken).ConfigureAwait(false);
            return new InitializationResult(null, default!, ownedObligations, parked);
        }

        var state = context.Serializer.Deserialize<TState>(new SerializedPayload(
            persisted.StateContentType,
            persisted.StatePayload));
        var execution = ReconcilePendingResumes(
            DurableFiberEnvelopeMapper.FromEnvelope(persisted),
            ownedObligations,
            context.Aggregate.WaitState.PendingResumes,
            context.Aggregate.ChildState);
        return new InitializationResult(execution, state, ownedObligations, null);
    }

    private async Task<SuspensionRecoveryResult> RecoverSuspensionsAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        int commands,
        Stopwatch elapsed,
        CancellationToken cancellationToken)
    {
        execution = RecoverCommittedSuspensions(
            execution,
            ownedObligations,
            context.Aggregate.WaitState,
            context.Aggregate.TimerState,
            plan,
            out var recoveredSuspension);
        if (!recoveredSuspension)
        {
            return new SuspensionRecoveryResult(execution, currentVersion, commands, null);
        }

        var recovered = await context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                "owned-suspension-recovery",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken).ConfigureAwait(false);
        if (recovered.Outcome != DurableCommandOutcome.Committed)
        {
            return new SuspensionRecoveryResult(
                execution,
                currentVersion,
                commands,
                Conflict(recovered));
        }

        currentVersion = recovered.StreamVersion;
        commands++;
        var terminal = BudgetReached(context, commands, elapsed)
            ? new DurableSegmentResult(
                DurableSegmentOutcome.BudgetExhausted,
                CommittedProgress: true)
            : null;
        return new SuspensionRecoveryResult(execution, currentVersion, commands, terminal);
    }

    private Task<DurableCommandResult> CommitScopeStartAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations,
        CompiledInstruction instruction,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        return context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{instruction.Path}:scope-start",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken);
    }

    private sealed record InitializationResult(
        StructuredExecutionState? Execution,
        TState State,
        List<DurableOwnedObligationState> OwnedObligations,
        DurableSegmentResult? Terminal);

    private sealed record SuspensionRecoveryResult(
        StructuredExecutionState Execution,
        StreamVersion StreamVersion,
        int Commands,
        DurableSegmentResult? Terminal);
}
