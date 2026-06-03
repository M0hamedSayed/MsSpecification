using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MsSpecification.Infra.EF.Tests.Fixtures;

/// <summary>
/// Creates an in-memory SQLite database, seeds test data, and exposes a factory
/// that returns a fresh <see cref="TestDbContext"/> sharing the same connection.
/// Implements <see cref="IDisposable"/> so xUnit collection fixtures clean up correctly.
/// </summary>
public sealed class DbFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CreateAndSeed();
    }

    public TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new TestDbContext(options);
    }

    private void CreateAndSeed()
    {
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        var electronics = new Category { Id = 1, Name = "Electronics" };
        var clothing = new Category { Id = 2, Name = "Clothing" };
        ctx.Categories.AddRange(electronics, clothing);

        var p1 = new Product { Id = 1, Name = "Laptop", Price = 999m, IsActive = true, Stock = 10, Category = electronics };
        var p2 = new Product { Id = 2, Name = "Phone", Price = 499m, IsActive = true, Stock = 25, Category = electronics };
        var p3 = new Product { Id = 3, Name = "T-Shirt", Price = 19m, IsActive = true, Stock = 100, Category = clothing };
        var p4 = new Product { Id = 4, Name = "Old Gadget", Price = 50m, IsActive = false, Stock = 5, Category = electronics };
        ctx.Products.AddRange(p1, p2, p3, p4);

        var o1 = new Order { Id = 1, CustomerEmail = "alice@test.com", OrderDate = DateTime.UtcNow.AddDays(-1), TotalAmount = 999m, IsDeleted = false };
        var o2 = new Order { Id = 2, CustomerEmail = "bob@test.com", OrderDate = DateTime.UtcNow.AddDays(-2), TotalAmount = 499m, IsDeleted = false };
        var o3 = new Order { Id = 3, CustomerEmail = "alice@test.com", OrderDate = DateTime.UtcNow.AddDays(-5), TotalAmount = 19m, IsDeleted = false };
        var o4 = new Order { Id = 4, CustomerEmail = "alice@test.com", OrderDate = DateTime.UtcNow.AddDays(-200), TotalAmount = 100m, IsDeleted = true };
        ctx.Orders.AddRange(o1, o2, o3, o4);

        ctx.OrderItems.AddRange(
            new OrderItem { Id = 1, Order = o1, Product = p1, Quantity = 1 },
            new OrderItem { Id = 2, Order = o2, Product = p2, Quantity = 1 },
            new OrderItem { Id = 3, Order = o3, Product = p3, Quantity = 2 },
            new OrderItem { Id = 4, Order = o4, Product = p1, Quantity = 1 }
        );

        ctx.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
