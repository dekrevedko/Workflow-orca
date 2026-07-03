using System.Linq.Expressions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// T1-13 / IOQ-6 resolution / MG-002: validates that a <c>Where(...)</c> predicate over
/// <see cref="WorkflowInstanceSnapshot"/> only references the allow-listed, query-safe metadata
/// fields (<see cref="WorkflowInstanceSnapshot.Status"/>, <see cref="WorkflowInstanceSnapshot.DefinitionId"/>,
/// <see cref="WorkflowInstanceSnapshot.DefinitionVersion"/>, <see cref="WorkflowInstanceSnapshot.CreatedAt"/>,
/// <see cref="WorkflowInstanceSnapshot.UpdatedAt"/>) combined with comparison operators and
/// <c>&amp;&amp;</c>/<c>||</c>. Anything else (method calls, arbitrary member access, captured
/// delegates invoked as calls, indexers, ...) is rejected with a clear exception BEFORE the
/// expression is ever compiled — "validate the shape once, then compile and evaluate the
/// compiled delegate" (the simpler of the two options the open question allowed), rather than a
/// general-purpose LINQ provider that translates into a separate structured query model.
/// </summary>
internal static class SnapshotPredicateValidator
{
    private static readonly HashSet<string> AllowedMembers =
    [
        nameof(WorkflowInstanceSnapshot.InstanceId),
        nameof(WorkflowInstanceSnapshot.DefinitionId),
        nameof(WorkflowInstanceSnapshot.DefinitionVersion),
        nameof(WorkflowInstanceSnapshot.Status),
        nameof(WorkflowInstanceSnapshot.CreatedAt),
        nameof(WorkflowInstanceSnapshot.UpdatedAt),
    ];

    /// <summary>Validates <paramref name="predicate"/>'s shape and returns it compiled, ready to evaluate against snapshots.</summary>
    public static Func<WorkflowInstanceSnapshot, bool> ValidateAndCompile(Expression<Func<WorkflowInstanceSnapshot, bool>> predicate)
    {
        Validate(predicate.Body, predicate.Parameters[0]);
        return predicate.Compile();
    }

    private static void Validate(Expression node, ParameterExpression snapshotParameter)
    {
        switch (node)
        {
            case BinaryExpression binary when IsSupportedBinary(binary.NodeType):
                Validate(binary.Left, snapshotParameter);
                Validate(binary.Right, snapshotParameter);
                return;

            case UnaryExpression { NodeType: ExpressionType.Not } unary:
                Validate(unary.Operand, snapshotParameter);
                return;

            case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary:
                Validate(unary.Operand, snapshotParameter);
                return;

            case MemberExpression { Expression: ParameterExpression parameter } member
                when ReferenceEquals(parameter, snapshotParameter):
                if (!AllowedMembers.Contains(member.Member.Name))
                {
                    throw Reject($"Field '{member.Member.Name}' is not part of the query-safe metadata model (MG-002).");
                }

                return;

            case ConstantExpression:
                return;

            // A captured local/closure value shows up as a MemberExpression over a compiler-
            // generated closure instance rather than the snapshot parameter - allowed, since it
            // is a plain data value baked in at Where(...)-call time, not an external call.
            case MemberExpression { Expression: ConstantExpression or MemberExpression or null }:
                return;

            default:
                throw Reject(
                    $"Expression node '{node.NodeType}' is not a supported query-safe construct (MG-002). " +
                    "Where(...) accepts equality/comparison over InstanceId, DefinitionId, DefinitionVersion, " +
                    "Status, CreatedAt, UpdatedAt combined with &&, ||, and !. Arbitrary delegates, method " +
                    "calls, and external calls are rejected.");
        }
    }

    private static bool IsSupportedBinary(ExpressionType nodeType) =>
        nodeType is ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.AndAlso or ExpressionType.OrElse;

    private static WorkflowDefinitionException Reject(string message) => new(message);
}
