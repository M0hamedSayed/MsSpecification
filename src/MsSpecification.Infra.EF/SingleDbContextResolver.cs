using Microsoft.EntityFrameworkCore;

namespace MsSpecification.Infra.EF;

/// <summary>
/// Trivial <see cref="IMsSpecificationDbContextResolver"/> that always returns the same
/// <see cref="DbContext"/> regardless of entity type. Use when constructing a
/// <see cref="SpecificationRepository{T}"/> manually with a single context — most commonly
/// from test fixtures or benchmarks.
/// </summary>
public sealed class SingleDbContextResolver : IMsSpecificationDbContextResolver
{
    private readonly DbContext _dbContext;

    public SingleDbContextResolver(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public DbContext GetContextForEntity(Type entityType) => _dbContext;
}
