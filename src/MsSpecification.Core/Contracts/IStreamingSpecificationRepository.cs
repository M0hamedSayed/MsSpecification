namespace MsSpecification.Core.Contracts;

/// <summary>
/// Provides streaming query capability for specifications.
/// Returns <see cref="IAsyncEnumerable{T}"/> for memory-efficient enumeration of large result sets
/// without buffering the entire result in memory.
/// </summary>
/// <typeparam name="T">Entity type (must be a reference type mapped in the ORM)</typeparam>
public interface IStreamingSpecificationRepository<T> where T : class
{
    /// <summary>
    /// Streams all entities matching the specification as an async enumerable.
    /// Use this instead of <see cref="ISpecificationRepository{T}.ListAsync"/> for large result sets
    /// to avoid buffering all rows in memory.
    /// </summary>
    IAsyncEnumerable<T> StreamAsync(ISpecification<T> spec, CancellationToken ct = default);

    /// <summary>
    /// Streams all projected results matching the specification as an async enumerable.
    /// </summary>
    IAsyncEnumerable<TResult> StreamAsync<TResult>(ISpecification<T, TResult> spec, CancellationToken ct = default);
}
