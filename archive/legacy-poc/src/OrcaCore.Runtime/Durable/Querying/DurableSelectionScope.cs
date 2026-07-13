using System.Linq.Expressions;

namespace OrcaCore.Runtime.Durable.Querying;

/// <summary>
/// Admin/management query surface for durable instance snapshots.
/// Queries are evaluated in memory and are not intended as a large-scale remote query abstraction.
/// </summary>
public sealed class DurableSelectionScope
{
    private readonly DurableWorkflowEngine _engine;
    private readonly Func<DurableInstanceSnapshot, bool>? _predicate;

    internal DurableSelectionScope(
        DurableWorkflowEngine engine,
        Expression<Func<DurableInstanceSnapshot, bool>>? predicate = null)
    {
        _engine = engine;
        if (predicate is not null)
        {
            DurableWorkflowQueryValidator.Validate(predicate);
            _predicate = predicate.Compile();
        }
    }

    public async Task<IReadOnlyList<DurableInstanceSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _engine.QuerySnapshotsAsync(cancellationToken);
        if (_predicate is not null)
            snapshots = snapshots.Where(_predicate).ToList();

        return snapshots.ToList().AsReadOnly();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _engine.QuerySnapshotsAsync(cancellationToken);
        if (_predicate is not null)
            return snapshots.Count(_predicate);

        return snapshots.Count();
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default) =>
        _engine.DeleteSelectionAsync(_predicate, cancellationToken);

    public Task PurgeArtifactsAsync(DurableArtifactRetentionPolicy policy, CancellationToken cancellationToken = default) =>
        _engine.PurgeSelectionArtifactsAsync(_predicate, policy, cancellationToken);
}

internal static class WorkflowQueryValidationCore
{
    public static void Validate(Expression expression, ParameterExpression parameter)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                ValidateBinary(binary, parameter);
                return;
            case UnaryExpression unary when unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked:
                Validate(unary.Operand, parameter);
                return;
            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                Validate(unary.Operand, parameter);
                return;
            case MemberExpression member:
                ValidateMember(member, parameter);
                return;
            case ConstantExpression:
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported query expression node '{expression.NodeType}'.");
        }
    }

    private static void ValidateBinary(BinaryExpression binary, ParameterExpression parameter)
    {
        if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse
            or ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual)
        {
            Validate(binary.Left, parameter);
            Validate(binary.Right, parameter);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported query operator '{binary.NodeType}'.");
    }

    private static void ValidateMember(MemberExpression member, ParameterExpression parameter)
    {
        if (IsSnapshotMember(member, parameter))
            return;

        if (IsCapturedConstant(member))
            return;

        throw new InvalidOperationException(
            $"Unsupported query member access '{member.Member.Name}'.");
    }

    private static bool IsSnapshotMember(MemberExpression member, ParameterExpression parameter)
    {
        if (member.Expression == parameter)
            return true;

        if (member.Expression is UnaryExpression unary
            && unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && unary.Operand == parameter)
            return true;

        return false;
    }

    private static bool IsCapturedConstant(MemberExpression member)
    {
        if (member.Expression is ConstantExpression)
            return true;

        if (member.Expression is MemberExpression inner)
            return IsCapturedConstant(inner);

        return false;
    }
}
