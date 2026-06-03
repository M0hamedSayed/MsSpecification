using System.Linq.Expressions;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Models;

namespace MsSpecification.Core.Contracts;

/// <summary>
/// Base class for all specifications.
/// Provides a fluent protected API for building criteria, includes, ordering, pagination,
/// projections, raw SQL, and all EF Core query hints.
///
/// Design decisions:
/// - All builder methods are protected — only the specification itself configures itself.
/// - Lazy initialization with null coalescing avoids allocations for unused features.
/// - IncludeChain uses ArrayPool internally for near-zero allocation of steps.
/// - Type metadata is resolved via generic specialization, never via runtime reflection in hot paths.
/// </summary>
/// <typeparam name="T">Root entity type (must be a reference type mapped in the ORM).</typeparam>
public abstract class MsSpec<T> : ISpecification<T>
    where T : class
{
    private List<IncludeChain<T>>? _includes;
    private List<OrderByClause<T>>? _thenOrderBys;
    private List<string>? _includeStrings;
    private object[]? _rawSqlParameters;

    // ─────────────────────────────────────────────────────────────────────────────
    // ISpecification<T> implementation
    // ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<IncludeChain<T>> Includes
        => _includes ?? (IReadOnlyList<IncludeChain<T>>)Array.Empty<IncludeChain<T>>();

    /// <inheritdoc/>
    public IReadOnlyList<string> IncludeStrings
        => _includeStrings ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <inheritdoc/>
    public OrderByClause<T>? OrderBy { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<OrderByClause<T>> ThenOrderBys
        => _thenOrderBys ?? (IReadOnlyList<OrderByClause<T>>)Array.Empty<OrderByClause<T>>();

    /// <inheritdoc/>
    public int? Skip { get; private set; }

    /// <inheritdoc/>
    public int? Take { get; private set; }

    /// <inheritdoc/>
    public bool AsNoTracking { get; private set; }

    /// <inheritdoc/>
    public bool AsNoTrackingWithIdentityResolution { get; private set; }

    /// <inheritdoc/>
    public bool AsSplitQuery { get; private set; }

    /// <inheritdoc/>
    public bool AsStreaming { get; private set; }

    /// <inheritdoc/>
    public Expression<Func<T, T>>? IdentitySelector { get; private set; }

    /// <inheritdoc/>
    public bool IsDistinct { get; private set; }

    /// <inheritdoc/>
    public string? QueryTag { get; private set; }

    /// <inheritdoc/>
    public bool IgnoreQueryFilters { get; private set; }

    /// <inheritdoc/>
    public string? RawSql { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<object> RawSqlParameters
        => _rawSqlParameters ?? Array.Empty<object>();

    /// <summary>
    /// Evaluate the specification in-memory — useful for unit tests and domain logic.
    /// Compiles the expression tree once per spec instance (cached via Lazy).
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        if (Criteria is null) return true;
        return _compiledCriteria.Value(entity);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Criteria
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the filter predicate.
    /// Multiple calls replace the previous criteria (use <see cref="And"/> / <see cref="Or"/> to combine).
    /// </summary>
    protected void Where(Expression<Func<T, bool>> criteria)
        => Criteria = criteria;

    /// <summary>Combines current criteria with another using AND.</summary>
    protected void And(Expression<Func<T, bool>> criteria)
    {
        Criteria = Criteria is null
            ? criteria
            : CombineExpressions(Criteria, criteria, ExpressionType.AndAlso);
    }

    /// <summary>Combines current criteria with another using OR.</summary>
    protected void Or(Expression<Func<T, bool>> criteria)
    {
        Criteria = Criteria is null
            ? criteria
            : CombineExpressions(Criteria, criteria, ExpressionType.OrElse);
    }

    /// <summary>Negates the current criteria.</summary>
    protected void Not()
    {
        if (Criteria is null) return;
        var param = Criteria.Parameters[0];
        var negated = Expression.Not(Criteria.Body);
        Criteria = Expression.Lambda<Func<T, bool>>(negated, param);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Includes
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts an include chain for a reference navigation property.
    /// Returns a builder that supports chaining ThenInclude calls.
    /// </summary>
    protected IncludeChainBuilder<T, TProperty> Include<TProperty>(
        Expression<Func<T, TProperty>> expression)
    {
        var chain = new IncludeChain<T>();

        chain.AddStep(new IncludeStep(
            navigationPropertyPath: expression,
            fromType: TypeMetadataCache<T, TProperty>.FromType,
            toType: TypeMetadataCache<T, TProperty>.ToType,
            isCollection: TypeMetadataCache<T, TProperty>.IsCollection,
            propertyName: ExpressionNameCache.GetPropertyName(expression),
            isFiltered: ExpressionNameCache.IsFilteredExpression(expression)
        ));

        (_includes ??= new List<IncludeChain<T>>()).Add(chain);
        return new IncludeChainBuilder<T, TProperty>(chain);
    }

    /// <summary>Starts an include chain for an IEnumerable collection navigation.</summary>
    protected IncludeChainBuilder<T, TElement> Include<TElement>(
        Expression<Func<T, IEnumerable<TElement>>> expression)
        where TElement : class
        => AddCollectionInclude<IEnumerable<TElement>, TElement>(expression);

    /// <summary>Starts an include chain for an ICollection navigation.</summary>
    protected IncludeChainBuilder<T, TElement> Include<TElement>(
        Expression<Func<T, ICollection<TElement>>> expression)
        where TElement : class
        => AddCollectionInclude<ICollection<TElement>, TElement>(expression);

    /// <summary>Starts an include chain for a List navigation.</summary>
    protected IncludeChainBuilder<T, TElement> Include<TElement>(
        Expression<Func<T, List<TElement>>> expression)
        where TElement : class
        => AddCollectionInclude<List<TElement>, TElement>(expression);

    /// <summary>Starts an include chain for an IReadOnlyCollection navigation.</summary>
    protected IncludeChainBuilder<T, TElement> Include<TElement>(
        Expression<Func<T, IReadOnlyCollection<TElement>>> expression)
        where TElement : class
        => AddCollectionInclude<IReadOnlyCollection<TElement>, TElement>(expression);

    /// <summary>Starts an include chain for an IReadOnlyList navigation.</summary>
    protected IncludeChainBuilder<T, TElement> Include<TElement>(
        Expression<Func<T, IReadOnlyList<TElement>>> expression)
        where TElement : class
        => AddCollectionInclude<IReadOnlyList<TElement>, TElement>(expression);

    /// <summary>Adds a string-based include path. Prefer the strongly-typed overloads when possible.</summary>
    protected void IncludeString(string navigationPropertyPath)
        => (_includeStrings ??= new List<string>()).Add(navigationPropertyPath);

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Ordering
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Sets the primary ascending sort key.</summary>
    protected void OrderByAscending(Expression<Func<T, object?>> keySelector)
    {
        OrderBy = new OrderByClause<T>(keySelector, descending: false);
        _thenOrderBys?.Clear();
    }

    /// <summary>Sets the primary descending sort key.</summary>
    protected void OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        OrderBy = new OrderByClause<T>(keySelector, descending: true);
        _thenOrderBys?.Clear();
    }

    /// <summary>Adds a secondary ascending sort key.</summary>
    protected void ThenByAscending(Expression<Func<T, object?>> keySelector)
        => (_thenOrderBys ??= new List<OrderByClause<T>>())
            .Add(new OrderByClause<T>(keySelector, descending: false));

    /// <summary>Adds a secondary descending sort key.</summary>
    protected void ThenByDescending(Expression<Func<T, object?>> keySelector)
        => (_thenOrderBys ??= new List<OrderByClause<T>>())
            .Add(new OrderByClause<T>(keySelector, descending: true));

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Pagination
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Applies skip/take pagination.</summary>
    protected void Paginate(int skip, int take)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be >= 0.");
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take), "Take must be > 0.");
        Skip = skip;
        Take = take;
    }

    /// <summary>
    /// Convenience method for page-based pagination — equivalent to
    /// <c>Paginate((pageNumber - 1) * pageSize, pageSize)</c>.
    /// </summary>
    /// <param name="pageNumber">
    /// The page to retrieve. <strong>1-indexed</strong>: <c>Page(1, 20)</c> returns rows 1–20,
    /// <c>Page(2, 20)</c> returns rows 21–40. Must be ≥ 1; passing 0 or a negative value throws.
    /// </param>
    /// <param name="pageSize">Number of rows per page. Must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageNumber"/> &lt; 1 or <paramref name="pageSize"/> ≤ 0.
    /// </exception>
    protected void Page(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1.");
        Paginate((pageNumber - 1) * pageSize, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Query hints & behavior
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Disables EF change tracking for read-only queries.</summary>
    protected void EnableNoTracking() => AsNoTracking = true;

    /// <summary>Disables change tracking but preserves identity resolution across navigations.</summary>
    protected void EnableNoTrackingWithIdentityResolution() => AsNoTrackingWithIdentityResolution = true;

    /// <summary>Enables split query mode to avoid cartesian explosion with collection includes.</summary>
    protected void EnableSplitQuery() => AsSplitQuery = true;

    /// <summary>Enables streaming semantics (AsAsyncEnumerable) — caller must enumerate.</summary>
    protected void EnableStreaming() => AsStreaming = true;

    /// <summary>Applies DISTINCT to the query result.</summary>
    protected void EnableDistinct() => IsDistinct = true;

    /// <summary>Bypasses global query filters (e.g. soft-delete, multi-tenancy).</summary>
    protected void EnableIgnoreQueryFilters() => IgnoreQueryFilters = true;

    /// <summary>Adds a diagnostic tag to the generated SQL (appears as SQL comment).</summary>
    protected void TagWith(string tag) => QueryTag = tag;

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Projection & Grouping
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets a projection expression that reshapes T → T (column pruning — same type, fewer columns).
    /// For projecting to a different type, derive <see cref="MsSpec{T, TResult}"/> and call its <c>Select</c> instead.
    /// </summary>
    protected void Select(Expression<Func<T, T>> selector) => IdentitySelector = selector;

    // ─────────────────────────────────────────────────────────────────────────────
    // Protected builder API — Raw SQL / Stored Procedures / Functions
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets a raw SQL query that will be used as the base source via FromSqlRaw.
    /// Supports stored procedures, table-valued functions, and ad-hoc SQL.
    /// All other spec clauses (Where, OrderBy, Includes, etc.) compose on top.
    /// </summary>
    protected void FromSqlRaw(string sql, params object[] parameters)
    {
        RawSql = sql;
        // Store the array directly so the repository can pass it straight to FromSqlRaw
        // without an intermediate copy. The params array is owned by us at this point.
        _rawSqlParameters = parameters ?? Array.Empty<object>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private IncludeChainBuilder<T, TElement> AddCollectionInclude<TCollection, TElement>(
        LambdaExpression expression)
    {
        var chain = new IncludeChain<T>();

        chain.AddStep(new IncludeStep(
            navigationPropertyPath: expression,
            fromType: typeof(T),
            toType: typeof(TCollection),
            isCollection: true,
            propertyName: ExpressionNameCache.GetPropertyName(expression),
            isFiltered: ExpressionNameCache.IsFilteredExpression(expression)
        ));

        (_includes ??= new List<IncludeChain<T>>()).Add(chain);
        return new IncludeChainBuilder<T, TElement>(chain);
    }

    /// <summary>
    /// Combines two predicate expressions using the specified binary operator.
    /// Rebinds the parameter of the right expression to match the left.
    /// </summary>
    private static Expression<Func<T, bool>> CombineExpressions(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        ExpressionType nodeType)
    {
        var param = left.Parameters[0];
        var rightBody = new ParameterReplacerVisitor(right.Parameters[0], param).Visit(right.Body);
        var combined = Expression.MakeBinary(nodeType, left.Body, rightBody);
        return Expression.Lambda<Func<T, bool>>(combined, param);
    }

    private Lazy<Func<T, bool>> _compiledCriteria
        => _lazyCompiled ??= new Lazy<Func<T, bool>>(
            () => Criteria?.Compile() ?? (_ => true),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private Lazy<Func<T, bool>>? _lazyCompiled;
}

/// <summary>
/// Typed specification base with strongly-typed result projection support.
/// Use this when the query projects to a DTO or view model type.
/// </summary>
/// <typeparam name="T">Root entity type (must be a reference type mapped in the ORM).</typeparam>
/// <typeparam name="TResult">Projected result type</typeparam>
public abstract class MsSpec<T, TResult> : MsSpec<T>, ISpecification<T, TResult>
    where T : class
{
    /// <inheritdoc/>
    public Expression<Func<T, TResult>>? Selector { get; private set; }

    /// <inheritdoc/>
    public Func<IQueryable<T>, IQueryable<TResult>>? GroupByApplier { get; private set; }

    /// <summary>
    /// Sets a strongly-typed projection selector T → TResult.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="GroupBy{TKey}"/> has already been configured on this specification —
    /// <c>Select</c> and <c>GroupBy</c> are mutually exclusive.
    /// </exception>
    protected void Select(Expression<Func<T, TResult>> selector)
    {
        if (GroupByApplier is not null)
            throw new InvalidOperationException(
                $"Specification {GetType().Name}: Select cannot be combined with GroupBy. " +
                "Either remove the GroupBy call or fold the projection into the GroupBy resultSelector.");

        Selector = selector;
    }

    /// <summary>
    /// Configures a server-side GROUP BY aggregation that produces <typeparamref name="TResult"/> rows.
    /// When set, the evaluator runs criteria, includes and ordering first, then applies
    /// <paramref name="keySelector"/> + <paramref name="resultSelector"/> as a single SQL aggregate query.
    /// This replaces <see cref="Select"/>; both cannot be used together.
    /// </summary>
    /// <typeparam name="TKey">Group key type.</typeparam>
    /// <param name="keySelector">Expression selecting the column(s) to group by.</param>
    /// <param name="resultSelector">Projection from <see cref="IGrouping{TKey,T}"/> to <typeparamref name="TResult"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="Select"/> has already been configured on this specification.
    /// </exception>
    protected void GroupBy<TKey>(
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<IGrouping<TKey, T>, TResult>> resultSelector)
    {
        if (Selector is not null)
            throw new InvalidOperationException(
                $"Specification {GetType().Name}: GroupBy cannot be combined with Select. " +
                "Either remove the Select call or fold its projection into the GroupBy resultSelector.");

        GroupByApplier = source => source.GroupBy(keySelector).Select(resultSelector);
    }
}
