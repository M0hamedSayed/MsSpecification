using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.EntityFrameworkCore;
using MsSpecification.Infra.EF;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace MsSpecification.Benchmarks;

[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.RatioSD)]
public class QueryBenchmarks
{
    private BenchmarkDbContext _db = null!;
    private SpecificationRepository<BenchmarkProduct> _productRepo = null!;
    private SpecificationRepository<BenchmarkOrder> _orderRepo = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new BenchmarkDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureDeleted();
        _db.Database.EnsureCreated();
        SeedBenchmarkData(_db);

        _productRepo = SpecificationRepository<BenchmarkProduct>.ForDbContext(_db);
        _orderRepo = SpecificationRepository<BenchmarkOrder>.ForDbContext(_db);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Simple Filter: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    public async Task<List<BenchmarkProduct>> RawEF_SimpleFilter()
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Price > 10m)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<BenchmarkProduct>> Spec_SimpleFilter()
    {
        return await _productRepo.ListAsync(new SimpleFilterSpec());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Filter + Include: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<List<BenchmarkProduct>> RawEF_FilterWithInclude()
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Category)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<BenchmarkProduct>> Spec_FilterWithInclude()
    {
        return await _productRepo.ListAsync(new FilterWithIncludeSpec());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Pagination: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<List<BenchmarkProduct>> RawEF_Paginated()
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Skip(0)
            .Take(20)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<BenchmarkProduct>> Spec_Paginated()
    {
        return await _productRepo.ListAsync(new PaginatedSpec(1, 20));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Complex Include Chain: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<List<BenchmarkOrder>> RawEF_ComplexInclude()
    {
        return await _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Category)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<BenchmarkOrder>> Spec_ComplexInclude()
    {
        return await _orderRepo.ListAsync(new ComplexIncludeSpec());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Projection: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<List<ProductDto>> RawEF_Projection()
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                CategoryName = p.Category.Name
            })
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<ProductDto>> Spec_Projection()
    {
        return await _productRepo.ListAsync(new ProjectionSpec());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Count: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<int> RawEF_Count()
    {
        return await _db.Products
            .Where(p => p.IsActive && p.Price > 10m)
            .CountAsync();
    }

    [Benchmark]
    public async Task<int> Spec_Count()
    {
        return await _productRepo.CountAsync(new SimpleFilterSpec());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Any: Specification vs Raw EF
    // ─────────────────────────────────────────────────────────────────────────────

    [Benchmark]
    public async Task<bool> RawEF_Any()
    {
        return await _db.Products
            .AnyAsync(p => p.IsActive && p.Price > 10m);
    }

    [Benchmark]
    public async Task<bool> Spec_Any()
    {
        return await _productRepo.AnyAsync(new SimpleFilterSpec());
    }

    private static void SeedBenchmarkData(BenchmarkDbContext db)
    {
        var categories = Enumerable.Range(1, 10)
            .Select(i => new BenchmarkCategory { Name = $"Category {i}" })
            .ToList();
        db.Categories.AddRange(categories);
        db.SaveChanges();

        var random = new Random(42);
        var products = Enumerable.Range(1, 500)
            .Select(i => new BenchmarkProduct
            {
                Name = $"Product {i:D4}",
                Price = random.Next(5, 500),
                Stock = random.Next(0, 200),
                IsActive = random.NextDouble() > 0.1,
                CategoryId = categories[random.Next(categories.Count)].Id
            })
            .ToList();
        db.Products.AddRange(products);
        db.SaveChanges();

        var orders = Enumerable.Range(1, 200)
            .Select(i => new BenchmarkOrder
            {
                CustomerName = $"Customer {i % 50}",
                OrderDate = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                TotalAmount = random.Next(20, 2000)
            })
            .ToList();
        db.Orders.AddRange(orders);
        db.SaveChanges();

        var orderItems = new List<BenchmarkOrderItem>();
        foreach (var order in orders)
        {
            var itemCount = random.Next(1, 5);
            for (int j = 0; j < itemCount; j++)
            {
                var product = products[random.Next(products.Count)];
                orderItems.Add(new BenchmarkOrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = random.Next(1, 10),
                    UnitPrice = product.Price
                });
            }
        }
        db.OrderItems.AddRange(orderItems);
        db.SaveChanges();
    }
}
