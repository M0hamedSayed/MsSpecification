using MsSpecification.Core.Contracts;
using MsSpecification.Core.Models;
using Xunit;

namespace MsSpecification.Core.Tests;

public class SpecificationBuilderTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }

    private class ActiveTestEntitySpec : MsSpec<TestEntity>
    {
        public ActiveTestEntitySpec() => Where(e => e.IsActive);
    }

    private class NamedTestEntitySpec : MsSpec<TestEntity>
    {
        public NamedTestEntitySpec(string name) => Where(e => e.Name == name);
    }

    [Fact]
    public void Where_SetsCriteria()
    {
        var spec = new ActiveTestEntitySpec();
        Assert.NotNull(spec.Criteria);
    }

    [Fact]
    public void And_WithNoCriteria_SetsAsCriteria()
    {
        var spec = new AndWithNoExistingSpec();
        Assert.NotNull(spec.Criteria);
    }

    private class AndWithNoExistingSpec : MsSpec<TestEntity>
    {
        public AndWithNoExistingSpec() => And(e => e.IsActive);
    }

    [Fact]
    public void And_WithExistingCriteria_CombinesWithAndAlso()
    {
        var spec = new AndCombinedSpec();
        Assert.NotNull(spec.Criteria);
        // Verify the combined expression works correctly via IsSatisfiedBy
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100 }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 50 }));
    }

    private class AndCombinedSpec : MsSpec<TestEntity>
    {
        public AndCombinedSpec()
        {
            Where(e => e.IsActive);
            And(e => e.Price > 75);
        }
    }

    [Fact]
    public void Or_WithExistingCriteria_CombinesWithOrElse()
    {
        var spec = new OrCombinedSpec();
        Assert.NotNull(spec.Criteria);
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10 }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 10 }));
    }

    private class OrCombinedSpec : MsSpec<TestEntity>
    {
        public OrCombinedSpec()
        {
            Where(e => e.IsActive);
            Or(e => e.Price > 75);
        }
    }

    [Fact]
    public void Not_WithNoCriteria_DoesNothing()
    {
        var spec = new NotWithNoCriteriaSpec();
        Assert.Null(spec.Criteria);
    }

    private class NotWithNoCriteriaSpec : MsSpec<TestEntity>
    {
        public NotWithNoCriteriaSpec() => Not();
    }

    [Fact]
    public void Not_WithExistingCriteria_Negates()
    {
        var spec = new NotSpec();
        Assert.NotNull(spec.Criteria);
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = false }));
    }

    private class NotSpec : MsSpec<TestEntity>
    {
        public NotSpec()
        {
            Where(e => e.IsActive);
            Not();
        }
    }

    [Fact]
    public void MultipleAnd_CombinesAll()
    {
        var spec = new MultipleAndSpec();
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100, Name = "Test" }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100, Name = "Other" }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 50, Name = "Test" }));
    }

    private class MultipleAndSpec : MsSpec<TestEntity>
    {
        public MultipleAndSpec()
        {
            Where(e => e.IsActive);
            And(e => e.Price > 75);
            And(e => e.Name == "Test");
        }
    }

    [Fact]
    public void OrderByAscending_SetsPrimary()
    {
        var spec = new OrderAscSpec();
        Assert.NotNull(spec.OrderBy);
        Assert.False(spec.OrderBy.Value.Descending);
    }

    private class OrderAscSpec : MsSpec<TestEntity>
    {
        public OrderAscSpec() => OrderByAscending(e => e.Name);
    }

    [Fact]
    public void OrderByDescending_ClearsThenOrderBys()
    {
        var spec = new OrderDescWithThenSpec();
        Assert.NotNull(spec.OrderBy);
        Assert.True(spec.OrderBy.Value.Descending);
        Assert.Single(spec.ThenOrderBys);
    }

    private class OrderDescWithThenSpec : MsSpec<TestEntity>
    {
        public OrderDescWithThenSpec()
        {
            OrderByAscending(e => e.Name);
            ThenByAscending(e => e.Price);
            // Setting a new primary clears then-bys
            OrderByDescending(e => e.Price);
            ThenByDescending(e => e.Name);
        }
    }

    [Fact]
    public void Paginate_NegativeSkip_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BadPaginateSkipSpec());
    }

    private class BadPaginateSkipSpec : MsSpec<TestEntity>
    {
        public BadPaginateSkipSpec() => Paginate(-1, 10);
    }

    [Fact]
    public void Paginate_ZeroTake_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BadPaginateTakeSpec());
    }

    private class BadPaginateTakeSpec : MsSpec<TestEntity>
    {
        public BadPaginateTakeSpec() => Paginate(0, 0);
    }

    [Fact]
    public void Page_LessThanOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BadPageSpec());
    }

    private class BadPageSpec : MsSpec<TestEntity>
    {
        public BadPageSpec() => Page(0, 10);
    }

    [Fact]
    public void Page_OneIndexed_CalculatesCorrectSkip()
    {
        var spec = new PageSpec(2, 25);
        Assert.Equal(25, spec.Skip);
        Assert.Equal(25, spec.Take);
    }

    private class PageSpec : MsSpec<TestEntity>
    {
        public PageSpec(int pageNumber, int pageSize) => Page(pageNumber, pageSize);
    }

    [Fact]
    public void EnableNoTracking_SetsFlag()
    {
        var spec = new NoTrackingSpec();
        Assert.True(spec.AsNoTracking);
    }

    private class NoTrackingSpec : MsSpec<TestEntity>
    {
        public NoTrackingSpec() => EnableNoTracking();
    }

    [Fact]
    public void EnableStreaming_SetsFlag()
    {
        var spec = new StreamingSpec();
        Assert.True(spec.AsStreaming);
    }

    private class StreamingSpec : MsSpec<TestEntity>
    {
        public StreamingSpec() => EnableStreaming();
    }

    [Fact]
    public void EnableDistinct_SetsFlag()
    {
        var spec = new DistinctSpec();
        Assert.True(spec.IsDistinct);
    }

    private class DistinctSpec : MsSpec<TestEntity>
    {
        public DistinctSpec() => EnableDistinct();
    }

    [Fact]
    public void EnableIgnoreQueryFilters_SetsFlag()
    {
        var spec = new IgnoreFiltersSpec();
        Assert.True(spec.IgnoreQueryFilters);
    }

    private class IgnoreFiltersSpec : MsSpec<TestEntity>
    {
        public IgnoreFiltersSpec() => EnableIgnoreQueryFilters();
    }

    [Fact]
    public void TagWith_SetsQueryTag()
    {
        var spec = new TaggedSpec();
        Assert.Equal("my-tag", spec.QueryTag);
    }

    private class TaggedSpec : MsSpec<TestEntity>
    {
        public TaggedSpec() => TagWith("my-tag");
    }

    [Fact]
    public void FromSqlRaw_SetsRawSql()
    {
        var spec = new RawSqlSpec();
        Assert.Equal("SELECT * FROM Entities", spec.RawSql);
        Assert.NotNull(spec.RawSqlParameters);
    }

    private class RawSqlSpec : MsSpec<TestEntity>
    {
        public RawSqlSpec() => FromSqlRaw("SELECT * FROM Entities");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var spec = new ActiveTestEntitySpec();
        Assert.Null(spec.OrderBy);
        Assert.Empty(spec.ThenOrderBys);
        Assert.Empty(spec.Includes);
        Assert.Empty(spec.IncludeStrings);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
        Assert.False(spec.AsNoTracking);
        Assert.False(spec.AsNoTrackingWithIdentityResolution);
        Assert.False(spec.AsSplitQuery);
        Assert.False(spec.AsStreaming);
        Assert.Null(spec.IdentitySelector);
        Assert.False(spec.IsDistinct);
        Assert.Null(spec.QueryTag);
        Assert.False(spec.IgnoreQueryFilters);
        Assert.Null(spec.RawSql);
        Assert.Empty(spec.RawSqlParameters);
    }

    // ─── Select / GroupBy mutual exclusion (v2.0 fix) ──────────────────────────

    private class SelectThenGroupBySpec : MsSpec<TestEntity, decimal>
    {
        public SelectThenGroupBySpec()
        {
            Select(e => e.Price);
            // Should throw because Select was already configured.
            GroupBy(e => e.IsActive, g => g.Sum(x => x.Price));
        }
    }

    private class GroupByThenSelectSpec : MsSpec<TestEntity, decimal>
    {
        public GroupByThenSelectSpec()
        {
            GroupBy(e => e.IsActive, g => g.Sum(x => x.Price));
            // Should throw because GroupBy was already configured.
            Select(e => e.Price);
        }
    }

    [Fact]
    public void Select_AfterGroupBy_Throws()
    {
        // GroupBy is set first, then Select is called — Select() detects GroupByApplier and throws.
        var ex = Assert.Throws<InvalidOperationException>(() => new GroupByThenSelectSpec());
        Assert.Contains("Select cannot be combined with GroupBy", ex.Message);
    }

    [Fact]
    public void GroupBy_AfterSelect_Throws()
    {
        // Select is set first, then GroupBy is called — GroupBy() detects Selector and throws.
        var ex = Assert.Throws<InvalidOperationException>(() => new SelectThenGroupBySpec());
        Assert.Contains("GroupBy cannot be combined with Select", ex.Message);
    }
}
