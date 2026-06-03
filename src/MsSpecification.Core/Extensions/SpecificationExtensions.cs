using System.Linq.Expressions;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Contracts;
using MsSpecification.Core.Models;

namespace MsSpecification.Core.Extensions;

/// <summary>
/// Extension methods for combining specifications using logical operators.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Creates a new specification by combining the criteria of two specifications with AND.
    /// Includes are merged from both; ordering/pagination come from the left specification; boolean
    /// flags (AsNoTracking, AsSplitQuery, AsStreaming, AsNoTrackingWithIdentityResolution,
    /// IsDistinct, IgnoreQueryFilters) are merged with OR — the combined spec is the stricter
    /// configuration. The returned specification is immutable; modifications to either input
    /// after combining do not affect it.
    /// </summary>
    public static ISpecification<T> And<T>(
        this ISpecification<T> left,
        ISpecification<T> right)
        where T : class
        => new CombinedSpecification<T>(left, right, combineWithAnd: true);

    /// <summary>
    /// Creates a new specification by combining the criteria of two specifications with OR.
    /// Includes are merged from both; ordering/pagination come from the left specification; boolean
    /// flags are merged with OR. The returned specification is immutable; modifications to either
    /// input after combining do not affect it.
    /// </summary>
    public static ISpecification<T> Or<T>(
        this ISpecification<T> left,
        ISpecification<T> right)
        where T : class
        => new CombinedSpecification<T>(left, right, combineWithAnd: false);
}

/// <summary>
/// Internal combined specification created by And/Or extension methods.
/// Merges criteria with the chosen operator; takes ordering/pagination from the left spec;
/// merges include chains from both; merges flags with OR.
/// </summary>
internal sealed class CombinedSpecification<T> : ISpecification<T>
    where T : class
{
    private readonly Lazy<Func<T, bool>> _compiledCriteria;

    public CombinedSpecification(ISpecification<T> left, ISpecification<T> right, bool combineWithAnd)
    {
        if (left.Criteria is null && right.Criteria is null)
        {
            Criteria = null;
        }
        else if (left.Criteria is null)
        {
            Criteria = right.Criteria;
        }
        else if (right.Criteria is null)
        {
            Criteria = left.Criteria;
        }
        else
        {
            var param = left.Criteria.Parameters[0];
            var rightBody = new ParameterReplacerVisitor(right.Criteria.Parameters[0], param)
                .Visit(right.Criteria.Body);

            var body = combineWithAnd
                ? Expression.AndAlso(left.Criteria.Body, rightBody)
                : Expression.OrElse(left.Criteria.Body, rightBody);

            Criteria = Expression.Lambda<Func<T, bool>>(body, param);
        }

        var allIncludes = new List<IncludeChain<T>>(left.Includes.Count + right.Includes.Count);
        allIncludes.AddRange(left.Includes);
        allIncludes.AddRange(right.Includes);
        Includes = allIncludes.AsReadOnly();

        var allStrings = new List<string>(left.IncludeStrings.Count + right.IncludeStrings.Count);
        allStrings.AddRange(left.IncludeStrings);
        allStrings.AddRange(right.IncludeStrings);
        IncludeStrings = allStrings.AsReadOnly();

        OrderBy = left.OrderBy;
        ThenOrderBys = left.ThenOrderBys;
        Skip = left.Skip;
        Take = left.Take;
        AsNoTracking = left.AsNoTracking || right.AsNoTracking;
        AsNoTrackingWithIdentityResolution = left.AsNoTrackingWithIdentityResolution || right.AsNoTrackingWithIdentityResolution;
        AsSplitQuery = left.AsSplitQuery || right.AsSplitQuery;
        AsStreaming = left.AsStreaming || right.AsStreaming;
        IdentitySelector = left.IdentitySelector;
        IsDistinct = left.IsDistinct || right.IsDistinct;
        QueryTag = left.QueryTag ?? right.QueryTag;
        IgnoreQueryFilters = left.IgnoreQueryFilters || right.IgnoreQueryFilters;
        RawSql = left.RawSql;
        RawSqlParameters = left.RawSqlParameters;

        _compiledCriteria = new Lazy<Func<T, bool>>(
            () => Criteria?.Compile() ?? (_ => true),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Expression<Func<T, bool>>? Criteria { get; }
    public IReadOnlyList<IncludeChain<T>> Includes { get; }
    public IReadOnlyList<string> IncludeStrings { get; }
    public OrderByClause<T>? OrderBy { get; }
    public IReadOnlyList<OrderByClause<T>> ThenOrderBys { get; }
    public int? Skip { get; }
    public int? Take { get; }
    public bool AsNoTracking { get; }
    public bool AsNoTrackingWithIdentityResolution { get; }
    public bool AsSplitQuery { get; }
    public bool AsStreaming { get; }
    public Expression<Func<T, T>>? IdentitySelector { get; }
    public bool IsDistinct { get; }
    public string? QueryTag { get; }
    public bool IgnoreQueryFilters { get; }
    public string? RawSql { get; }
    public IReadOnlyList<object> RawSqlParameters { get; }

    public bool IsSatisfiedBy(T entity)
    {
        if (Criteria is null) return true;
        return _compiledCriteria.Value(entity);
    }

}
