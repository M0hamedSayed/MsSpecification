using Microsoft.EntityFrameworkCore;
using MsSpecification.Core.Contracts;
using MsSpecification.Infra.EF.Tests.Fixtures;
using Xunit;

namespace MsSpecification.Infra.EF.Tests;

/// <summary>
/// For each canonical specification, materialize results via SQL and via in-memory
/// <see cref="MsSpec{T}.IsSatisfiedBy"/> and confirm both agree on the matching set.
/// This catches the class of bug where the SQL evaluator returns X but
/// <c>IsSatisfiedBy</c> returns Y because CLR semantics diverge from SQL — null
/// propagation, comparison rules, etc.
/// </summary>
public class IsSatisfiedByParityTests : IDisposable
{
    private readonly DbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static void AssertParity<T>(IEnumerable<T> all, MsSpec<T> spec, List<T> sqlMatches)
        where T : class
    {
        var inMemoryMatches = all.Where(e => spec.IsSatisfiedBy(e)).ToList();

        var sqlIds = sqlMatches.Cast<object>().Select(GetId).OrderBy(x => x).ToList();
        var memIds = inMemoryMatches.Cast<object>().Select(GetId).OrderBy(x => x).ToList();
        Assert.Equal(sqlIds, memIds);
    }

    private static int GetId(object entity) => entity switch
    {
        Product p => p.Id,
        Order o => o.Id,
        Category c => c.Id,
        OrderItem oi => oi.Id,
        _ => -1,
    };

    [Fact]
    public async Task ActivePriceRangeSpec_AgreesWithSql()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new ActiveInPriceRangeSpec(50m, 1000m);

        var allInDb = await ctx.Products.ToListAsync();
        var sqlMatches = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        AssertParity(allInDb, spec, sqlMatches);
    }

    [Fact]
    public async Task NameContainsSpec_AgreesWithSql_OnCaseInsensitiveData()
    {
        using var ctx = _fixture.CreateContext();
        var spec = new NameContainsSpec("Lap");

        var allInDb = await ctx.Products.ToListAsync();
        var sqlMatches = await SpecificationEvaluator.GetQuery(ctx.Products, spec).ToListAsync();

        AssertParity(allInDb, spec, sqlMatches);
    }

    [Fact]
    public async Task OrderByCustomerEmailSpec_AgreesWithSql_OnPredicate()
    {
        // Test only the predicate parity (IsSatisfiedBy doesn't consider ordering/pagination).
        using var ctx = _fixture.CreateContext();
        var spec = new OrdersByCustomerSpec("alice@test.com");

        var allInDb = await ctx.Orders.ToListAsync();
        var sqlMatches = await SpecificationEvaluator.GetQuery(ctx.Orders, spec).ToListAsync();

        AssertParity(allInDb, spec, sqlMatches);
    }

    [Fact]
    public async Task CombinedAndSpec_AgreesWithSql()
    {
        using var ctx = _fixture.CreateContext();

        var left = new ActiveInPriceRangeSpec(0m, 100m);
        var right = new NameContainsSpec("T");
        var combined = (MsSpec<Product>)Activator.CreateInstance(
            typeof(CombinedActiveAndNameSpec),
            new object[] { 0m, 100m, "T" })!;

        var allInDb = await ctx.Products.ToListAsync();
        var sqlMatches = await SpecificationEvaluator.GetQuery(ctx.Products, combined).ToListAsync();

        AssertParity(allInDb, combined, sqlMatches);
    }

    // ─── Spec definitions ────────────────────────────────────────────────────────

    private class ActiveInPriceRangeSpec : MsSpec<Product>
    {
        public ActiveInPriceRangeSpec(decimal min, decimal max)
        {
            Where(p => p.IsActive);
            And(p => p.Price >= min);
            And(p => p.Price <= max);
        }
    }

    private class NameContainsSpec : MsSpec<Product>
    {
        public NameContainsSpec(string fragment)
            => Where(p => p.Name.Contains(fragment));
    }

    private class OrdersByCustomerSpec : MsSpec<Order>
    {
        public OrdersByCustomerSpec(string email)
            => Where(o => o.CustomerEmail == email);
    }

    private class CombinedActiveAndNameSpec : MsSpec<Product>
    {
        public CombinedActiveAndNameSpec(decimal min, decimal max, string fragment)
        {
            Where(p => p.IsActive);
            And(p => p.Price >= min);
            And(p => p.Price <= max);
            And(p => p.Name.Contains(fragment));
        }
    }
}
