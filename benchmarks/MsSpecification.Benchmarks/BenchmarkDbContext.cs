using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace MsSpecification.Benchmarks;

public class BenchmarkDbContext : DbContext
{
    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options) { }

    public DbSet<BenchmarkOrder> Orders => Set<BenchmarkOrder>();
    public DbSet<BenchmarkOrderItem> OrderItems => Set<BenchmarkOrderItem>();
    public DbSet<BenchmarkProduct> Products => Set<BenchmarkProduct>();
    public DbSet<BenchmarkCategory> Categories => Set<BenchmarkCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenchmarkOrder>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<BenchmarkOrder>().Property(o => o.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<BenchmarkOrderItem>().Property(oi => oi.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<BenchmarkProduct>().Property(p => p.Price).HasPrecision(18, 2);
    }
}

public class BenchmarkCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<BenchmarkProduct> Products { get; set; } = new List<BenchmarkProduct>();
}

public class BenchmarkProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public int CategoryId { get; set; }
    public BenchmarkCategory Category { get; set; } = null!;
    public ICollection<BenchmarkOrderItem> OrderItems { get; set; } = new List<BenchmarkOrderItem>();
}

public class BenchmarkOrder
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<BenchmarkOrderItem> Items { get; set; } = new List<BenchmarkOrderItem>();
}

public class BenchmarkOrderItem
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int OrderId { get; set; }
    public BenchmarkOrder Order { get; set; } = null!;
    public int ProductId { get; set; }
    public BenchmarkProduct Product { get; set; } = null!;
}
