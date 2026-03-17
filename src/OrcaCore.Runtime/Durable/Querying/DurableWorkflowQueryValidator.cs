using System.Linq.Expressions;

namespace OrcaCore.Runtime.Durable.Querying;

internal static class DurableWorkflowQueryValidator
{
    public static void Validate(Expression<Func<DurableInstanceSnapshot, bool>> predicate)
    {
        WorkflowQueryValidationCore.Validate(predicate.Body, predicate.Parameters[0]);
    }
}
