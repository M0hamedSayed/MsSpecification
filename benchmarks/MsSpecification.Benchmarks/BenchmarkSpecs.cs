using MsSpecification.Core.Contracts;

namespace MsSpecification.Benchmarks;

public class SimpleFilterSpec : MsSpec<BenchmarkProduct>
{
    public SimpleFilterSpec()
    {
        Where(p => p.IsActive && p.Price > 10m);
        EnableNoTracking();
    }
}

public class FilterWithIncludeSpec : MsSpec<BenchmarkProduct>
{
    public FilterWithIncludeSpec()
    {
        Where(p => p.IsActive);
        Include(p => p.Category);
        EnableNoTracking();
    }
}

public class PaginatedSpec : MsSpec<BenchmarkProduct>
{
    public PaginatedSpec(int page, int pageSize)
    {
        Where(p => p.IsActive);
        OrderByAscending(p => p.Name);
        Page(page, pageSize);
        EnableNoTracking();
    }
}

public class ComplexIncludeSpec : MsSpec<BenchmarkOrder>
{
    public ComplexIncludeSpec()
    {
        Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category);
        OrderByDescending(o => o.OrderDate);
        EnableNoTracking();
        EnableSplitQuery();
    }
}

public class ProjectionSpec : MsSpec<BenchmarkProduct, ProductDto>
{
    public ProjectionSpec()
    {
        Where(p => p.IsActive);
        Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            CategoryName = p.Category.Name
        });
        EnableNoTracking();
    }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
