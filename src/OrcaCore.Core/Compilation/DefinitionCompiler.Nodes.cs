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
                init.InputType,
                init.CreateState),
                SelectedStepAuthoringNode<TState> step => new BusinessStepNode<TState>(
                    nodeId,
                    step.StepFactory ?? (() => throw new InvalidOperationException(
                        $"Named step '{step.StepType?.FullName}' requires compiled host resolution.")),
                    step.StepType,
                    step.Policies),
                SelectedWaitAuthoringNode<TState> wait => new WaitNode<TState>(
                    nodeId,
                    wait.EventContract,
                    wait.CorrelationSelector),
                SelectedPublishAuthoringNode<TState> publish => new PublishNode<TState>(
                    nodeId,
                    publish.EventContract,
                    publish.CorrelationSelector,
                    publish.PayloadType,
                    publish.PayloadSelector),
                SelectedDelayAuthoringNode<TState> delay => new DelayNode<TState>(
                    nodeId,
                    delay.Duration),
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
                SelectedResourceLeaseAuthoringNode<TState> lease => new SequenceNode<TState>(
                    $"{nodeId}/lease",
                    BuildNodes(lease.Body, $"{nodeId}/lease")),
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
