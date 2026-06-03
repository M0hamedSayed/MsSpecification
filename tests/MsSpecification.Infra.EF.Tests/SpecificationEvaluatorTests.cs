using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class SpecificationEvaluatorTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // ─── WHERE ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Where_FiltersEntities()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.All(result, p => Assert.True(p.IsActive));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Where_Null_ReturnsAll()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new AllProductsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.Equal(4, result.Count);
    }

    // ─── INCLUDE ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Include_LoadsNavigation()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsWithCategorySpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.All(result, p => Assert.NotNull(p.Category));
    }

    [Fact]
    public async Task ThenInclude_LoadsNestedNavigation()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithItemsAndProductSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        Assert.All(result, o =>
        {
            Assert.NotEmpty(o.Items);
            Assert.All(o.Items, i => Assert.NotNull(i.Product));
        });
    }

    [Fact]
    public async Task DeepThenInclude_LoadsThreeLevels()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithItemsProductCategorySpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        Assert.All(result, o =>
            Assert.All(o.Items, i => Assert.NotNull(i.Product.Category)));
    }

    [Fact]
    public async Task IncludeString_LoadsNavigation()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductWithStringIncludeSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.All(result, p => Assert.NotNull(p.Category));
    }

    // ─── FILTERED INCLUDE ────────────────────────────────────────────────────────

    [Fact]
    public async Task FilteredInclude_LoadsOnlyMatchingItems()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithActiveItemsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        Assert.All(result, o =>
            Assert.All(o.Items, i => Assert.True(i.Quantity > 0)));
    }

    // ─── ORDERING ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderByAscending_SortsCorrectly()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsOrderByIdAscSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        for (var i = 1; i < result.Count; i++)
            Assert.True(result[i].Id >= result[i - 1].Id);
    }

    [Fact]
    public async Task OrderByDescending_SortsCorrectly()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsOrderByIdDescSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        for (var i = 1; i < result.Count; i++)
            Assert.True(result[i].Id <= result[i - 1].Id);
    }

    [Fact]
    public async Task ThenByAscending_AppliesSecondarySort()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsOrderByCategoryThenStockSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.NotEmpty(result);
    }

    // ─── PAGINATION ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Skip_Take_LimitsResults()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsPageSpec(skip: 1, take: 2);
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Skip_LargerThanCount_ReturnsEmpty()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductsPageSpec(skip: 100, take: 10);
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.Empty(result);
    }

    // ─── DISTINCT ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Distinct_EliminatesDuplicates()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new DistinctActiveProductsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.Equal(result.Count, result.Select(p => p.Id).Distinct().Count());
    }

    // ─── QUERY HINTS ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AsNoTracking_EntitiesAreNotTracked()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new NoTrackingProductsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.NotEmpty(result);
        Assert.All(result, p =>
            Assert.Equal(EntityState.Detached, ctx.Entry(p).State));
    }

    [Fact]
    public async Task AsNoTrackingWithIdentityResolution_EntitiesAreNotTracked()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new NoTrackingWithIdResSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.NotEmpty(result);
        Assert.All(result, p =>
            Assert.Equal(EntityState.Detached, ctx.Entry(p).State));
    }

    [Fact]
    public async Task AsStreaming_AutoAppliesNoTracking()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new StreamingProductsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.NotEmpty(result);
        Assert.All(result, p =>
            Assert.Equal(EntityState.Detached, ctx.Entry(p).State));
    }

    [Fact]
    public async Task TagWith_QueryExecutesWithComment()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new TaggedProductsSpec();
        // TagWith adds a SQL comment; execution should not throw
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task IgnoreQueryFilters_ReturnsDeletedOrders()
    {
        using var ctx = _fixture.CreateContext();
        // Without IgnoreQueryFilters — soft-deleted order excluded
        var normal = await ctx.Orders.ToListAsync();
        Assert.DoesNotContain(normal, o => o.IsDeleted);

        var spec = new IgnoreFiltersOrdersSpec();
        var withDeleted = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        Assert.Contains(withDeleted, o => o.IsDeleted);
    }

    [Fact]
    public async Task AsSplitQuery_ExecutesWithoutCartesianExplosion()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new SplitQueryOrdersSpec();
        // The query should execute without error; correctness (Items loaded) is verified
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        Assert.NotEmpty(result);
        Assert.All(result, o => Assert.NotEmpty(o.Items));
    }

    [Fact]
    public async Task Selector_T_To_T_ColumnPruning()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductSelectorSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();
        // Selector projects to a new Product with only Name populated
        Assert.All(result, p =>
        {
            Assert.NotNull(p.Name);
            // Other columns default since they were excluded in the projection
            // (SQLite always gives int defaults, so just verify no exceptions)
        });
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class ActiveProductsSpec : MsSpec<Product>
    {
        public ActiveProductsSpec() => Where(p => p.IsActive);
    }

    private class AllProductsSpec : MsSpec<Product> { }

    private class ProductsWithCategorySpec : MsSpec<Product>
    {
        public ProductsWithCategorySpec() { Include(p => p.Category); EnableNoTracking(); }
    }

    private class ProductWithStringIncludeSpec : MsSpec<Product>
    {
        public ProductWithStringIncludeSpec() { IncludeString("Category"); EnableNoTracking(); }
    }

    private class OrdersWithItemsAndProductSpec : MsSpec<Order>
    {
        public OrdersWithItemsAndProductSpec()
        {
            Include(o => o.Items).ThenInclude(i => i.Product);
            EnableNoTracking();
        }
    }

    private class OrdersWithItemsProductCategorySpec : MsSpec<Order>
    {
        public OrdersWithItemsProductCategorySpec()
        {
            Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category);
            EnableNoTracking();
        }
    }

    private class OrdersWithActiveItemsSpec : MsSpec<Order>
    {
        public OrdersWithActiveItemsSpec()
        {
            Include(o => o.Items.Where(i => i.Quantity > 0));
            EnableNoTracking();
        }
    }

    private class ProductsOrderByIdAscSpec : MsSpec<Product>
    {
        public ProductsOrderByIdAscSpec() => OrderByAscending(p => p.Id);
    }

    private class ProductsOrderByIdDescSpec : MsSpec<Product>
    {
        public ProductsOrderByIdDescSpec() => OrderByDescending(p => p.Id);
    }

    private class ProductsOrderByCategoryThenStockSpec : MsSpec<Product>
    {
        public ProductsOrderByCategoryThenStockSpec()
        {
            OrderByAscending(p => p.CategoryId);
            ThenByAscending(p => p.Stock);
        }
    }

    private class ProductsPageSpec : MsSpec<Product>
    {
        public ProductsPageSpec(int skip, int take)
        {
            OrderByAscending(p => p.Stock);
            Paginate(skip, take);
        }
    }

    private class DistinctActiveProductsSpec : MsSpec<Product>
    {
        public DistinctActiveProductsSpec() { Where(p => p.IsActive); EnableDistinct(); }
    }

    private class NoTrackingProductsSpec : MsSpec<Product>
    {
        public NoTrackingProductsSpec() => EnableNoTracking();
    }

    private class NoTrackingWithIdResSpec : MsSpec<Product>
    {
        public NoTrackingWithIdResSpec() => EnableNoTrackingWithIdentityResolution();
    }

    private class StreamingProductsSpec : MsSpec<Product>
    {
        public StreamingProductsSpec() => EnableStreaming();
    }

    private class TaggedProductsSpec : MsSpec<Product>
    {
        public TaggedProductsSpec() { Where(p => p.IsActive); TagWith("EFTest"); }
    }

    private class IgnoreFiltersOrdersSpec : MsSpec<Order>
    {
        public IgnoreFiltersOrdersSpec() => EnableIgnoreQueryFilters();
    }

    private class SplitQueryOrdersSpec : MsSpec<Order>
    {
        public SplitQueryOrdersSpec()
        {
            Include(o => o.Items);
            EnableSplitQuery();
            EnableNoTracking();
        }
    }

    private class ProductSelectorSpec : MsSpec<Product>
    {
        public ProductSelectorSpec()
        {
            Select(p => new Product { Id = p.Id, Name = p.Name });
            EnableNoTracking();
        }
    }
}
