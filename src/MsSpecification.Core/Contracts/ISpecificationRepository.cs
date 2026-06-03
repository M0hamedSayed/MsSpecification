namespace MsSpecification.Core.Contracts;

/// <summary>
/// Provides a high-level query API for specifications.
/// Implementations translate specifications into database queries and execute them.
/// </summary>
/// <typeparam name="T">Entity type (must be a reference type mapped in the ORM)</typeparam>
public interface ISpecificationRepository<T> where T : class
{
    /// <summary>Returns the first entity matching the specification, or null.</summary>
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Returns the single entity matching the specification, or null. Throws if more than one match.</summary>
    Task<T?> SingleOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Returns all entities matching the specification.</summary>
    Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Returns the count of entities matching the specification criteria.</summary>
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Returns true if any entity matches the specification criteria.</summary>
    Task<bool> AnyAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>Returns the first projected result matching the specification, or null/default.</summary>
    Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default);

    /// <summary>Returns the single projected result matching the specification, or null/default. Throws if more than one match.</summary>
    Task<TResult?> SingleOrDefaultAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default);

    /// <summary>Returns all projected results matching the specification.</summary>
    Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default);

    /// <summary>
    /// Returns the count of projected results matching the specification criteria.
    /// </summary>
    /// <remarks>
    /// This method generates suboptimal SQL because it counts after projection.
    /// Prefer <see cref="CountAsync(ISpecification{T}, CancellationToken)"/> for criteria-only counting.
    /// </remarks>
    [Obsolete("CountAsync<TResult> counts projected results which generates suboptimal SQL. " +
              "Use CountAsync(ISpecification<T>) for criteria-only counting instead.")]
    Task<int> CountAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default);

    /// <summary>
    /// Bulk-deletes entities matching the specification criteria (provider-dependent; EF Core 7+).
    /// </summary>
    Task<int> ExecuteDeleteAsync(ISpecification<T> spec, CancellationToken ct = default);
}
