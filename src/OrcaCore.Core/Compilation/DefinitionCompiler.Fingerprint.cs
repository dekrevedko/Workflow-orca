using System.Globalization;
using System.Reflection;
using System.Text;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static string DescribeSequence<TState>(IReadOnlyList<SelectedAuthoringNode<TState>> nodes)
    {
        var builder = new StringBuilder();
        DescribeSequence(nodes, builder);
        return builder.ToString();
    }

    private static void DescribeSequence<TState>(
        IReadOnlyList<SelectedAuthoringNode<TState>> nodes,
        StringBuilder builder)
    {
        builder.Append('[');
        foreach (var node in nodes)
        {
            builder.Append(node.Kind);
            switch (node)
            {
                case SelectedInitAuthoringNode<TState>:
                    builder.Append("<opaque>");
                    break;
                case SelectedStepAuthoringNode<TState> step:
                    builder.Append('<')
                        .Append(step.StepType?.AssemblyQualifiedName ?? "inline").Append(':')
                        .Append(DescribePolicy(step.Policies)).Append('>');
                    break;
                case SelectedWaitAuthoringNode<TState> wait:
                    builder.Append('<')
                        .Append(wait.EventName).Append(':').Append(wait.Mode).Append(':')
                        .Append(wait.Timeout?.Ticks).Append(':')
                        .Append("opaque>");
                    break;
                case SelectedDelayAuthoringNode<TState> delay:
                    builder.Append('<').Append(delay.Duration.Ticks).Append('>');
                    break;
                case SelectedEndAuthoringNode<TState> end:
                    builder.Append('<')
                        .Append(end.OutcomeName ?? (end.OutcomeSelector is null ? "unnamed" : "dynamic")).Append(':')
                        .Append(end.OutputType?.AssemblyQualifiedName).Append('>');
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    builder.Append("<opaque>");
                    DescribeSequence(conditional.Then, builder);
                    DescribeSequence(conditional.Else, builder);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    builder.Append("<opaque>");
                    DescribeSequence(loop.Body, builder);
                    break;
                case SelectedContinueAsNewAuthoringNode<TState>:
                    builder.Append("<opaque>");
                    break;
                case SelectedResourceLeaseAuthoringNode<TState> lease:
                    DescribeResourceLease(lease.StaticRequest, lease.RequestSelector, builder);
                    DescribeSequence(lease.Body, builder);
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    builder.Append('<')
                        .Append(scope.ResultType.AssemblyQualifiedName).Append('>');
                    DescribeBranches(scope.Branches, builder);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    builder.Append('<')
                        .Append(forEach.ItemType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.ItemStateType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.ResultType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.JoinPolicy).Append(':')
                        .Append(forEach.FailurePolicy).Append(':')
                        .Append(forEach.MaxItems).Append(':')
                        .Append(forEach.MaxConcurrency).Append(':')
                        .Append(PartitionerIdentity(forEach.Partitioner)).Append('>');
                    DescribeBranchInstructions(forEach.Body, builder);
                    break;
            }

            builder.Append(';');
        }

        builder.Append(']');
    }

    private static void DescribeBranches(
        IReadOnlyList<StructuredBranchAuthoring> branches,
        StringBuilder builder)
    {
        foreach (var branch in branches)
        {
            builder.Append('{')
                .Append(branch.BranchId).Append(':')
                .Append(branch.BranchStateType.AssemblyQualifiedName).Append(':');
            DescribeBranchInstructions(branch.Instructions, builder);
            builder.Append('}');
        }
    }

    private static void DescribeBranchInstructions(
        IReadOnlyList<BranchAuthoringInstruction> instructions,
        StringBuilder builder)
    {
        foreach (var instruction in instructions)
        {
            switch (instruction)
            {
                case BranchStepAuthoringInstruction branchStep:
                    builder.Append("Step<")
                        .Append(branchStep.StepType?.AssemblyQualifiedName ?? "inline").Append(':')
                        .Append(DescribePolicy(branchStep.Policies)).Append('>');
                    break;
                case BranchWaitAuthoringInstruction wait:
                    builder.Append("Wait<")
                        .Append(wait.EventName).Append(':').Append(wait.Mode).Append(':')
                        .Append(wait.Timeout?.Ticks).Append('>');
                    break;
                case BranchDelayAuthoringInstruction delay:
                    builder.Append("Delay<").Append(delay.Duration.Ticks).Append('>');
                    break;
                case BranchReturnAuthoringInstruction:
                    builder.Append("Return<opaque>");
                    break;
                case BranchStructuredScopeAuthoringInstruction nested:
                    builder.Append("Scope<")
                        .Append(nested.ScopeKind).Append(':')
                        .Append(nested.ParentStateType.AssemblyQualifiedName).Append(':')
                        .Append(nested.ResultType.AssemblyQualifiedName).Append('>');
                    DescribeBranches(nested.Branches, builder);
                    break;
                case BranchIfAuthoringInstruction conditional:
                    builder.Append("If<opaque:then[");
                    DescribeBranchInstructions(conditional.Then, builder);
                    builder.Append("]:else[");
                    DescribeBranchInstructions(conditional.Else, builder);
                    builder.Append("]>");
                    break;
                case BranchResourceLeaseAuthoringInstruction lease:
                    DescribeResourceLease(lease.StaticRequest, lease.RequestSelector, builder);
                    builder.Append("body[");
                    DescribeBranchInstructions(lease.Body, builder);
                    builder.Append(']');
                    break;
            }

            builder.Append(',');
        }
    }

    private static void DescribeResourceLease(
        global::OrcaCore.ResourceLeaseRequest? staticRequest,
        Delegate? requestSelector,
        StringBuilder builder)
    {
        builder.Append('<');
        if (staticRequest is null)
        {
            builder.Append(requestSelector is null ? "invalid" : "selector:opaque");
        }
        else
        {
            builder.Append("static:");
            foreach (var requirement in staticRequest.Requirements)
            {
                builder.Append(requirement.Pool.Value.Length.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(requirement.Pool.Value)
                    .Append('=')
                    .Append(requirement.Units.ToString(CultureInfo.InvariantCulture))
                    .Append(',');
            }
        }

        builder.Append('>');
    }

    private static string DescribePolicy(WorkflowPolicySet policy)
    {
        return string.Join(
            ':',
            policy.Retry?.MaxAttempts,
            policy.Retry?.Backoff.Ticks,
            policy.Timeout?.Duration.Ticks,
            policy.Cancellation,
            policy.PoolKey);
    }

    private static string PartitionerIdentity(object partitioner)
    {
        var type = partitioner.GetType();
        var builder = new StringBuilder(type.AssemblyQualifiedName);
        foreach (var property in type
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var value = property.GetValue(partitioner);
            builder.Append('|').Append(property.Name).Append('=');
            builder.Append(value switch
            {
                null => "null",
                Delegate => "opaque",
                IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            });
        }

        return builder.ToString();
    }
}
