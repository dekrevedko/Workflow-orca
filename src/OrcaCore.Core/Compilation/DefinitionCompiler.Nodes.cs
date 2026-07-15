using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static IEnumerable<WorkflowNode<TState>> BuildNodes<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        string path)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var nodeId = $"{path}/{index}";
            yield return node switch
            {
                SelectedInitAuthoringNode<TState> init => new InitNode<TState>(
                    nodeId,
                    init.CreateState,
                    init.RehydrateInput),
                SelectedStepAuthoringNode<TState> step => new BusinessStepNode<TState>(
                    nodeId,
                    step.StepFactory,
                    step.Policies),
                SelectedWaitAuthoringNode<TState> wait => new WaitNode<TState>(
                    nodeId,
                    wait.EventName,
                    wait.CorrelationSelector),
                SelectedDelayAuthoringNode<TState> delay => new DelayNode<TState>(
                    nodeId,
                    delay.Duration),
                SelectedRunChildAuthoringNode<TState> child => new RunChildNode<TState>(
                    nodeId,
                    child.ChildDefinitionId,
                    child.ChildDefinitionVersion,
                    child.FailurePolicy),
                SelectedRunChildrenAuthoringNode<TState> children => new RunChildrenNode<TState>(
                    nodeId,
                    children.ChildDefinitionId,
                    children.ChildDefinitionVersion,
                    children.ItemSnapshotSelector,
                    children.FailurePolicy,
                    children.MaxConcurrency,
                    children.JoinPolicy,
                    children.ResidualPolicy),
                SelectedEndAuthoringNode<TState> end => new EndNode<TState>(
                    nodeId,
                    end.OutcomeName,
                    end.OutcomeSelector),
                SelectedIfAuthoringNode<TState> conditional => new IfNode<TState>(
                    nodeId,
                    conditional.Condition,
                    new SequenceNode<TState>($"{nodeId}/then", BuildNodes(conditional.Then, $"{nodeId}/then")),
                    new SequenceNode<TState>($"{nodeId}/else", BuildNodes(conditional.Else, $"{nodeId}/else"))),
                SelectedWhileAuthoringNode<TState> loop => new WhileNode<TState>(
                    nodeId,
                    loop.Condition,
                    new SequenceNode<TState>($"{nodeId}/body", BuildNodes(loop.Body, $"{nodeId}/body"))),
                SelectedContinueAsNewAuthoringNode<TState> rollover => new ContinueAsNewNode<TState>(
                    nodeId,
                    rollover.StateSelector),
                SelectedStructuredScopeAuthoringNode<TState> => new CompiledScopeNode<TState>(
                    nodeId,
                    $"scope/{nodeId}"),
                SelectedForEachAuthoringNode<TState> => new CompiledScopeNode<TState>(
                    nodeId,
                    $"scope/{nodeId}"),
                _ => throw new InvalidOperationException($"Unsupported authored node '{node.GetType().Name}'.")
            };
        }
    }
}
