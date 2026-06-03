using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

public class StreamingSpecificationRepositoryTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private SpecificationRepository<Product> ProductRepo(out TestDbContext ctx)
    {
        ctx = _fixture.CreateContext();
        return SpecificationRepository<Product>.ForDbContext(ctx);
    }

    // ─── StreamAsync (entity) ────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_YieldsAllMatchingEntities()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            var results = new List<Product>();
            await foreach (var product in repo.StreamAsync(new ActiveProductsSpec()))
                results.Add(product);

            Assert.Equal(3, results.Count);
            Assert.All(results, p => Assert.True(p.IsActive));
        }
    }

    [Fact]
    public async Task StreamAsync_RespectsOrdering()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            var results = new List<Product>();
            await foreach (var p in repo.StreamAsync(new ProductsOrderByIdAscSpec()))
                results.Add(p);

            for (var i = 1; i < results.Count; i++)
                Assert.True(results[i].Id >= results[i - 1].Id);
        }
    }

    [Fact]
    public async Task StreamAsync_WithAsStreaming_AutoNoTracking()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            var results = new List<Product>();
            await foreach (var p in repo.StreamAsync(new StreamingActiveProductsSpec()))
                results.Add(p);

            Assert.NotEmpty(results);
            Assert.All(results, p =>
                Assert.Equal(EntityState.Detached, ctx.Entry(p).State));
        }
    }

    // ─── StreamAsync<TResult> ────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_Projection_YieldsDtos()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            var results = new List<ProductDto>();
            await foreach (var dto in repo.StreamAsync(new ProductSummarySpec()))
                results.Add(dto);

            Assert.Equal(3, results.Count);
            Assert.All(results, dto =>
            {
                Assert.False(string.IsNullOrEmpty(dto.Name));
                Assert.True(dto.Price > 0);
            });
        }
    }

    [Fact]
    public async Task StreamAsync_Projection_OrderedDescending_IdsDescending()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            var results = new List<ProductDto>();
            await foreach (var dto in repo.StreamAsync(new ProductSummaryDescIdSpec()))
                results.Add(dto);

            Assert.NotEmpty(results);
            for (var i = 1; i < results.Count; i++)
                Assert.True(results[i].Id <= results[i - 1].Id);
        }
    }

    // ─── Cancellation forwarding (v2.0 fix) ──────────────────────────────────────

    [Fact]
    public async Task StreamAsync_RespectsCancellationToken()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // already cancelled before enumeration starts

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in repo.StreamAsync(new ActiveProductsSpec(), cts.Token))
                {
                    // never reached
                }
            });
        }
    }

    [Fact]
    public async Task StreamAsync_Projection_RespectsCancellationToken()
    {
        var repo = ProductRepo(out var ctx);
        using (ctx)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in repo.StreamAsync(new ProductSummaryDescIdSpec(), cts.Token))
                {
                    // never reached
                }
            });
        }
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class ActiveProductsSpec : MsSpec<Product>
    {
        public ActiveProductsSpec() => Where(p => p.IsActive);
    }

    private class ProductsOrderByIdAscSpec : MsSpec<Product>
    {
        public ProductsOrderByIdAscSpec() => OrderByAscending(p => p.Id);
    }

    private class StreamingActiveProductsSpec : MsSpec<Product>
    {
        public StreamingActiveProductsSpec()
        {
            Where(p => p.IsActive);
            EnableStreaming();
        }
    }

    private record ProductDto(int Id, string Name, decimal Price);

    private class ProductSummarySpec : MsSpec<Product, ProductDto>
    {
        public ProductSummarySpec()
        {
            Where(p => p.IsActive);
            Select(p => new ProductDto(p.Id, p.Name, p.Price));
        }
    }

    private class ProductSummaryDescIdSpec : MsSpec<Product, ProductDto>
    {
        public ProductSummaryDescIdSpec()
        {
            Where(p => p.IsActive);
            Select(p => new ProductDto(p.Id, p.Name, p.Price));
            OrderByDescending(p => p.Id);
        }
    }
}
