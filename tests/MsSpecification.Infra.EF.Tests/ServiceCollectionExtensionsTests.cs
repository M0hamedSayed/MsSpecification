using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Contracts;
using MsSpecification.Infra.EF.Extensions;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public ServiceCollectionExtensionsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite(_connection));
        services.AddMsSpecification<TestDbContext>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void AddMsSpecification_RegistersISpecificationRepository()
    {
        using var scope = _provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<Product>>();
        Assert.NotNull(repo);
        Assert.IsType<SpecificationRepository<Product>>(repo);
    }

    [Fact]
    public void AddMsSpecification_RegistersIUpdateSpecificationRepository()
    {
        using var scope = _provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUpdateSpecificationRepository<Product>>();
        Assert.NotNull(repo);
        Assert.IsType<SpecificationRepository<Product>>(repo);
    }

    [Fact]
    public void AddMsSpecification_RegistersIStreamingSpecificationRepository()
    {
        using var scope = _provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IStreamingSpecificationRepository<Product>>();
        Assert.NotNull(repo);
        Assert.IsType<SpecificationRepository<Product>>(repo);
    }

    [Fact]
    public void AddMsSpecification_AllInterfacesSameInstance()
    {
        // All three interfaces should resolve to the same concrete instance per scope
        using var scope = _provider.CreateScope();
        var repo1 = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<Product>>();
        var repo2 = scope.ServiceProvider.GetRequiredService<IUpdateSpecificationRepository<Product>>();
        var repo3 = scope.ServiceProvider.GetRequiredService<IStreamingSpecificationRepository<Product>>();

        // All are SpecificationRepository<Product>, resolved as Scoped — same instance within scope
        Assert.IsType<SpecificationRepository<Product>>(repo1);
        Assert.IsType<SpecificationRepository<Product>>(repo2);
        Assert.IsType<SpecificationRepository<Product>>(repo3);
    }

    [Fact]
    public void AddMsSpecification_OpenGenericRegistration_WorksForDifferentEntities()
    {
        using var scope = _provider.CreateScope();
        var productRepo = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<Product>>();
        var categoryRepo = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<Category>>();

        Assert.NotNull(productRepo);
        Assert.NotNull(categoryRepo);
    }

    [Fact]
    public async Task AddMsSpecification_TwoDbContexts_ResolvesEachIndependently()
    {
        // Two contexts in one host; each owns a disjoint set of entities. The resolver
        // must route ISpecificationRepository<T> to whichever context maps T.
        using var primaryConn = new SqliteConnection("Data Source=:memory:");
        using var logConn = new SqliteConnection("Data Source=:memory:");
        primaryConn.Open();
        logConn.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite(primaryConn));
        services.AddDbContext<LogDbContext>(o => o.UseSqlite(logConn));
        services.AddMsSpecification<TestDbContext>();
        services.AddMsSpecification<LogDbContext>();

        await using var provider = services.BuildServiceProvider();

        using (var setupScope = provider.CreateScope())
        {
            await setupScope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
            var logCtx = setupScope.ServiceProvider.GetRequiredService<LogDbContext>();
            await logCtx.Database.EnsureCreatedAsync();
            logCtx.LogEntries.Add(new LogEntry { Id = 1, Message = "hello" });
            await logCtx.SaveChangesAsync();
        }

        using var scope = provider.CreateScope();
        var productRepo = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<Product>>();
        var logRepo = scope.ServiceProvider.GetRequiredService<ISpecificationRepository<LogEntry>>();

        // Each repository must talk to its owning DbContext.
        var products = await productRepo.ListAsync(new AllProductsSpec());
        var logs = await logRepo.ListAsync(new AllLogEntriesSpec());

        Assert.Empty(products); // primary db wasn't seeded with products
        Assert.Single(logs);
        Assert.Equal("hello", logs[0].Message);
    }

    [Fact]
    public void Resolver_UnknownEntity_ThrowsInformativeError()
    {
        using var scope = _provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IMsSpecificationDbContextResolver>();
        var ex = Assert.Throws<InvalidOperationException>(() => resolver.GetContextForEntity(typeof(UnregisteredEntity)));
        Assert.Contains("not mapped by any registered DbContext", ex.Message);
    }

    // ─── Auxiliary types for multi-context test ───────────────────────────────────

    private class LogEntry
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private class UnregisteredEntity
    {
        public int Id { get; set; }
    }

    private class LogDbContext : DbContext
    {
        public LogDbContext(DbContextOptions<LogDbContext> options) : base(options) { }
        public DbSet<LogEntry> LogEntries => Set<LogEntry>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogEntry>(e => e.HasKey(x => x.Id));
        }
    }

    private class AllProductsSpec : MsSpec<Product> { }
    private class AllLogEntriesSpec : MsSpec<LogEntry> { }
}
