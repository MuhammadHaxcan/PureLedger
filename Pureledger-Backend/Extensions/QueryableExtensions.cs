using System.Linq.Expressions;

namespace PureLedger.Extensions
{
    /// <summary>
    /// Extension methods for IQueryable to simplify common query patterns
    /// </summary>
    public static class QueryableExtensions
    {
        /// <summary>
        /// Conditionally applies a Where clause if the condition is true
        /// </summary>
        public static IQueryable<T> WhereIf<T>(
            this IQueryable<T> query,
            bool condition,
            Expression<Func<T, bool>> predicate)
        {
            return condition ? query.Where(predicate) : query;
        }

        /// <summary>
        /// Applies pagination to a query
        /// </summary>
        public static IQueryable<T> ApplyPagination<T>(
            this IQueryable<T> query,
            int page,
            int pageSize)
        {
            page = Math.Max(0, page);
            pageSize = Math.Clamp(pageSize, 1, 500);
            return query.Skip(page * pageSize).Take(pageSize);
        }

        /// <summary>
        /// Conditionally applies a Where clause with a nullable value
        /// </summary>
        public static IQueryable<T> WhereIfHasValue<T, TValue>(
            this IQueryable<T> query,
            TValue? value,
            Expression<Func<T, bool>> predicate) where TValue : struct
        {
            return value.HasValue ? query.Where(predicate) : query;
        }
    }
}
