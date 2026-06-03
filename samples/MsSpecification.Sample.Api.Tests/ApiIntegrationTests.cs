using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MsSpecification.Sample.Api.Entities;
using MsSpecification.Sample.Api.Specifications;
using Xunit;

namespace MsSpecification.Sample.Api.Tests;

public sealed class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    [Fact]
    public async Task GetActiveProducts_ReturnsOnlyActiveWithCategories()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products", JsonOptions);

        Assert.NotNull(products);
        Assert.Equal(9, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.IsActive);
            Assert.NotNull(p.Category);
            Assert.False(string.IsNullOrEmpty(p.Category.Name));
        });
        Assert.Equal(
            products.Select(p => p.Name).OrderBy(n => n),
            products.Select(p => p.Name));
    }

    [Fact]
    public async Task GetProductsByCategory_ReturnsPagedShapeAndConsistentTotal()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedProductsResponse>(
            "/api/products/category/1?page=1&pageSize=5",
            JsonOptions);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.PageSize);
        Assert.True(page.TotalCount >= page.Items.Count);
        Assert.All(page.Items, p => Assert.Equal(1, p.CategoryId));
        Assert.All(page.Items, p => Assert.NotNull(p.ProductTags));
    }

    [Fact]
    public async Task GetProductsByPriceRange_ReturnsProductsInRange()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>(
            "/api/products/price-range?min=40&max=100",
            JsonOptions);

        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.InRange(p.Price, 40m, 100m);
            Assert.True(p.IsActive);
        });
    }

    [Fact]
    public async Task SearchProducts_FindsByTerm()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>(
            "/api/products/search?q=design",
            JsonOptions);

        Assert.NotNull(products);
        Assert.Contains(products, p =>
            p.Name.Contains("Design", StringComparison.OrdinalIgnoreCase)
            || (p.Description?.Contains("Design", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [Fact]
    public async Task GetProductDetails_ExistingId_ReturnsGraph()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var product = await client.GetFromJsonAsync<Product>("/api/products/1/details", JsonOptions);

        Assert.NotNull(product);
        Assert.Equal(1, product.Id);
        Assert.NotNull(product.Category);
        Assert.NotEmpty(product.ProductTags);
        Assert.NotEmpty(product.OrderItems);
    }

    [Fact]
    public async Task GetProductDetails_MissingId_Returns404()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products/99999/details");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductSummaries_ReturnsProjectionDtos()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<ProductSummaryDto>>("/api/products/summaries", JsonOptions);

        Assert.NotNull(rows);
        Assert.Equal(9, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.True(r.Id > 0);
            Assert.False(string.IsNullOrEmpty(r.Name));
            Assert.False(string.IsNullOrEmpty(r.CategoryName));
            Assert.True(r.Price > 0);
            Assert.True(r.TagCount >= 0);
        });
    }

    [Fact]
    public async Task GetLowStockProducts_RespectsThreshold()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>(
            "/api/products/low-stock?threshold=100",
            JsonOptions);

        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.True(p.Stock < 100);
            Assert.True(p.IsActive);
        });
    }

    [Fact]
    public async Task GetCombinedSpec_ReturnsIntersectionOfSpecs()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products/combined", JsonOptions);

        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.True(p.IsActive);
            Assert.InRange(p.Price, 40m, 100m);
        });
    }

    [Fact]
    public async Task GetRecentOrders_ReturnsOrdersWithItemsAndProducts()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var orders = await client.GetFromJsonAsync<List<Order>>(
            "/api/orders/recent?days=30&page=1&pageSize=10",
            JsonOptions);

        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        Assert.All(orders, o =>
        {
            Assert.False(o.IsDeleted);
            Assert.NotEmpty(o.Items);
            Assert.All(o.Items, i =>
            {
                Assert.NotNull(i.Product);
                Assert.True(i.Quantity > 0);
            });
        });
    }

    [Fact]
    public async Task GetOrdersByCustomer_ReturnsOnlyThatEmail()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var orders = await client.GetFromJsonAsync<List<Order>>(
            "/api/orders/customer/alice%40example.com",
            JsonOptions);

        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        Assert.All(orders, o =>
        {
            Assert.Equal("alice@example.com", o.CustomerEmail, StringComparer.OrdinalIgnoreCase);
            Assert.NotNull(o.Items);
            Assert.All(o.Items, i => Assert.NotNull(i.Product.Category));
        });
    }

    [Fact]
    public async Task GetOrderIncludingDeleted_DeletedRow_Returns200()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var order = await client.GetFromJsonAsync<Order>("/api/orders/5/including-deleted", JsonOptions);

        Assert.NotNull(order);
        Assert.Equal(5, order.Id);
        Assert.True(order.IsDeleted);
    }

    [Fact]
    public async Task GetOrderIncludingDeleted_Missing_Returns404()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders/99999/including-deleted");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrdersByStatus_ReturnsOnlyThatStatus()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var orders = await client.GetFromJsonAsync<List<Order>>(
            "/api/orders/status/Delivered",
            JsonOptions);

        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.Equal(OrderStatus.Delivered, o.Status));
    }

    [Fact]
    public async Task GetOrderSummaries_ReturnsProjectionRows()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<OrderSummaryDto>>("/api/orders/summaries", JsonOptions);

        Assert.NotNull(rows);
        Assert.NotEmpty(rows);
        Assert.All(rows, r =>
        {
            Assert.True(r.Id > 0);
            Assert.False(string.IsNullOrEmpty(r.CustomerName));
            Assert.False(string.IsNullOrEmpty(r.Status));
            Assert.True(r.ItemCount >= 0);
        });
    }

    [Fact]
    public async Task AnyPendingOrders_ReturnsBooleanPayload()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<AnyPendingResponse>("/api/orders/any-pending", JsonOptions);

        Assert.NotNull(payload);
        Assert.True(payload.HasPendingOrders);
    }

    [Fact]
    public async Task GetOrdersByRawSql_ReturnsFilteredOrders()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var orders = await client.GetFromJsonAsync<List<Order>>(
            "/api/orders/raw-sql?minAmount=100",
            JsonOptions);

        Assert.NotNull(orders);
        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.True(o.TotalAmount >= 100m));
    }

    [Fact]
    public async Task BulkCancelOldOrders_DeletesMatchingRows()
    {
        using var factory = new SampleApiWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/orders/bulk-cancel-old", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BulkCancelResponse>(JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload.DeletedCount >= 4);

        var remaining = await client.GetFromJsonAsync<List<Order>>(
            "/api/orders/recent?days=365&page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(remaining);
        Assert.Empty(remaining);
    }

    public sealed class PagedProductsResponse
    {
        public List<Product> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public sealed record AnyPendingResponse(bool HasPendingOrders);

    public sealed record BulkCancelResponse(int DeletedCount);
}
