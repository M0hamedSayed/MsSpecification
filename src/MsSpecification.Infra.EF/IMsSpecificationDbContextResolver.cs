using Microsoft.EntityFrameworkCore;

namespace MsSpecification.Infra.EF;

/// <summary>
/// Resolves the <see cref="DbContext"/> that owns a given entity type. Provided by the
/// <c>AddMsSpecification&lt;TContext&gt;</c> DI registration so a single host can hold multiple
/// <see cref="DbContext"/> types and have repositories route entities to the correct context.
/// </summary>
/// <remarks>
/// For single-context applications this is fully transparent — register one context with
/// <c>AddMsSpecification&lt;TContext&gt;()</c> and inject <c>ISpecificationRepository&lt;T&gt;</c> as before.
/// For multi-context applications, call <c>AddMsSpecification&lt;TContext&gt;()</c> once per context;
/// the resolver collects them all and dispatches each repository to the context whose model maps T.
/// </remarks>
public interface IMsSpecificationDbContextResolver
{
    /// <summary>
    /// Returns the registered <see cref="DbContext"/> whose model contains <paramref name="entityType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no registered context maps the entity, or if more than one does.
    /// </exception>
    DbContext GetContextForEntity(Type entityType);
}
