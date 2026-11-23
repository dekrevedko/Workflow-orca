using System.Globalization;
using System.Reflection;
using System.Text;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    // A field is either null or a UTF-16-length-prefixed value encoded as hex code
    // units. This preserves even unpaired surrogates through the later UTF-8 hash,
    // and caller-supplied names cannot become fingerprint structure.
    private static StringBuilder AppendField(this StringBuilder builder, object? value)
    {
        if (value is null) return builder.Append("null:");
        var text = value is IFormattable formatted
            ? formatted.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString()!;
        builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var codeUnit in text)
            builder.Append(((int)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
        return builder;
    }

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
                        .AppendField(step.StepType?.AssemblyQualifiedName).Append(':')
                        .Append(DescribePolicy(step.Policies)).Append('>');
                    break;
                case SelectedWaitAuthoringNode<TState> wait:
                    builder.Append('<')
                        .AppendField(wait.EventContract.EventName.Value).Append(':')
                        .AppendField(wait.EventContract.Version.Value).Append(':')
                        .AppendField(wait.Mode).Append(':')
                        .AppendField(wait.Timeout?.Ticks).Append(':')
                        .Append("opaque>");
                    break;
                case SelectedPublishAuthoringNode<TState> publish:
                    builder.Append('<')
                        .AppendField(publish.EventContract.EventName.Value).Append(':')
                        .AppendField(publish.EventContract.Version.Value).Append(':')
                        .AppendField(publish.PayloadType?.AssemblyQualifiedName).Append(':')
                        .Append("opaque>");
                    break;
                case SelectedDelayAuthoringNode<TState> delay:
                    builder.Append('<').AppendField(delay.Duration.Ticks).Append('>');
                    break;
                case SelectedEndAuthoringNode<TState> end:
                    builder.Append('<');
                    if (end.OutcomeName is { } name)
                        builder.Append("named:").AppendField(name);
                    else
                        builder.Append(end.OutcomeSelector is null ? "absent" : "selector:opaque");
                    builder.Append(':').AppendField(end.OutputType?.AssemblyQualifiedName).Append('>');
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
                        .AppendField(scope.ResultType.AssemblyQualifiedName).Append('>');
                    DescribeBranches(scope.Branches, builder);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    builder.Append('<')
                        .AppendField(forEach.ItemType.AssemblyQualifiedName).Append(':')
                        .AppendField(forEach.ItemStateType.AssemblyQualifiedName).Append(':')
                        .AppendField(forEach.ResultType.AssemblyQualifiedName).Append(':')
                        .AppendField(forEach.JoinPolicy).Append(':')
                        .AppendField(forEach.FailurePolicy).Append(':')
                        .AppendField(forEach.MaxItems).Append(':')
                        .AppendField(forEach.MaxConcurrency).Append(':')
                        .AppendField(PartitionerIdentity(forEach.Partitioner)).Append('>');
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
                .AppendField(branch.BranchId).Append(':')
                .AppendField(branch.BranchStateType.AssemblyQualifiedName).Append(':');
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
                        .AppendField(branchStep.StepType?.AssemblyQualifiedName).Append(':')
                        .Append(DescribePolicy(branchStep.Policies)).Append('>');
                    break;
                case BranchWaitAuthoringInstruction wait:
                    builder.Append("Wait<")
                        .AppendField(wait.EventContract.EventName.Value).Append(':')
                        .AppendField(wait.EventContract.Version.Value).Append(':')
                        .AppendField(wait.Mode).Append(':')
                        .AppendField(wait.Timeout?.Ticks).Append('>');
                    break;
                case BranchPublishAuthoringInstruction publish:
                    builder.Append("Publish<")
                        .AppendField(publish.EventContract.EventName.Value).Append(':')
                        .AppendField(publish.EventContract.Version.Value).Append(':')
                        .AppendField(publish.PayloadType?.AssemblyQualifiedName).Append(':')
                        .Append("opaque>");
                    break;
                case BranchDelayAuthoringInstruction delay:
                    builder.Append("Delay<").AppendField(delay.Duration.Ticks).Append('>');
                    break;
                case BranchReturnAuthoringInstruction:
                    builder.Append("Return<opaque>");
                    break;
                case BranchStructuredScopeAuthoringInstruction nested:
                    builder.Append("Scope<")
                        .AppendField(nested.ScopeKind).Append(':')
                        .AppendField(nested.ParentStateType.AssemblyQualifiedName).Append(':')
                        .AppendField(nested.ResultType.AssemblyQualifiedName).Append('>');
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
                builder.AppendField(requirement.Pool.Value)
                    .Append('=')
                    .AppendField(requirement.Units)
                    .Append(',');
            }
        }

        builder.Append('>');
    }

    private static string DescribePolicy(WorkflowPolicySet policy)
    {
        return new StringBuilder()
            .AppendField(policy.Retry?.MaxAttempts)
            .AppendField(policy.Retry?.Backoff.Ticks)
            .AppendField(policy.Timeout?.Duration.Ticks)
            .AppendField(policy.Cancellation)
            .AppendField(policy.PoolKey).ToString();
    }

    private static string PartitionerIdentity(object partitioner)
    {
        if (partitioner is global::OrcaCore.Core.Definitions.IWorkflowPartitionerIdentity typed)
            return typed.FingerprintIdentity;
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("The ForEach partitioner is missing its closed typed identity.");
        return LegacyPartitionerIdentity(partitioner);
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT partitioner identity only.")]
    private static string LegacyPartitionerIdentity(object partitioner)
    {
        var type = partitioner.GetType();
        var builder = new StringBuilder().AppendField(type.AssemblyQualifiedName);
        foreach (var property in type
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var value = property.GetValue(partitioner);
            builder.Append('|').AppendField(property.Name).Append('=').AppendField(value?.GetType().AssemblyQualifiedName);
            builder.AppendField(value switch
            {
                null => null,
                Delegate => "opaque",
                IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            });
        }

        return builder.ToString();
    }
}
