using System.Linq.Expressions;

namespace Sizzle.Infrastructure.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> WhereIdIn<T>(this IQueryable<T> query, IEnumerable<Guid> ids, Expression<Func<T, Guid>> idSelector)
    {
        Expression predicate = Expression.Constant(false);

        foreach (var id in ids.Distinct())
        {
            Expression<Func<Guid>> value = () => id;
            predicate = Expression.OrElse(predicate, Expression.Equal(idSelector.Body, value.Body));
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, idSelector.Parameters));
    }
}
