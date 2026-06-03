using System.Linq.Expressions;
using MsSpecification.Core.Contracts;

namespace MsSpecification.Infra.EF.Contracts;

/// <summary>
/// Provides bulk-update capability for specifications.
/// Separated from <see cref="ISpecificationRepository{T}"/> because the signature
/// depends on EF Core types that must not leak into the Core package.
/// </summary>
/// <typeparam name="T">Entity type (must be a reference type mapped in the ORM)</typeparam>
public interface IUpdateSpecificationRepository<T> where T : class
{
    /// <summary>
    /// Bulk-updates all entities matching the specification criteria.
    /// Uses EF Core ExecuteUpdateAsync for server-side execution without loading entities.
    /// </summary>
#if NET10_0_OR_GREATER
    Task<int> ExecuteUpdateAsync(
        ISpecification<T> spec,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T>> setPropertyCalls,
        CancellationToken ct = default);
#else
    Task<int> ExecuteUpdateAsync(
        ISpecification<T> spec,
        Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>,
                        Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>>> setPropertyCalls,
        CancellationToken ct = default);
#endif
}
