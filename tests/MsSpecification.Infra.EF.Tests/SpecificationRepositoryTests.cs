using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class SpecificationRepositoryTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private SpecificationRepository<Product> ProductRepo()
    {
        var ctx = _fixture.CreateContext();
        return SpecificationRepository<Product>.ForDbContext(ctx);
    }

    private SpecificationRepository<Order> OrderRepo()
    {
        var ctx = _fixture.CreateContext();
        return SpecificationRepository<Order>.ForDbContext(ctx);
    }

    // ─── ListAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsAllMatching()
    {
        var repo = ProductRepo();
        var result = await repo.ListAsync(new ActiveProductsSpec());
        Assert.Equal(3, result.Count);
        Assert.All(result, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task ListAsync_NoCriteria_ReturnsAll()
    {
        var repo = ProductRepo();
        var result = await repo.ListAsync(new AllProductsSpec());
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task ListAsync_WithAsStreaming_Throws()
    {
        var repo = ProductRepo();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.ListAsync(new StreamingProductsSpec()));
    }

    // ─── ListAsync<TResult> ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_Projection_ReturnsDtos()
    {
        var repo = ProductRepo();
        var result = await repo.ListAsync(new ProductSummarySpec());
        Assert.Equal(3, result.Count);
        Assert.All(result, dto => Assert.False(string.IsNullOrEmpty(dto.Name)));
    }

    [Fact]
    public async Task ListAsync_Projection_WithAsStreaming_Throws()
    {
        var repo = ProductRepo();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.ListAsync(new StreamingProductSummarySpec()));
    }

    // ─── FirstOrDefaultAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefaultAsync_MatchFound_ReturnsEntity()
    {
        var repo = ProductRepo();
        var result = await repo.FirstOrDefaultAsync(new ProductByIdSpec(1));
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var repo = ProductRepo();
        var result = await repo.FirstOrDefaultAsync(new ProductByIdSpec(999));
        Assert.Null(result);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithAsStreaming_Throws()
    {
        var repo = ProductRepo();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.FirstOrDefaultAsync(new StreamingProductsSpec()));
    }

    // ─── SingleOrDefaultAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SingleOrDefaultAsync_UniqueMatch_ReturnsEntity()
    {
        var repo = ProductRepo();
        var result = await repo.SingleOrDefaultAsync(new ProductByIdSpec(2));
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var repo = ProductRepo();
        var result = await repo.SingleOrDefaultAsync(new ProductByIdSpec(999));
        Assert.Null(result);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_WithAsStreaming_Throws()
    {
        var repo = ProductRepo();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.SingleOrDefaultAsync(new StreamingProductsSpec()));
    }

    // ─── CountAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        var repo = ProductRepo();
        var count = await repo.CountAsync(new ActiveProductsSpec());
        Assert.Equal(3, count);
    }

    // ─── AnyAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnyAsync_Match_ReturnsTrue()
    {
        var repo = ProductRepo();
        var any = await repo.AnyAsync(new ActiveProductsSpec());
        Assert.True(any);
    }

    [Fact]
    public async Task AnyAsync_NoMatch_ReturnsFalse()
    {
        var repo = ProductRepo();
        var any = await repo.AnyAsync(new ProductByIdSpec(999));
        Assert.False(any);
    }

    // ─── ExecuteDeleteAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteDeleteAsync_DeletesMatchingRows()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);

        var deleted = await repo.ExecuteDeleteAsync(new InactiveProductsSpec());

        Assert.Equal(1, deleted);

        // Verify gone from DB
        var count = await ctx.Products.CountAsync(p => !p.IsActive);
        Assert.Equal(0, count);
    }

    // ─── GetQuery (raw IQueryable) ────────────────────────────────────────────────

    [Fact]
    public async Task GetQuery_ReturnsComposableQueryable()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);
        var query = repo.GetQuery(new ActiveProductsSpec());
        var result = await query.Where(p => p.Price > 100).ToListAsync();
        Assert.All(result, p => Assert.True(p.Price > 100m));
    }

    // ─── Streaming guard (v2.0 fix) — applies to obsolete CountAsync<TResult> too ────

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CS0618", Justification = "Testing the obsolete API still guards streaming.")]
    public async Task CountAsync_OfTResult_WithStreaming_Throws()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
#pragma warning disable CS0618
            () => repo.CountAsync(new StreamingProjectionSpec())
#pragma warning restore CS0618
        );
        Assert.Contains("AsStreaming", ex.Message);
    }

    // ─── FromSqlRaw + Skip/Take/OrderBy composition (D3 coverage gap) ────────────

    [Fact]
    public async Task FromSqlRaw_WithPaginationAndOrderBy_ComposesCorrectly()
    {
        using var ctx = _fixture.CreateContext();
        var repo = SpecificationRepository<Product>.ForDbContext(ctx);

        var spec = new RawSqlPaginatedProductsSpec();
        var page = await repo.ListAsync(spec);

        Assert.Equal(2, page.Count); // Take(2)
        // Ordered by Id ASC, skip 1: first row is the second product (Id=2), second is Id=3.
        Assert.True(page[0].Id < page[1].Id);
        Assert.Equal(2, page[0].Id);
        Assert.Equal(3, page[1].Id);
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class ActiveProductsSpec : MsSpec<Product>
    {
        public ActiveProductsSpec() => Where(p => p.IsActive);
    }

    private class AllProductsSpec : MsSpec<Product> { }

    private class ProductByIdSpec : MsSpec<Product>
    {
        public ProductByIdSpec(int id) => Where(p => p.Id == id);
    }

    private class InactiveProductsSpec : MsSpec<Product>
    {
        public InactiveProductsSpec() => Where(p => !p.IsActive);
    }

    private class StreamingProductsSpec : MsSpec<Product>
    {
        public StreamingProductsSpec() => EnableStreaming();
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

    private class StreamingProductSummarySpec : MsSpec<Product, ProductDto>
    {
        public StreamingProductSummarySpec()
        {
            Where(p => p.IsActive);
            Select(p => new ProductDto(p.Name, p.Price));
            EnableStreaming();
        }
    }

    private class StreamingProjectionSpec : MsSpec<Product, ProductDto>
    {
        public StreamingProjectionSpec()
        {
            Select(p => new ProductDto(p.Name, p.Price));
            EnableStreaming();
        }
    }

    private class RawSqlPaginatedProductsSpec : MsSpec<Product>
    {
        public RawSqlPaginatedProductsSpec()
        {
            FromSqlRaw("SELECT * FROM Products");
            // OrderBy by Id (int) — SQLite+EF8 doesn't translate ORDER BY on decimal columns.
            OrderByAscending(p => p.Id);
            Paginate(skip: 1, take: 2);
            EnableNoTracking();
        }
    }
}
