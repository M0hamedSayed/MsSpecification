using MsSpecification.Core.Contracts;
using MsSpecification.Sample.Api.Entities;

namespace MsSpecification.Sample.Api.Specifications;

/// <summary>Active products with category eager-loaded, sorted by name.</summary>
public class ActiveProductsSpec : MsSpec<Product>
{
    public ActiveProductsSpec()
    {
        Where(p => p.IsActive);
        Include(p => p.Category);
        OrderByAscending(p => p.Name);
        EnableNoTracking();
        TagWith("ActiveProducts");
    }
}

/// <summary>Products filtered by category with pagination.</summary>
public class ProductsByCategorySpec : MsSpec<Product>
{
    public ProductsByCategorySpec(int categoryId, int page = 1, int pageSize = 10)
    {
        Where(p => p.CategoryId == categoryId && p.IsActive);
        Include(p => p.Category);
        Include(p => p.ProductTags);
        OrderByDescending(p => p.CreatedAt);
        Page(page, pageSize);
        EnableNoTracking();
        EnableSplitQuery();
    }
}

/// <summary>Products within a price range using And/Or criteria composition.</summary>
public class ProductsByPriceRangeSpec : MsSpec<Product>
{
    public ProductsByPriceRangeSpec(decimal minPrice, decimal maxPrice)
    {
        Where(p => p.IsActive);
        And(p => p.Price >= minPrice);
        And(p => p.Price <= maxPrice);
        OrderByAscending(p => p.Price);
        EnableNoTracking();
    }
}

/// <summary>Search products by name or description (OR criteria).</summary>
public class ProductSearchSpec : MsSpec<Product>
{
    public ProductSearchSpec(string searchTerm)
    {
        var term = searchTerm.ToLower();
        Where(p => p.Name.ToLower().Contains(term));
        Or(p => p.Description != null && p.Description.ToLower().Contains(term));
        And(p => p.IsActive);
        Include(p => p.Category);
        OrderByAscending(p => p.Name);
        EnableNoTracking();
    }
}

/// <summary>Product with all navigations deeply loaded (filtered include demo).</summary>
public class ProductWithDetailsSpec : MsSpec<Product>
{
    public ProductWithDetailsSpec(int productId)
    {
        Where(p => p.Id == productId);
        Include(p => p.Category);
        Include(p => p.ProductTags);
        Include(p => p.OrderItems).ThenInclude(oi => oi.Order);
        EnableNoTrackingWithIdentityResolution();
    }
}

/// <summary>Distinct category names of active products (distinct demo).</summary>
public class DistinctProductCategoriesSpec : MsSpec<Product>
{
    public DistinctProductCategoriesSpec()
    {
        Where(p => p.IsActive);
        Include(p => p.Category);
        EnableDistinct();
        EnableNoTracking();
    }
}

/// <summary>Projection specification: Project Product to a lightweight DTO.</summary>
public class ProductSummarySpec : MsSpec<Product, ProductSummaryDto>
{
    public ProductSummarySpec()
    {
        Where(p => p.IsActive);
        Select(p => new ProductSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            CategoryName = p.Category.Name,
            Stock = p.Stock,
            TagCount = p.ProductTags.Count
        });
        OrderByDescending(p => p.Price);
        EnableNoTracking();
    }
}

public class ProductSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int TagCount { get; set; }
}

/// <summary>Low-stock products for restocking alerts.</summary>
public class LowStockProductsSpec : MsSpec<Product>
{
    public LowStockProductsSpec(int threshold = 50)
    {
        Where(p => p.IsActive && p.Stock < threshold);
        Include(p => p.Category);
        OrderByAscending(p => p.Stock);
        EnableNoTracking();
    }
}

/// <summary>Inactive products targeted for bulk deletion.</summary>
public class InactiveProductsSpec : MsSpec<Product>
{
    public InactiveProductsSpec()
    {
        Where(p => !p.IsActive);
        TagWith("InactiveProducts.BulkDelete");
    }
}

/// <summary>Streamable enumeration of all active products — uses IAsyncEnumerable, no buffering.</summary>
public class StreamableActiveProductsSpec : MsSpec<Product>
{
    public StreamableActiveProductsSpec()
    {
        Where(p => p.IsActive);
        OrderByAscending(p => p.Id);
        EnableStreaming();
        TagWith("ActiveProducts.Stream");
    }
}
