using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Contracts;

namespace MsSpecification.Infra.EF.Extensions;

/// <summary>
/// Extension methods for registering MsSpecification services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SpecificationRepository{T}"/> as the implementation for
    /// <see cref="ISpecificationRepository{T}"/>, <see cref="IUpdateSpecificationRepository{T}"/>,
    /// and <see cref="IStreamingSpecificationRepository{T}"/> using the specified DbContext type.
    /// </summary>
    /// <remarks>
    /// Call this once per <see cref="DbContext"/> the application uses. Each call adds the context
    /// to an internal resolver that routes repositories to the correct context for each entity type.
    /// Single-context applications behave exactly as before; multi-context applications can register
    /// any number of contexts and each repository injection automatically targets the right one.
    /// </remarks>
    /// <typeparam name="TContext">The DbContext type to resolve from DI.</typeparam>
    public static IServiceCollection AddMsSpecification<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Repository registrations are idempotent — only the first AddMsSpecification call adds them.
        services.TryAdd(ServiceDescriptor.Scoped(typeof(ISpecificationRepository<>), typeof(SpecificationRepository<>)));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IUpdateSpecificationRepository<>), typeof(SpecificationRepository<>)));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IStreamingSpecificationRepository<>), typeof(SpecificationRepository<>)));

        // The resolver is also single-instance per request scope; only registered once.
        services.TryAddScoped<IMsSpecificationDbContextResolver, MsSpecificationDbContextResolver>();

        // Each AddMsSpecification<TContext> appends a DbContext factory. The resolver injects the
        // full IEnumerable<DbContext> so it can see every context that's been registered.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
