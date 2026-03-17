using System.Linq.Expressions;

namespace OrcaCore.Runtime.Querying;

internal static class DefinitionScopedQueryBuilder
{
    public static Expression<Func<TSnapshot, bool>> ForDefinition<TSnapshot>(
        Expression<Func<TSnapshot, bool>> predicate,
        params (string PropertyName, object Value)[] filters)
    {
        var parameter = predicate.Parameters[0];
        Expression body = predicate.Body;

        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var equals = Expression.Equal(
                Expression.Property(parameter, filter.PropertyName),
                Expression.Constant(filter.Value));
            body = Expression.AndAlso(equals, body);
        }

        return Expression.Lambda<Func<TSnapshot, bool>>(body, parameter);
    }
}
