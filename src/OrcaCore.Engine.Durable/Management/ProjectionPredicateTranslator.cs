using System.Linq.Expressions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Management;

internal static class ProjectionPredicateTranslator
{
    internal static WorkflowProjectionQuery Translate(
        WorkflowProjectionQuery query,
        Expression<Func<WorkflowInstanceQueryModel, bool>> predicate)
    {
        return TranslateExpression(query, predicate.Body);
    }

    private static WorkflowProjectionQuery TranslateExpression(
        WorkflowProjectionQuery query,
        Expression expression)
    {
        return expression switch
        {
            BinaryExpression { NodeType: ExpressionType.AndAlso } andAlso =>
                TranslateExpression(TranslateExpression(query, andAlso.Left), andAlso.Right),
            BinaryExpression { NodeType: ExpressionType.Equal } equal =>
                TranslateEquality(query, equal.Left, equal.Right),
            MethodCallExpression methodCall => TranslateEqualsCall(query, methodCall),
            _ => throw new NotSupportedException("Durable management predicates support equality and && only.")
        };
    }

    private static WorkflowProjectionQuery TranslateEqualsCall(
        WorkflowProjectionQuery query,
        MethodCallExpression methodCall)
    {
        if (methodCall.Method.Name == nameof(object.Equals) &&
            methodCall.Object is not null &&
            methodCall.Arguments.Count == 1 &&
            TryGetMemberName(methodCall.Object, out var memberName))
        {
            return ApplyEquality(query, memberName, Evaluate(methodCall.Arguments[0]));
        }

        throw new NotSupportedException("Durable management predicates support member equality and && only.");
    }

    private static WorkflowProjectionQuery TranslateEquality(
        WorkflowProjectionQuery query,
        Expression left,
        Expression right)
    {
        if (TryGetMemberName(left, out var leftMemberName))
        {
            return ApplyEquality(query, leftMemberName, Evaluate(right));
        }

        if (TryGetMemberName(right, out var rightMemberName))
        {
            return ApplyEquality(query, rightMemberName, Evaluate(left));
        }

        throw new NotSupportedException("Durable management predicates must compare query model members to values.");
    }

    private static WorkflowProjectionQuery ApplyEquality(
        WorkflowProjectionQuery query,
        string memberName,
        object? value)
    {
        return memberName switch
        {
            nameof(WorkflowInstanceQueryModel.InstanceId) => query with { InstanceId = (InstanceId?)value },
            nameof(WorkflowInstanceQueryModel.ParentInstanceId) => query with { ParentInstanceId = (InstanceId?)value },
            nameof(WorkflowInstanceQueryModel.RootInstanceId) => query with { RootInstanceId = (InstanceId?)value },
            nameof(WorkflowInstanceQueryModel.DefinitionId) => query with { DefinitionId = (DefinitionId?)value },
            nameof(WorkflowInstanceQueryModel.DefinitionVersion) => query with { DefinitionVersion = (DefinitionVersion?)value },
            nameof(WorkflowInstanceQueryModel.Status) => query with { Status = ToWorkflowStatus(value) },
            _ => throw new NotSupportedException(
                $"Durable management predicate member '{memberName}' is not projection-queryable yet.")
        };
    }

    private static WorkflowStatus? ToWorkflowStatus(object? value)
    {
        return value switch
        {
            null => null,
            WorkflowStatus status => status,
            int status => (WorkflowStatus)status,
            _ => throw new NotSupportedException("Durable management status predicates must compare to WorkflowStatus.")
        };
    }

    private static bool TryGetMemberName(Expression expression, out string memberName)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } converted)
        {
            return TryGetMemberName(converted.Operand, out memberName);
        }

        if (expression is MemberExpression { Expression: ParameterExpression } member)
        {
            memberName = member.Member.Name;
            return true;
        }

        memberName = string.Empty;
        return false;
    }

    private static object? Evaluate(Expression expression)
    {
        var boxed = Expression.Convert(expression, typeof(object));
        return Expression.Lambda<Func<object?>>(boxed).Compile()();
    }
}
