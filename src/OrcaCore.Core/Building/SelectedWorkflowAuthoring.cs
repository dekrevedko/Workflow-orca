using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

internal sealed class SelectedWorkflowAuthoring<TState>
{
    internal SelectedWorkflowAuthoring(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        WorkflowExecutionMode mode)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Mode = mode;
    }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    internal WorkflowExecutionMode Mode { get; }

    internal DefinitionCompilerOptions CompilerOptions { get; set; } = new();

    internal IWorkflowTypeSerializerRegistry TypeSerializerRegistry { get; set; } =
        DefaultWorkflowTypeSerializerRegistry.Instance;

    internal List<SelectedAuthoringNode<TState>> RootNodes { get; } = [];
}

internal abstract record SelectedAuthoringNode<TState>
{
    internal abstract string Kind { get; }
}

internal sealed record SelectedInitAuthoringNode<TState>(
    Func<object?, TState> CreateState,
    Func<SerializedPayload, IWorkflowPayloadSerializer, object?> RehydrateInput)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "Init";
}

internal sealed record SelectedStepAuthoringNode<TState>(
    Func<IStep<TState>> StepFactory,
    WorkflowPolicySet Policies)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "Step";
}

internal sealed record SelectedWaitAuthoringNode<TState>(
    string EventName,
    Func<TState, CorrelationId> CorrelationSelector,
    WaitMode Mode)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "Wait";
}

internal sealed record SelectedDelayAuthoringNode<TState>(TimeSpan Duration)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "Delay";
}

internal sealed record SelectedEndAuthoringNode<TState>(
    string? OutcomeName,
    Func<TState, string?>? OutcomeSelector)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "End";
}

internal sealed record SelectedIfAuthoringNode<TState>(
    Func<TState, bool> Condition,
    IReadOnlyList<SelectedAuthoringNode<TState>> Then,
    IReadOnlyList<SelectedAuthoringNode<TState>> Else)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "If";
}

internal sealed record SelectedWhileAuthoringNode<TState>(
    Func<TState, bool> Condition,
    IReadOnlyList<SelectedAuthoringNode<TState>> Body)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "While";
}

internal sealed record SelectedContinueAsNewAuthoringNode<TState>(Func<TState, TState> StateSelector)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "ContinueAsNew";
}

internal sealed record SelectedRunChildAuthoringNode<TState>(
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    RunChildFailurePolicy FailurePolicy)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "RunChild";
}

internal sealed record SelectedRunChildrenAuthoringNode<TState>(
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    Func<TState, IReadOnlyList<string>> ItemSnapshotSelector,
    int? MaxConcurrency,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "RunChildren";
}

internal sealed record SelectedStructuredScopeAuthoringNode<TState>(
    string ScopeKind,
    Type ResultType,
    IReadOnlyList<StructuredBranchAuthoring> Branches,
    Delegate Merge)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => ScopeKind;
}

internal sealed record SelectedForEachAuthoringNode<TState>(
    Type ItemType,
    Type ItemStateType,
    Type ResultType,
    Delegate ItemSelector,
    object Partitioner,
    Delegate ItemStateProjector,
    IReadOnlyList<BranchAuthoringInstruction> Body,
    ForEachJoinPolicy JoinPolicy,
    ForEachFailurePolicy FailurePolicy,
    int? MaxConcurrency,
    Delegate? Merge)
    : SelectedAuthoringNode<TState>
{
    internal override string Kind => "ForEach";
}
