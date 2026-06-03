using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace MsSpecification.Infra.EF;

/// <summary>
/// Default resolver that walks all registered <see cref="DbContext"/> instances and selects the one
/// whose model contains the requested entity type. Caches the mapping per entity for subsequent calls.
/// </summary>
internal sealed class MsSpecificationDbContextResolver : IMsSpecificationDbContextResolver
{
    private readonly IReadOnlyList<DbContext> _contexts;
    private readonly ConcurrentDictionary<Type, DbContext> _entityToContext = new();

    public MsSpecificationDbContextResolver(IEnumerable<DbContext> contexts)
    {
        if (contexts is null) throw new ArgumentNullException(nameof(contexts));
        _contexts = contexts as IReadOnlyList<DbContext> ?? contexts.ToList();
        if (_contexts.Count == 0)
            throw new InvalidOperationException(
                "MsSpecification: no DbContext has been registered. " +
                "Call services.AddMsSpecification<TContext>() at least once.");
    }

    public DbContext GetContextForEntity(Type entityType)
    {
        if (entityType is null) throw new ArgumentNullException(nameof(entityType));

        return _entityToContext.GetOrAdd(entityType, t =>
        {
            DbContext? owning = null;
            foreach (var ctx in _contexts)
            {
                if (ctx.Model.FindEntityType(t) is null) continue;

                if (owning is not null)
                {
                    throw new InvalidOperationException(
                        $"Entity '{t.FullName}' is mapped by multiple registered DbContexts " +
                        $"('{owning.GetType().Name}' and '{ctx.GetType().Name}'). " +
                        "MsSpecification cannot disambiguate. Inject the desired DbContext directly " +
                        $"and construct SpecificationRepository<{t.Name}> manually for this case.");
                }

                owning = ctx;
            }

            return owning ?? throw new InvalidOperationException(
                $"Entity '{t.FullName}' is not mapped by any registered DbContext. " +
                "Ensure AddMsSpecification<TContext>() was called for the context that owns this entity.");
        });
    }
}
