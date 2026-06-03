using MsSpecification.Core.Contracts;
using MsSpecification.Core.Extensions;
using MsSpecification.Sample.Api.Entities;
using MsSpecification.Sample.Api.Specifications;

namespace MsSpecification.Sample.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (ISpecificationRepository<Product> repo) =>
        {
            var products = await repo.ListAsync(new ActiveProductsSpec());
            return Results.Ok(products);
        }).WithName("GetActiveProducts");

        group.MapGet("/category/{categoryId:int}", async (
            int categoryId, int page, int pageSize,
            ISpecificationRepository<Product> repo) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;
            var spec = new ProductsByCategorySpec(categoryId, page, pageSize);
            var products = await repo.ListAsync(spec);
            var count = await repo.CountAsync(spec);
            return Results.Ok(new { Items = products, TotalCount = count, Page = page, PageSize = pageSize });
        }).WithName("GetProductsByCategory");

        group.MapGet("/price-range", async (
            decimal min, decimal max,
            ISpecificationRepository<Product> repo) =>
        {
            var products = await repo.ListAsync(new ProductsByPriceRangeSpec(min, max));
            return Results.Ok(products);
        }).WithName("GetProductsByPriceRange");

        group.MapGet("/search", async (
            string q,
            ISpecificationRepository<Product> repo) =>
        {
            var products = await repo.ListAsync(new ProductSearchSpec(q));
            return Results.Ok(products);
        }).WithName("SearchProducts");

        group.MapGet("/{id:int}/details", async (
            int id,
            ISpecificationRepository<Product> repo) =>
        {
            var product = await repo.FirstOrDefaultAsync(new ProductWithDetailsSpec(id));
            return product is null ? Results.NotFound() : Results.Ok(product);
        }).WithName("GetProductDetails");

        group.MapGet("/summaries", async (ISpecificationRepository<Product> repo) =>
        {
            var summaries = await repo.ListAsync(new ProductSummarySpec());
            return Results.Ok(summaries);
        }).WithName("GetProductSummaries");

        group.MapGet("/low-stock", async (
            int? threshold,
            ISpecificationRepository<Product> repo) =>
        {
            var products = await repo.ListAsync(new LowStockProductsSpec(threshold ?? 50));
            return Results.Ok(products);
        }).WithName("GetLowStockProducts");

        group.MapGet("/combined", async (ISpecificationRepository<Product> repo) =>
        {
            var active = new ActiveProductsSpec();
            var priceRange = new ProductsByPriceRangeSpec(40m, 100m);
            var combined = active.And(priceRange);
            var products = await repo.ListAsync(combined);
            return Results.Ok(products);
        }).WithName("GetCombinedSpec");

        // Streaming endpoint: returns IAsyncEnumerable<Product> — ASP.NET serializes incrementally.
        group.MapGet("/stream", (IStreamingSpecificationRepository<Product> streamingRepo, CancellationToken ct) =>
            streamingRepo.StreamAsync(new StreamableActiveProductsSpec(), ct))
            .WithName("StreamActiveProducts");

        // Bulk-delete endpoint: removes all inactive products server-side with one SQL statement.
        group.MapDelete("/inactive", async (ISpecificationRepository<Product> repo, CancellationToken ct) =>
        {
            var deleted = await repo.ExecuteDeleteAsync(new InactiveProductsSpec(), ct);
            return Results.Ok(new { DeletedCount = deleted });
        }).WithName("DeleteInactiveProducts");
    }
}
