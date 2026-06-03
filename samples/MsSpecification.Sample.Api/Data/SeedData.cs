using MsSpecification.Sample.Api.Entities;

namespace MsSpecification.Sample.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(SampleDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Categories.AnyAsync())
            return;

        var electronics = new Category { Name = "Electronics", Description = "Electronic devices and gadgets" };
        var books = new Category { Name = "Books", Description = "Physical and digital books" };
        var clothing = new Category { Name = "Clothing", Description = "Apparel and accessories", IsActive = false };

        db.Categories.AddRange(electronics, books, clothing);
        await db.SaveChangesAsync();

        var products = new[]
        {
            new Product { Name = "Laptop Pro 16", Description = "High-end laptop", Price = 2499.99m, Stock = 50, Category = electronics, CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new Product { Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 49.99m, Stock = 200, Category = electronics, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new Product { Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 149.99m, Stock = 150, Category = electronics, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            new Product { Name = "USB-C Hub", Description = "7-in-1 USB-C hub", Price = 79.99m, Stock = 100, Category = electronics, CreatedAt = DateTime.UtcNow.AddDays(-15) },
            new Product { Name = "Monitor Stand", Description = "Adjustable monitor stand", Price = 39.99m, Stock = 300, Category = electronics, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new Product { Name = "C# in Depth", Description = "Advanced C# programming", Price = 44.99m, Stock = 80, Category = books, CreatedAt = DateTime.UtcNow.AddDays(-28) },
            new Product { Name = "Clean Architecture", Description = "Software architecture guide", Price = 39.99m, Stock = 60, Category = books, CreatedAt = DateTime.UtcNow.AddDays(-22) },
            new Product { Name = "Domain-Driven Design", Description = "DDD fundamentals", Price = 54.99m, Stock = 40, Category = books, CreatedAt = DateTime.UtcNow.AddDays(-18) },
            new Product { Name = "Design Patterns", Description = "GoF design patterns", Price = 49.99m, Stock = 70, Category = books, CreatedAt = DateTime.UtcNow.AddDays(-12) },
            new Product { Name = "Winter Jacket", Description = "Warm winter jacket", Price = 199.99m, Stock = 25, Category = clothing, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-35) },
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var tags = new[]
        {
            new ProductTag { Product = products[0], Tag = "premium" },
            new ProductTag { Product = products[0], Tag = "laptop" },
            new ProductTag { Product = products[1], Tag = "peripheral" },
            new ProductTag { Product = products[2], Tag = "peripheral" },
            new ProductTag { Product = products[2], Tag = "premium" },
            new ProductTag { Product = products[5], Tag = "bestseller" },
            new ProductTag { Product = products[6], Tag = "bestseller" },
            new ProductTag { Product = products[7], Tag = "bestseller" },
        };

        db.ProductTags.AddRange(tags);
        await db.SaveChangesAsync();

        var orders = new[]
        {
            new Order { CustomerName = "Alice", CustomerEmail = "alice@example.com", OrderDate = DateTime.UtcNow.AddDays(-5), Status = OrderStatus.Delivered, TotalAmount = 2549.98m },
            new Order { CustomerName = "Bob", CustomerEmail = "bob@example.com", OrderDate = DateTime.UtcNow.AddDays(-3), Status = OrderStatus.Shipped, TotalAmount = 199.98m },
            new Order { CustomerName = "Charlie", CustomerEmail = "charlie@example.com", OrderDate = DateTime.UtcNow.AddDays(-1), Status = OrderStatus.Pending, TotalAmount = 94.98m },
            new Order { CustomerName = "Alice", CustomerEmail = "alice@example.com", OrderDate = DateTime.UtcNow, Status = OrderStatus.Processing, TotalAmount = 149.99m },
            new Order { CustomerName = "Deleted Order", CustomerEmail = "test@example.com", IsDeleted = true, TotalAmount = 0 },
        };

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();

        var orderItems = new[]
        {
            new OrderItem { Order = orders[0], Product = products[0], Quantity = 1, UnitPrice = 2499.99m },
            new OrderItem { Order = orders[0], Product = products[1], Quantity = 1, UnitPrice = 49.99m },
            new OrderItem { Order = orders[1], Product = products[2], Quantity = 1, UnitPrice = 149.99m },
            new OrderItem { Order = orders[1], Product = products[1], Quantity = 1, UnitPrice = 49.99m },
            new OrderItem { Order = orders[2], Product = products[5], Quantity = 1, UnitPrice = 44.99m },
            new OrderItem { Order = orders[2], Product = products[8], Quantity = 1, UnitPrice = 49.99m },
            new OrderItem { Order = orders[3], Product = products[2], Quantity = 1, UnitPrice = 149.99m },
        };

        db.OrderItems.AddRange(orderItems);
        await db.SaveChangesAsync();
    }

    private static Task<bool> AnyAsync(this IQueryable<Category> source)
        => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(source);
}
