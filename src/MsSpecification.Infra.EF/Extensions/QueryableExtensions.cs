using MsSpecification.Core.Contracts;

namespace MsSpecification.Infra.EF.Extensions;

/// <summary>
/// Extension methods for applying specifications directly to IQueryable sources.
/// Useful when composing specifications with additional ad-hoc query logic.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies all clauses from a specification to the source queryable.
    /// </summary>
    public static IQueryable<T> WithSpecification<T>(
        this IQueryable<T> source,
        ISpecification<T> specification)
        where T : class
        => SpecificationEvaluator.GetQuery(source, specification);

    /// <summary>
    /// Applies only the criteria (WHERE clause) from a specification.
    /// Useful for count/exists queries where includes and ordering are unnecessary.
    /// </summary>
    public static IQueryable<T> WithCriteria<T>(
        this IQueryable<T> source,
        ISpecification<T> specification)
        where T : class
        => SpecificationEvaluator.GetQuery(source, specification, evaluateCriteriaOnly: true);

    /// <summary>
    /// Applies all clauses from a projection specification and returns IQueryable&lt;TResult&gt;.
    /// </summary>
    public static IQueryable<TResult> WithSpecification<T, TResult>(
        this IQueryable<T> source,
        ISpecification<T, TResult> specification)
        where T : class
        => SpecificationEvaluator.GetQuery(source, specification);
}
