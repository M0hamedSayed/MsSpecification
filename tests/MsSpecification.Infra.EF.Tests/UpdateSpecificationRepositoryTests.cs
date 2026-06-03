using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class UpdateSpecificationRepositoryTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ExecuteUpdateAsync_UpdatesMatchingRows()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);

        var updated = await repo.ExecuteUpdateAsync(
            new ActiveLaptopSpec(),
#if NET10_0_OR_GREATER
            s => s.SetProperty(p => p.Stock, 999)
#else
            s => s.SetProperty(p => p.Stock, 999)
#endif
        );

        Assert.Equal(1, updated);

        var laptop = await ctx.Products.FindAsync(1);
        Assert.NotNull(laptop);
        Assert.Equal(999, laptop.Stock);
    }

    [Fact]
    public async Task ExecuteUpdateAsync_NoMatch_UpdatesZeroRows()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);

        var updated = await repo.ExecuteUpdateAsync(
            new ProductByIdSpec(9999),
#if NET10_0_OR_GREATER
            s => s.SetProperty(p => p.Stock, 0)
#else
            s => s.SetProperty(p => p.Stock, 0)
#endif
        );

        Assert.Equal(0, updated);
    }

    // ─── ExecuteDelete / ExecuteUpdate honor IgnoreQueryFilters (D2 regression) ──

    [Fact]
    public async Task ExecuteDeleteAsync_OrderWithIgnoreQueryFilters_DeletesSoftDeletedRows()
    {
        // Order entity has a soft-delete global query filter (!o.IsDeleted in TestDbContext).
        // Order id=4 is soft-deleted in the fixture seed and is normally invisible.
        // A spec that calls EnableIgnoreQueryFilters() should be able to delete it.
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Order>.ForDbContext(ctx);

        var beforeIncludingDeleted = await ctx.Orders.IgnoreQueryFilters().CountAsync();

        var deleted = await repo.ExecuteDeleteAsync(new SoftDeletedOrdersSpec());

        Assert.Equal(1, deleted);

        var afterIncludingDeleted = await ctx.Orders.IgnoreQueryFilters().CountAsync();
        Assert.Equal(beforeIncludingDeleted - 1, afterIncludingDeleted);
    }

    [Fact]
    public async Task ExecuteUpdateAsync_OrderWithIgnoreQueryFilters_UpdatesSoftDeletedRows()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Order>.ForDbContext(ctx);

        var updated = await repo.ExecuteUpdateAsync(
            new SoftDeletedOrdersSpec(),
#if NET10_0_OR_GREATER
            s => s.SetProperty(o => o.TotalAmount, 0m)
#else
            s => s.SetProperty(o => o.TotalAmount, 0m)
#endif
        );

        Assert.Equal(1, updated);

        var softDeletedTotal = await ctx.Orders.IgnoreQueryFilters()
            .Where(o => o.IsDeleted)
            .Select(o => o.TotalAmount)
            .SingleAsync();
        Assert.Equal(0m, softDeletedTotal);
    }

    private class ActiveLaptopSpec : MsSpec<Product>
    {
        public ActiveLaptopSpec()
        {
            Where(p => p.IsActive && p.Name == "Laptop");
        }
    }

    private class ProductByIdSpec : MsSpec<Product>
    {
        public ProductByIdSpec(int id) => Where(p => p.Id == id);
    }

    private class SoftDeletedOrdersSpec : MsSpec<Order>
    {
        public SoftDeletedOrdersSpec()
        {
            EnableIgnoreQueryFilters();
            Where(o => o.IsDeleted);
        }
    }
}
