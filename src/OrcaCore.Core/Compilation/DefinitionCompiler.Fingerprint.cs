using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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
                case SelectedInitAuthoringNode<TState> init:
                    builder.Append('<').Append(DelegateIdentity(init.CreateState)).Append('>');
                    break;
                case SelectedStepAuthoringNode<TState> step:
                    builder.Append('<')
                        .Append(DelegateIdentity(step.StepFactory)).Append(':')
                        .Append(DescribePolicy(step.Policies)).Append('>');
                    break;
                case SelectedWaitAuthoringNode<TState> wait:
                    builder.Append('<')
                        .Append(wait.EventName).Append(':').Append(wait.Mode).Append(':')
                        .Append(DelegateIdentity(wait.CorrelationSelector)).Append('>');
                    break;
                case SelectedDelayAuthoringNode<TState> delay:
                    builder.Append('<').Append(delay.Duration.Ticks).Append('>');
                    break;
                case SelectedRunChildAuthoringNode<TState> child:
                    builder.Append('<')
                        .Append(child.ChildDefinitionId).Append(':')
                        .Append(child.ChildDefinitionVersion).Append(':')
                        .Append(child.FailurePolicy).Append('>');
                    break;
                case SelectedRunChildrenAuthoringNode<TState> children:
                    builder.Append('<')
                        .Append(children.ChildDefinitionId).Append(':')
                        .Append(children.ChildDefinitionVersion).Append(':')
                        .Append(children.MaxConcurrency).Append(':')
                        .Append(children.FailurePolicy).Append(':')
                        .Append(children.JoinPolicy).Append(':')
                        .Append(children.ResidualPolicy).Append(':')
                        .Append(DelegateIdentity(children.ItemSnapshotSelector)).Append('>');
                    break;
                case SelectedEndAuthoringNode<TState> end:
                    builder.Append('<')
                        .Append(end.OutcomeName ?? DelegateIdentity(end.OutcomeSelector)).Append('>');
                    break;
                case SelectedIfAuthoringNode<TState> conditional:
                    builder.Append('<').Append(DelegateIdentity(conditional.Condition)).Append('>');
                    DescribeSequence(conditional.Then, builder);
                    DescribeSequence(conditional.Else, builder);
                    break;
                case SelectedWhileAuthoringNode<TState> loop:
                    builder.Append('<').Append(DelegateIdentity(loop.Condition)).Append('>');
                    DescribeSequence(loop.Body, builder);
                    break;
                case SelectedContinueAsNewAuthoringNode<TState> rollover:
                    builder.Append('<').Append(DelegateIdentity(rollover.StateSelector)).Append('>');
                    break;
                case SelectedStructuredScopeAuthoringNode<TState> scope:
                    builder.Append('<')
                        .Append(scope.ResultType.AssemblyQualifiedName).Append(':')
                        .Append(DelegateIdentity(scope.Merge)).Append('>');
                    DescribeBranches(scope.Branches, builder);
                    break;
                case SelectedForEachAuthoringNode<TState> forEach:
                    builder.Append('<')
                        .Append(forEach.ItemType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.ItemStateType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.ResultType.AssemblyQualifiedName).Append(':')
                        .Append(forEach.JoinPolicy).Append(':')
                        .Append(forEach.FailurePolicy).Append(':')
                        .Append(forEach.MaxConcurrency).Append(':')
                        .Append(PartitionerIdentity(forEach.Partitioner)).Append(':')
                        .Append(DelegateIdentity(forEach.ItemSelector)).Append(':')
                        .Append(DelegateIdentity(forEach.ItemStateProjector)).Append(':')
                        .Append(DelegateIdentity(forEach.Merge)).Append('>');
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
                .Append(branch.BranchStateType.AssemblyQualifiedName).Append(':')
                .Append(DelegateIdentity(branch.InputProjector)).Append(':');
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
                        .Append(DelegateIdentity(branchStep.StepFactory)).Append(':')
                        .Append(DescribePolicy(branchStep.Policies)).Append('>');
                    break;
                case BranchWaitAuthoringInstruction wait:
                    builder.Append("Wait<")
                        .Append(wait.EventName).Append(':').Append(wait.Mode).Append(':')
                        .Append(DelegateIdentity(wait.CorrelationSelector)).Append('>');
                    break;
                case BranchDelayAuthoringInstruction delay:
                    builder.Append("Delay<").Append(delay.Duration.Ticks).Append('>');
                    break;
                case BranchReturnAuthoringInstruction branchReturn:
                    builder.Append("Return<")
                        .Append(DelegateIdentity(branchReturn.ResultProjector)).Append('>');
                    break;
                case BranchStructuredScopeAuthoringInstruction nested:
                    builder.Append("Scope<")
                        .Append(nested.ScopeKind).Append(':')
                        .Append(nested.ParentStateType.AssemblyQualifiedName).Append(':')
                        .Append(nested.ResultType.AssemblyQualifiedName).Append(':')
                        .Append(DelegateIdentity(nested.Merge)).Append('>');
                    DescribeBranches(nested.Branches, builder);
                    break;
            }

            builder.Append(',');
        }
    }

    private static string DescribeOptions(DefinitionCompilerOptions options)
    {
        return string.Join(
            ':',
            options.MaxInternalInstructionsPerQuantum,
            options.MaxScopeDepth,
            options.MaxActiveFibers,
            options.MaxSerializedResultBytes,
            options.MaxSerializedEnvelopeBytes);
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

    private static string DelegateIdentity(Delegate? value)
    {
        return DelegateIdentity(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static string DelegateIdentity(
        Delegate? value,
        HashSet<object> visited)
    {
        if (value is null)
        {
            return "none";
        }

        var method = value.Method;
        var builder = new StringBuilder()
            .Append(method.DeclaringType?.AssemblyQualifiedName)
            .Append('|').Append(method.Name)
            .Append('|').Append(method.ReturnType.AssemblyQualifiedName);
        foreach (var parameter in method.GetParameters())
        {
            builder.Append('|').Append(parameter.ParameterType.AssemblyQualifiedName);
        }

        if (value.Target is { } target)
        {
            builder.Append("|target:").Append(target.GetType().AssemblyQualifiedName);
            if (!target.GetType().IsValueType && !visited.Add(target))
            {
                return builder.Append("|cycle").ToString();
            }

            if (target is IWorkflowPlanFingerprintSource source)
            {
                builder.Append("|source:").Append(source.GetWorkflowPlanFingerprint());
            }
            else if (IsCompilerGenerated(target.GetType()))
            {
                AppendCapturedFields(builder, target, visited);
            }
        }

        return builder.ToString();
    }

    private static void AppendCapturedFields(
        StringBuilder builder,
        object target,
        HashSet<object> visited)
    {
        foreach (var field in target.GetType()
                     .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderBy(field => field.Name, StringComparer.Ordinal))
        {
            builder.Append("|capture:").Append(field.Name).Append('=');
            AppendConfigurationValue(builder, field.GetValue(target), visited);
        }
    }

    private static void AppendConfigurationValue(
        StringBuilder builder,
        object? value,
        HashSet<object> visited)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case string text:
                builder.Append("string:").Append(text.Length).Append(':').Append(text);
                return;
            case char character:
                builder.Append("char:").Append((int)character);
                return;
            case bool boolean:
                builder.Append("bool:").Append(boolean ? '1' : '0');
                return;
            case Enum enumeration:
                builder.Append("enum:").Append(enumeration.GetType().AssemblyQualifiedName)
                    .Append(':').Append(Convert.ToString(enumeration, CultureInfo.InvariantCulture));
                return;
            case TimeSpan duration:
                builder.Append("timespan:").Append(duration.Ticks);
                return;
            case DateTime dateTime:
                builder.Append("datetime:").Append(dateTime.Ticks).Append(':').Append(dateTime.Kind);
                return;
            case DateTimeOffset dateTimeOffset:
                builder.Append("datetimeoffset:").Append(dateTimeOffset.Ticks)
                    .Append(':').Append(dateTimeOffset.Offset.Ticks);
                return;
            case Guid guid:
                builder.Append("guid:").Append(guid.ToString("N"));
                return;
            case Type type:
                builder.Append("type:").Append(type.AssemblyQualifiedName);
                return;
            case IWorkflowPlanFingerprintSource source:
                builder.Append("source:").Append(source.GetWorkflowPlanFingerprint());
                return;
            case Delegate operation:
                builder.Append("delegate:").Append(DelegateIdentity(operation, visited));
                return;
            case IFormattable formatted when value.GetType().IsValueType:
                builder.Append(value.GetType().AssemblyQualifiedName).Append(':')
                    .Append(formatted.ToString(null, CultureInfo.InvariantCulture));
                return;
        }

        if (!value.GetType().IsValueType && !visited.Add(value))
        {
            builder.Append("cycle:").Append(value.GetType().AssemblyQualifiedName);
            return;
        }

        // Only traverse arrays automatically. Arbitrary IEnumerable implementations can be
        // lazy, consuming, or blocking; complex configuration objects can opt in explicitly
        // through IWorkflowPlanFingerprintSource.
        if (value is Array sequence)
        {
            builder.Append("sequence:").Append(value.GetType().AssemblyQualifiedName).Append('[');
            var count = Math.Min(sequence.Length, 64);
            for (var index = 0; index < count; index++)
            {
                AppendConfigurationValue(builder, sequence.GetValue(index), visited);
                builder.Append(',');
            }

            if (sequence.Length > count)
            {
                builder.Append("truncated");
            }

            builder.Append(']');
            return;
        }

        if (IsCompilerGenerated(value.GetType()))
        {
            builder.Append("closure:").Append(value.GetType().AssemblyQualifiedName);
            foreach (var field in value.GetType()
                         .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                builder.Append('|').Append(field.Name).Append('=');
                AppendConfigurationValue(builder, field.GetValue(value), visited);
            }

            return;
        }

        builder.Append("opaque:").Append(value.GetType().AssemblyQualifiedName);
    }

    private static bool IsCompilerGenerated(Type type)
    {
        return type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
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
                Delegate operation => DelegateIdentity(operation),
                IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            });
        }

        return builder.ToString();
    }
}
