using MsSpecification.Core.Contracts;
using MsSpecification.Sample.Api.Entities;

namespace MsSpecification.Sample.Api.Specifications;

/// <summary>Recent orders with items and products loaded.</summary>
public class RecentOrdersSpec : MsSpec<Order>
{
    public RecentOrdersSpec(int days = 7, int page = 1, int pageSize = 20)
    {
        Where(o => o.OrderDate >= DateTime.UtcNow.AddDays(-days));
        Include(o => o.Items).ThenInclude(i => i.Product);
        OrderByDescending(o => o.OrderDate);
        Page(page, pageSize);
        EnableNoTracking();
        EnableSplitQuery();
        TagWith("RecentOrders");
    }
}

/// <summary>Orders by customer email.</summary>
public class OrdersByCustomerSpec : MsSpec<Order>
{
    public OrdersByCustomerSpec(string email)
    {
        Where(o => o.CustomerEmail == email);
        Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Category);
        OrderByDescending(o => o.OrderDate);
        EnableNoTracking();
    }
}

/// <summary>Order by ID with full details (bypasses soft-delete filter).</summary>
public class OrderByIdIncludingDeletedSpec : MsSpec<Order>
{
    public OrderByIdIncludingDeletedSpec(int orderId)
    {
        Where(o => o.Id == orderId);
        Include(o => o.Items).ThenInclude(i => i.Product);
        EnableIgnoreQueryFilters();
        EnableNoTracking();
    }
}

/// <summary>Orders by status with line items and products loaded (split-query friendly).</summary>
public class OrdersByStatusWithBulkItemsSpec : MsSpec<Order>
{
    public OrdersByStatusWithBulkItemsSpec(OrderStatus status)
    {
        Where(o => o.Status == status);
        Include(o => o.Items).ThenInclude(i => i.Product);
        OrderByDescending(o => o.TotalAmount);
        EnableNoTracking();
    }
}

/// <summary>Projection: Order summary DTO.</summary>
public class OrderSummarySpec : MsSpec<Order, OrderSummaryDto>
{
    public OrderSummarySpec()
    {
        Select(o => new OrderSummaryDto
        {
            Id = o.Id,
            CustomerName = o.CustomerName,
            OrderDate = o.OrderDate,
            TotalAmount = o.TotalAmount,
            Status = o.Status.ToString(),
            ItemCount = o.Items.Count
        });
        OrderByDescending(o => o.OrderDate);
        EnableNoTracking();
    }
}

public class OrderSummaryDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

/// <summary>Raw SQL specification: get orders via a raw query.</summary>
public class OrdersByRawSqlSpec : MsSpec<Order>
{
    public OrdersByRawSqlSpec(decimal minAmount)
    {
        FromSqlRaw("SELECT * FROM Orders WHERE TotalAmount >= {0} AND IsDeleted = 0", minAmount);
        // Composed LINQ Where ensures filtering even if the provider composes oddly on FromSql + OrderBy.
        Where(o => o.TotalAmount >= minAmount);
        OrderByDescending(o => o.TotalAmount);
        EnableNoTracking();
    }
}
