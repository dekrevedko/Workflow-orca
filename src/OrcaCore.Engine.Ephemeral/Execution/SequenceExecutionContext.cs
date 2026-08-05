using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed record SequenceExecutionContext<TState, TInput>
{
    internal SequenceExecutionContext(
        SequenceNode<TState> sequence,
        InterpreterRunState<TState> runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(resumeEvent);

        Sequence = sequence;
        RunState = runState;
        Input = input;
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        BranchId = branchId;
        ResumeEvent = resumeEvent;
        AfterSequence = afterSequence;
    }

    internal SequenceNode<TState> Sequence { get; init; }

    internal InterpreterRunState<TState> RunState { get; init; }

    internal TInput Input { get; init; }

    internal InstanceId InstanceId { get; init; }

    internal DefinitionId DefinitionId { get; init; }

    internal DefinitionVersion DefinitionVersion { get; init; }

    internal BranchId? BranchId { get; init; }

    /// <summary>
    /// The ForEach work item this sequence is executing under, if any. Propagates to nested
    /// sequences (If/While/Parallel inside a ForEach body) via <see cref="CreateNested"/>; a nested
    /// ForEach overrides it with its own item.
    /// </summary>
    internal ForEachItemContext? ForEachItem { get; init; }

    internal ResumeEventSlot ResumeEvent { get; init; }

    internal Func<CancellationToken, Task>? AfterSequence { get; init; }

    internal bool DeferFailures { get; init; }

    internal SequenceExecutionContext<TState, TInput> CreateNested(
        SequenceNode<TState> sequence,
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence)
    {
        return this with
        {
            Sequence = sequence,
            BranchId = branchId,
            ResumeEvent = resumeEvent,
            AfterSequence = afterSequence
        };
    }
}
