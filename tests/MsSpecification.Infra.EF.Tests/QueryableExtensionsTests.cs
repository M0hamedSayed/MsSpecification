using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Extensions;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class QueryableExtensionsTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task WithSpecification_AppliesAllClauses()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductsOrderedSpec();

        var result = await ctx.Products
            .WithSpecification(spec)
            .ToListAsync();

        Assert.Equal(3, result.Count);
        Assert.All(result, p => Assert.True(p.IsActive));
        for (var i = 1; i < result.Count; i++)
            Assert.True(result[i].Id >= result[i - 1].Id);
    }

    [Fact]
    public async Task WithSpecification_AllowsFurtherComposition()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductsOrderedSpec();

        var result = await ctx.Products
            .WithSpecification(spec)
            .Where(p => p.Stock > 10)
            .ToListAsync();

        Assert.All(result, p => Assert.True(p.Stock > 10));
    }

    [Fact]
    public async Task WithCriteria_AppliesOnlyWhere()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveProductsOrderedSpec();

        // WithCriteria should filter but not include Category or apply ordering
        var result = await ctx.Products
            .WithCriteria(spec)
            .CountAsync();

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task WithSpecification_Projection_ProjectsToTResult()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ProductSummarySpec();

        var result = await ctx.Products
            .WithSpecification(spec)
            .ToListAsync();

        Assert.Equal(3, result.Count);
        Assert.All(result, dto => Assert.False(string.IsNullOrEmpty(dto.Name)));
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class ActiveProductsOrderedSpec : MsSpec<Product>
    {
        public ActiveProductsOrderedSpec()
        {
            Where(p => p.IsActive);
            Include(p => p.Category);
            OrderByAscending(p => p.Id);
            EnableNoTracking();
        }
    }

    private record ProductDto(string Name, decimal Price);

    private class ProductSummarySpec : MsSpec<Product, ProductDto>
    {
        public ProductSummarySpec()
        {
            Where(p => p.IsActive);
            Select(p => new ProductDto(p.Name, p.Price));
        }
    }
}
