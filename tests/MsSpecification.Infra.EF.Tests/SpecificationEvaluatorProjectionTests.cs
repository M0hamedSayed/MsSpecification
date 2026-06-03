using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class SpecificationEvaluatorProjectionTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Selector_ProjectsToDto()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductNamePriceSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        Assert.NotEmpty(result);
        Assert.All(result, dto =>
        {
            Assert.True(dto.Id > 0);
            Assert.False(string.IsNullOrEmpty(dto.Name));
            Assert.True(dto.Price > 0);
        });
    }

    [Fact]
    public async Task Selector_WithCriteria_FiltersBeforeProjection()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductNamePriceSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        // Only 3 active products seeded
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Selector_MissingSelector_ThrowsInvalidOperation()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new MissingSelectorSpec();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync());
    }

    [Fact]
    public async Task Distinct_OnProjection_DedupesProjectedRows()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new DistinctCategoryIdSpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        // 4 products across 2 categories — DISTINCT must apply to the projected CategoryId,
        // yielding 2 rows (not 4). This proves DISTINCT runs after the Select projection.
        Assert.Equal(2, result.Count);
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public async Task GroupBy_AggregatesToCorrectResult()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductCountByCategorySpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        // 2 categories seeded: Electronics (3 products), Clothing (1 product)
        Assert.Equal(2, result.Count);
        var electronics = result.First(r => r.CategoryId == 1);
        Assert.Equal(3, electronics.ProductCount);
        var clothing = result.First(r => r.CategoryId == 2);
        Assert.Equal(1, clothing.ProductCount);
    }

    [Fact]
    public async Task GroupBy_WithCriteria_FiltersBeforeGrouping()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductCountByCategorySpec();
        var result = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        // Active products: Laptop(1), Phone(1), T-Shirt(1) — Old Gadget excluded
        // Electronics: 2 active (Laptop, Phone), Clothing: 1 active (T-Shirt)
        Assert.Equal(2, result.Count);
        var electronics = result.First(r => r.CategoryId == 1);
        Assert.Equal(2, electronics.ProductCount);
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private record ProductDto(int Id, string Name, decimal Price);

    private class ProductNamePriceSpec : MsSpec<Product, ProductDto>
    {
        public ProductNamePriceSpec()
        {
            Select(p => new ProductDto(p.Id, p.Name, p.Price));
            EnableNoTracking();
        }
    }

    private class ActiveProductNamePriceSpec : MsSpec<Product, ProductDto>
    {
        public ActiveProductNamePriceSpec()
        {
            Where(p => p.IsActive);
            Select(p => new ProductDto(p.Id, p.Name, p.Price));
            EnableNoTracking();
        }
    }

    private class MissingSelectorSpec : MsSpec<Product, ProductDto>
    {
        // Intentionally no Select or GroupBy — should throw
    }

    private class DistinctCategoryIdSpec : MsSpec<Product, int>
    {
        public DistinctCategoryIdSpec()
        {
            Select(p => p.CategoryId);
            EnableDistinct();
            EnableNoTracking();
        }
    }

    private record CategoryProductCount(int CategoryId, int ProductCount);

    private class ProductCountByCategorySpec : MsSpec<Product, CategoryProductCount>
    {
        public ProductCountByCategorySpec()
        {
            GroupBy(
                p => p.CategoryId,
                g => new CategoryProductCount(g.Key, g.Count()));
            EnableNoTracking();
        }
    }

    private class ActiveProductCountByCategorySpec : MsSpec<Product, CategoryProductCount>
    {
        public ActiveProductCountByCategorySpec()
        {
            Where(p => p.IsActive);
            GroupBy(
                p => p.CategoryId,
                g => new CategoryProductCount(g.Key, g.Count()));
            EnableNoTracking();
        }
    }
}
