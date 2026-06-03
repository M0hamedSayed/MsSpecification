using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Contracts;
using MsSpecification.Core.Models;

namespace MsSpecification.Infra.EF;

/// <summary>
/// Translates a specification into an EF Core IQueryable pipeline.
/// Applies all specification clauses in the correct order for optimal query generation.
///
/// Clause application order (ISpecification&lt;T&gt;):
///   1. IgnoreQueryFilters  2. AsNoTracking / AsNoTrackingWithIdentityResolution / AsStreaming
///   3. TagWith             4. Criteria (WHERE)
///   5. Includes            6. AsSplitQuery
///   7. Distinct            8. Ordering
///   9. Pagination (Skip/Take)  10. Selector (projection T → T)
///
/// For ISpecification&lt;T, TResult&gt; with GroupByApplier: criteria+includes applied first,
/// then GroupByApplier(query) short-circuits the rest.
///
/// For ISpecification&lt;T, TResult&gt; with a Selector: DISTINCT is deferred so it applies to the
/// projected result (SELECT DISTINCT &lt;projected columns&gt;) rather than to the full entity.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>
    /// Applies all specification clauses to the source queryable and returns the resulting query.
    /// </summary>
    public static IQueryable<T> GetQuery<T>(
        IQueryable<T> source,
        ISpecification<T> specification,
        bool evaluateCriteriaOnly = false,
        bool deferDistinct = false)
        where T : class
    {
        if (specification.IgnoreQueryFilters)
            source = source.IgnoreQueryFilters();

        if (specification.AsNoTrackingWithIdentityResolution)
            source = source.AsNoTrackingWithIdentityResolution();
        else if (specification.AsNoTracking || specification.AsStreaming)
            source = source.AsNoTracking();

        if (specification.QueryTag is not null)
            source = source.TagWith(specification.QueryTag);

        if (specification.Criteria is not null)
            source = source.Where(specification.Criteria);

        if (evaluateCriteriaOnly)
            return source;

        source = ApplyIncludes(source, specification);

        if (specification.AsSplitQuery)
            source = source.AsSplitQuery();

        // When a projection follows (ISpecification<T,TResult> with a Selector), DISTINCT is
        // deferred so it applies to the projected columns rather than to the full entity.
        if (specification.IsDistinct && !deferDistinct)
            source = source.Distinct();

        source = ApplyOrdering(source, specification);

        if (specification.Skip.HasValue)
            source = source.Skip(specification.Skip.Value);

        if (specification.Take.HasValue)
            source = source.Take(specification.Take.Value);

        if (specification.IdentitySelector is not null)
            source = source.Select(specification.IdentitySelector);

        return source;
    }

    /// <summary>
    /// Applies all specification clauses and projects the result to TResult via the typed selector
    /// or the group-by applier delegate (when <see cref="ISpecification{T,TResult}.GroupByApplier"/> is set).
    /// </summary>
    public static IQueryable<TResult> GetQuery<T, TResult>(
        IQueryable<T> source,
        ISpecification<T, TResult> specification)
        where T : class
    {
        // Defer DISTINCT only when a Selector projection follows, so it dedupes the projected
        // result. The GroupBy path keeps the default behaviour (its rows are already key-distinct).
        var query = GetQuery(source, (ISpecification<T>)specification,
            deferDistinct: specification.Selector is not null);

        if (specification.GroupByApplier is not null)
            return specification.GroupByApplier(query);

        if (specification.Selector is null)
            throw new InvalidOperationException(
                $"Specification {specification.GetType().Name} must define either a Selector or a GroupBy when using ISpecification<T, TResult>.");

        var projected = query.Select(specification.Selector);
        return specification.IsDistinct ? projected.Distinct() : projected;
    }

    private static IQueryable<T> ApplyIncludes<T>(
        IQueryable<T> source,
        ISpecification<T> specification)
        where T : class
    {
        foreach (var chain in specification.Includes)
        {
            source = ApplyIncludeChain(source, chain);
        }

        foreach (var includeString in specification.IncludeStrings)
        {
            source = source.Include(includeString);
        }

        return source;
    }

    /// <summary>
    /// Routes include chains to either fast string-based Include (no filtered steps)
    /// or the typed fluent API via FilteredIncludeApplier (when any step has a filter).
    /// </summary>
    private static IQueryable<T> ApplyIncludeChain<T>(
        IQueryable<T> source,
        IncludeChain<T> chain)
        where T : class
    {
        var steps = chain.Steps;
        if (steps.IsEmpty) return source;

        bool hasFilteredStep = false;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].IsFiltered) { hasFilteredStep = true; break; }
        }

        if (hasFilteredStep)
            return FilteredIncludeApplier.Apply(source, chain);

        if (steps.Length == 1)
            return source.Include(steps[0].PropertyName);

        return source.Include(BuildNavigationPath(steps));
    }

    private static string BuildNavigationPath(ReadOnlySpan<IncludeStep> steps)
    {
        if (steps.Length == 1)
            return steps[0].PropertyName;

        // Calculate total length for the navigation path string
        var totalLength = steps.Length - 1; // dots between segments
        for (int i = 0; i < steps.Length; i++)
            totalLength += steps[i].PropertyName.Length;

        // Use stackalloc for typical short chains (avoid heap allocation)
        Span<char> buffer = totalLength <= 256 ? stackalloc char[totalLength] : new char[totalLength];

        var pos = 0;
        for (int i = 0; i < steps.Length; i++)
        {
            if (i > 0) buffer[pos++] = '.';
            var name = steps[i].PropertyName.AsSpan();
            name.CopyTo(buffer[pos..]);
            pos += name.Length;
        }

        return new string(buffer);
    }

    private static IQueryable<T> ApplyOrdering<T>(
        IQueryable<T> source,
        ISpecification<T> specification)
        where T : class
    {
        if (specification.OrderBy is null)
            return source;

        var primary = specification.OrderBy.Value;
        IOrderedQueryable<T> ordered = primary.Descending
            ? source.OrderByDescending(primary.KeySelector)
            : source.OrderBy(primary.KeySelector);

        foreach (var secondary in specification.ThenOrderBys)
        {
            ordered = secondary.Descending
                ? ordered.ThenByDescending(secondary.KeySelector)
                : ordered.ThenBy(secondary.KeySelector);
        }

        return ordered;
    }
}
