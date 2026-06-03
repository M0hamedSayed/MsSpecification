using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Contracts;

namespace MsSpecification.Infra.EF;

/// <summary>
/// EF Core implementation of <see cref="ISpecificationRepository{T}"/>,
/// <see cref="IUpdateSpecificationRepository{T}"/>, and <see cref="IStreamingSpecificationRepository{T}"/>.
/// Handles raw SQL base queries, specification evaluation, and query execution.
/// </summary>
/// <typeparam name="T">Entity type mapped in the DbContext.</typeparam>
public class SpecificationRepository<T> : ISpecificationRepository<T>, IUpdateSpecificationRepository<T>, IStreamingSpecificationRepository<T>
    where T : class
{
    private readonly DbContext _dbContext;

    /// <summary>
    /// DI-friendly constructor used by the open-generic registration in
    /// <see cref="Extensions.ServiceCollectionExtensions.AddMsSpecification{TContext}"/>.
    /// Routes to the correct DbContext per entity type, supporting hosts with multiple contexts.
    /// For direct construction with a single DbContext, use <see cref="ForDbContext"/>.
    /// </summary>
    public SpecificationRepository(IMsSpecificationDbContextResolver resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        _dbContext = resolver.GetContextForEntity(typeof(T));
    }

    /// <summary>
    /// Convenience factory for direct construction with a single <see cref="DbContext"/>
    /// — useful for test fixtures, benchmarks, and advanced manual-DI scenarios.
    /// </summary>
    public static SpecificationRepository<T> ForDbContext(DbContext dbContext)
    {
        if (dbContext is null) throw new ArgumentNullException(nameof(dbContext));
        return new SpecificationRepository<T>(new SingleDbContextResolver(dbContext));
    }

    /// <inheritdoc/>
    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<T?> SingleOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.SingleOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.AnyAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TResult?> SingleOrDefaultAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.SingleOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec);
        return await query.ToListAsync(ct);
    }

    /// <inheritdoc/>
    [Obsolete("CountAsync<TResult> counts projected results which generates suboptimal SQL. " +
              "Use CountAsync(ISpecification<T>) for criteria-only counting instead.")]
    public async Task<int> CountAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default)
    {
        ThrowIfStreaming(spec);
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.CountAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<int> ExecuteDeleteAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc/>
#if NET10_0_OR_GREATER
    public async Task<int> ExecuteUpdateAsync(
        ISpecification<T> spec,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T>> setPropertyCalls,
        CancellationToken ct = default)
    {
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.ExecuteUpdateAsync(setPropertyCalls, ct);
    }
#else
    public async Task<int> ExecuteUpdateAsync(
        ISpecification<T> spec,
        Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>>> setPropertyCalls,
        CancellationToken ct = default)
    {
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.ExecuteUpdateAsync(setPropertyCalls, ct);
    }
#endif

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> StreamAsync(
        ISpecification<T> spec,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = ApplySpecification(spec);
        await foreach (var item in query.AsAsyncEnumerable().WithCancellation(ct).ConfigureAwait(false))
            yield return item;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TResult> StreamAsync<TResult>(
        ISpecification<T, TResult> spec,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = ApplySpecification(spec);
        await foreach (var item in query.AsAsyncEnumerable().WithCancellation(ct).ConfigureAwait(false))
            yield return item;
    }

    /// <summary>
    /// Returns the raw IQueryable after applying the specification.
    /// Useful for advanced scenarios where you need further manual composition.
    /// </summary>
    public IQueryable<T> GetQuery(ISpecification<T> spec)
        => ApplySpecification(spec);

    private IQueryable<T> ApplySpecification(ISpecification<T> spec, bool evaluateCriteriaOnly = false)
    {
        var baseQuery = GetBaseQuery(spec);
        return SpecificationEvaluator.GetQuery(baseQuery, spec, evaluateCriteriaOnly);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> spec, bool evaluateCriteriaOnly = false)
    {
        if (evaluateCriteriaOnly)
        {
            var baseQuery = GetBaseQuery(spec);
            var criteriaQuery = SpecificationEvaluator.GetQuery(baseQuery, (ISpecification<T>)spec, evaluateCriteriaOnly: true);
            if (spec.Selector is not null)
                return criteriaQuery.Select(spec.Selector);
            if (spec.GroupByApplier is not null)
                return spec.GroupByApplier(criteriaQuery);
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} has neither a Selector nor a GroupByApplier.");
        }

        var source = GetBaseQuery(spec);
        return SpecificationEvaluator.GetQuery(source, spec);
    }

    private IQueryable<T> GetBaseQuery(ISpecification<T> spec)
    {
        if (spec.RawSql is null)
            return _dbContext.Set<T>();

        // Avoid per-query allocation by passing the stored array straight through when possible.
        var parameters = spec.RawSqlParameters as object[] ?? spec.RawSqlParameters.ToArray();
        return _dbContext.Set<T>().FromSqlRaw(spec.RawSql, parameters);
    }

    private static void ThrowIfStreaming(ISpecification<T> spec)
    {
        if (spec.AsStreaming)
            throw new InvalidOperationException(
                $"Specification {spec.GetType().Name} is marked AsStreaming. " +
                "Use IStreamingSpecificationRepository<T>.StreamAsync instead of buffered query methods.");
    }
}
