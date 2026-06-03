using MsSpecification.Core.Contracts;
using MsSpecification.Sample.Api.Entities;
using MsSpecification.Sample.Api.Specifications;

namespace MsSpecification.Sample.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/recent", async (
            int? days, int? page, int? pageSize,
            ISpecificationRepository<Order> repo) =>
        {
            var spec = new RecentOrdersSpec(days ?? 30, page ?? 1, pageSize ?? 20);
            var orders = await repo.ListAsync(spec);
            return Results.Ok(orders);
        }).WithName("GetRecentOrders");

        group.MapGet("/customer/{email}", async (
            string email,
            ISpecificationRepository<Order> repo) =>
        {
            var orders = await repo.ListAsync(new OrdersByCustomerSpec(email));
            return Results.Ok(orders);
        }).WithName("GetOrdersByCustomer");

        group.MapGet("/{id:int}/including-deleted", async (
            int id,
            ISpecificationRepository<Order> repo) =>
        {
            var order = await repo.FirstOrDefaultAsync(new OrderByIdIncludingDeletedSpec(id));
            return order is null ? Results.NotFound() : Results.Ok(order);
        }).WithName("GetOrderIncludingDeleted");

        group.MapGet("/status/{status}", async (
            OrderStatus status,
            ISpecificationRepository<Order> repo) =>
        {
            var orders = await repo.ListAsync(new OrdersByStatusWithBulkItemsSpec(status));
            return Results.Ok(orders);
        }).WithName("GetOrdersByStatus");

        group.MapGet("/summaries", async (ISpecificationRepository<Order> repo) =>
        {
            var summaries = await repo.ListAsync(new OrderSummarySpec());
            return Results.Ok(summaries);
        }).WithName("GetOrderSummaries");

        group.MapGet("/any-pending", async (ISpecificationRepository<Order> repo) =>
        {
            var spec = new RecentOrdersSpec(days: 365);
            var hasPending = await repo.AnyAsync(spec);
            return Results.Ok(new { HasPendingOrders = hasPending });
        }).WithName("AnyPendingOrders");

        group.MapGet("/raw-sql", async (
            decimal? minAmount,
            ISpecificationRepository<Order> repo) =>
        {
            var spec = new OrdersByRawSqlSpec(minAmount ?? 100m);
            var orders = await repo.ListAsync(spec);
            return Results.Ok(orders);
        }).WithName("GetOrdersByRawSql");

        group.MapPost("/bulk-cancel-old", async (
            ISpecificationRepository<Order> repo) =>
        {
            var spec = new RecentOrdersSpec(days: 365);
            var deleted = await repo.ExecuteDeleteAsync(spec);
            return Results.Ok(new { DeletedCount = deleted });
        }).WithName("BulkCancelOldOrders");
    }
}
