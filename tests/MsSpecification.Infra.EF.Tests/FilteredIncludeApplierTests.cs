using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class FilteredIncludeApplierTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // ─── Filtered root include ────────────────────────────────────────────────────

    [Fact]
    public async Task FilteredRootInclude_LoadsOnlyMatchingItems()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithItemsQuantityGt1Spec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();

        // Order 3 has an item with Quantity=2 (>1), orders 1 and 2 have Quantity=1 (not >1)
        Assert.All(result, o =>
            Assert.All(o.Items, i => Assert.True(i.Quantity > 1)));
    }

    [Fact]
    public async Task FilteredRootInclude_EmptyResult_WhenNoItemsMatch()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithItemsQuantityGt100Spec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();
        // Orders should still be returned; their Items collections just empty
        Assert.NotEmpty(result);
        Assert.All(result, o => Assert.Empty(o.Items));
    }

    // ─── Filtered include + ThenInclude ──────────────────────────────────────────

    [Fact]
    public async Task FilteredInclude_ThenInclude_LoadsNestedNavigation()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithFilteredItemsThenProductSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();

        Assert.NotEmpty(result);
        var ordersWithItems = result.Where(o => o.Items.Any()).ToList();
        Assert.All(ordersWithItems, o =>
            Assert.All(o.Items, i => Assert.NotNull(i.Product)));
    }

    // ─── Deep filtered chain (3 levels) ──────────────────────────────────────────

    [Fact]
    public async Task FilteredInclude_DeepChain_ThreeLevels()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersWithFilteredItemsProductCategorySpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();

        Assert.NotEmpty(result);
        var ordersWithItems = result.Where(o => o.Items.Any()).ToList();
        Assert.All(ordersWithItems, o =>
            Assert.All(o.Items, i => Assert.NotNull(i.Product.Category)));
    }

    // ─── Fingerprint cache hit ────────────────────────────────────────────────────

    [Fact]
    public async Task FingerprintCache_TwoSpecsWithSameShape_BothExecuteCorrectly()
    {
        // Two separate spec instances describing the same include shape.
        // The second call should hit the structural fingerprint cache.
        using var ctx1 = _fixture.CreateContext();
        using var ctx2 = _fixture.CreateContext();

        var spec1 = new OrdersWithFilteredItemsThenProductSpec();
        var spec2 = new OrdersWithFilteredItemsThenProductSpec();

        var result1 = await SpecificationEvaluator.GetQuery(ctx1.Orders, spec1).ToListAsync();
        var result2 = await SpecificationEvaluator.GetQuery(ctx2.Orders, spec2).ToListAsync();

        Assert.Equal(result1.Count, result2.Count);
    }

    [Fact]
    public async Task FingerprintCache_ReusesCompiledDelegateAcrossInstances()
    {
        // Reset cache so the test is deterministic regardless of previous test runs.
        FilteredIncludeApplier.SetCacheCapacityForTesting<Order>(1024);

        using var ctx = _fixture.CreateContext();

        // First spec — primes the cache.
        var spec1 = new OrdersWithFilteredItemsThenProductSpec();
        await SpecificationEvaluator.GetQuery(ctx.Orders, spec1).ToListAsync();
        var countAfterFirst = FilteredIncludeApplier.GetCacheCount<Order>();

        // Second structurally identical spec — must hit the cache, not allocate a new entry.
        var spec2 = new OrdersWithFilteredItemsThenProductSpec();
        await SpecificationEvaluator.GetQuery(ctx.Orders, spec2).ToListAsync();
        var countAfterSecond = FilteredIncludeApplier.GetCacheCount<Order>();

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.True(countAfterFirst >= 1, "First call should have populated the cache.");
    }

    [Fact]
    public async Task FingerprintCache_DistinctShapes_AddSeparateEntries()
    {
        FilteredIncludeApplier.SetCacheCapacityForTesting<Order>(1024);

        using var ctx = _fixture.CreateContext();

        var before = FilteredIncludeApplier.GetCacheCount<Order>();

        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithFilteredItemsThenProductSpec()).ToListAsync();
        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithFilteredItemsProductCategorySpec()).ToListAsync();
        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithItemsQuantityGt1Spec()).ToListAsync();

        var after = FilteredIncludeApplier.GetCacheCount<Order>();
        Assert.Equal(before + 3, after);
    }

    [Fact]
    public async Task FingerprintCache_LRU_EvictsOldEntriesPastCapacity()
    {
        // Cap the cache at 2 so a third distinct shape forces eviction.
        FilteredIncludeApplier.SetCacheCapacityForTesting<Order>(2);

        using var ctx = _fixture.CreateContext();

        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithFilteredItemsThenProductSpec()).ToListAsync();
        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithFilteredItemsProductCategorySpec()).ToListAsync();
        await SpecificationEvaluator.GetQuery(ctx.Orders, new OrdersWithItemsQuantityGt1Spec()).ToListAsync();

        // Cache count must not exceed the capacity.
        Assert.True(FilteredIncludeApplier.GetCacheCount<Order>() <= 2,
            $"Cache size exceeded capacity: {FilteredIncludeApplier.GetCacheCount<Order>()}");

        // Restore for other tests.
        FilteredIncludeApplier.SetCacheCapacityForTesting<Order>(1024);
    }

    // ─── IEnumerable<T> navigation regression (B3 fix) ───────────────────────────

    [Fact]
    public async Task FilteredInclude_IEnumerableNavigation_WorksCorrectly()
    {
        // ProductWithIEnumerableOrderItemsSpec uses IEnumerable<OrderItem> navigation
        // to exercise the fixed TryGetEnumerableElement path.
        using var ctx = _fixture.CreateContext();
        var spec = new ProductWithIEnumerableOrderItemsSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        Assert.NotEmpty(result);
        var productsWithItems = result.Where(p => p.OrderItems.Any()).ToList();
        Assert.All(productsWithItems, p =>
            Assert.All(p.OrderItems, i => Assert.True(i.Quantity > 0)));
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class OrdersWithItemsQuantityGt1Spec : MsSpec<Order>
    {
        public OrdersWithItemsQuantityGt1Spec()
        {
            Include(o => o.Items.Where(i => i.Quantity > 1));
            EnableNoTracking();
        }
    }

    private class OrdersWithItemsQuantityGt100Spec : MsSpec<Order>
    {
        public OrdersWithItemsQuantityGt100Spec()
        {
            Include(o => o.Items.Where(i => i.Quantity > 100));
            EnableNoTracking();
        }
    }

    private class OrdersWithFilteredItemsThenProductSpec : MsSpec<Order>
    {
        public OrdersWithFilteredItemsThenProductSpec()
        {
            Include(o => o.Items.Where(i => i.Quantity > 0))
                .ThenInclude(i => i.Product);
            EnableNoTracking();
        }
    }

    private class OrdersWithFilteredItemsProductCategorySpec : MsSpec<Order>
    {
        public OrdersWithFilteredItemsProductCategorySpec()
        {
            Include(o => o.Items.Where(i => i.Quantity > 0))
                .ThenInclude(i => i.Product)
                .ThenInclude(p => p.Category);
            EnableNoTracking();
        }
    }

    /// <summary>
    /// Uses an IEnumerable&lt;OrderItem&gt;-typed navigation to exercise the B3 fix in
    /// FilteredIncludeApplier.TryGetEnumerableElement.
    /// </summary>
    private class ProductWithIEnumerableOrderItemsSpec : MsSpec<Product>
    {
        public ProductWithIEnumerableOrderItemsSpec()
        {
            Where(p => p.IsActive);
            Include(p => p.OrderItems.Where(i => i.Quantity > 0));
            EnableNoTracking();
        }
    }
}
