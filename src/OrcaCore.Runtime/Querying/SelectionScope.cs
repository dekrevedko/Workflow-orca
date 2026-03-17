using System.Linq.Expressions;

namespace OrcaCore.Runtime.Querying;

public sealed class SelectionScope
{
    private readonly InMemoryInstanceStore _store;
    private readonly Func<WorkflowInstanceSnapshot, bool>? _predicate;

    internal SelectionScope(InMemoryInstanceStore store, Expression<Func<WorkflowInstanceSnapshot, bool>>? predicate = null)
    {
        _store = store;
        if (predicate is not null)
        {
            WorkflowQueryValidator.Validate(predicate);
            _predicate = predicate.Compile();
        }
    }

    public IReadOnlyList<WorkflowInstanceSnapshot> List()
    {
        var snapshots = _store.GetAllSnapshots();
        if (_predicate is not null)
            snapshots = snapshots.Where(_predicate).ToList();
        return snapshots.ToList().AsReadOnly();
    }

    public int Count()
    {
        var snapshots = _store.GetAllSnapshots();
        if (_predicate is not null)
            return snapshots.Count(_predicate);
        return snapshots.Count();
    }
}

internal static class WorkflowQueryValidator
{
    public static void Validate(Expression<Func<WorkflowInstanceSnapshot, bool>> predicate)
    {
        WorkflowQueryValidationCore.Validate(predicate.Body, predicate.Parameters[0]);
    }
}
