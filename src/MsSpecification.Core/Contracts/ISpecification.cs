using System.Linq.Expressions;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Models;

namespace MsSpecification.Core.Contracts;

/// <summary>
/// Core specification contract defining all query shaping capabilities.
/// Implementations describe WHAT to query, not HOW — the evaluator/repository handles execution.
/// </summary>
/// <typeparam name="T">Entity type (must be a reference type mapped in the ORM).</typeparam>
public interface ISpecification<T> where T : class
{
    /// <summary>WHERE clause predicate.</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>Strongly-typed include chains (root includes + their ThenInclude steps).</summary>
    IReadOnlyList<IncludeChain<T>> Includes { get; }

    /// <summary>String-based includes (escape hatch for complex navigation paths).</summary>
    IReadOnlyList<string> IncludeStrings { get; }

    /// <summary>Primary ordering clause. Null means no ordering.</summary>
    OrderByClause<T>? OrderBy { get; }

    /// <summary>Secondary ordering clauses applied after the primary.</summary>
    IReadOnlyList<OrderByClause<T>> ThenOrderBys { get; }

    /// <summary>Pagination: rows to skip.</summary>
    int? Skip { get; }

    /// <summary>Pagination: rows to take.</summary>
    int? Take { get; }

    /// <summary>Disables change tracking for read-only queries.</summary>
    bool AsNoTracking { get; }

    /// <summary>Disables change tracking but preserves identity resolution across navigation properties.</summary>
    bool AsNoTrackingWithIdentityResolution { get; }

    /// <summary>Splits the query into multiple SQL statements (avoids cartesian explosion with collection includes).</summary>
    bool AsSplitQuery { get; }

    /// <summary>Enables streaming semantics (AsAsyncEnumerable) — caller is responsible for enumeration.</summary>
    bool AsStreaming { get; }

    /// <summary>
    /// SELECT projection that reshapes T → T (column pruning). Null means select all columns.
    /// For projecting to a different type, derive <see cref="ISpecification{T, TResult}"/> and use
    /// its <c>Selector</c> instead.
    /// </summary>
    Expression<Func<T, T>>? IdentitySelector { get; }

    /// <summary>Applies DISTINCT to the query result set.</summary>
    bool IsDistinct { get; }

    /// <summary>Adds a comment tag to the generated SQL for diagnostics/profiling.</summary>
    string? QueryTag { get; }

    /// <summary>Bypasses global query filters (e.g. soft-delete, multi-tenancy).</summary>
    bool IgnoreQueryFilters { get; }

    /// <summary>Raw SQL string for FromSqlRaw. Null means standard LINQ query.</summary>
    string? RawSql { get; }

    /// <summary>Parameters for the raw SQL query.</summary>
    IReadOnlyList<object> RawSqlParameters { get; }

    /// <summary>Evaluate the spec in-memory (for unit tests / domain logic). Returns true when criteria is null.</summary>
    bool IsSatisfiedBy(T entity);
}

/// <summary>
/// Specification that projects the query result to a different type (DTO / view model).
/// </summary>
/// <typeparam name="T">Source entity type.</typeparam>
/// <typeparam name="TResult">Projected result type.</typeparam>
public interface ISpecification<T, TResult> : ISpecification<T>
    where T : class
{
    /// <summary>
    /// Strongly-typed projection selector T → TResult.
    /// Mutually exclusive with <see cref="GroupByApplier"/>; only one may be set per specification.
    /// </summary>
    /// <remarks>
    /// This is a distinct property from <see cref="ISpecification{T}.IdentitySelector"/> — projected
    /// specifications use this typed selector; column-pruning specs use the base identity selector.
    /// </remarks>
    Expression<Func<T, TResult>>? Selector { get; }

    /// <summary>
    /// Optional typed GROUP BY applier.  When non-null, the evaluator feeds the filtered/ordered
    /// <see cref="IQueryable{T}"/> into this delegate instead of the plain <see cref="Selector"/>.
    /// Build it via <c>GroupBy(keySelector, resultSelector)</c> on <see cref="MsSpec{T,TResult}"/>.
    /// </summary>
    Func<IQueryable<T>, IQueryable<TResult>>? GroupByApplier { get; }
}
