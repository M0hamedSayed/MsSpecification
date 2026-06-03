using MsSpecification.Core.Contracts;
using MsSpecification.Core.Extensions;
using Xunit;

namespace MsSpecification.Core.Tests;

public class SpecificationExtensionsTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private class ActiveSpec : MsSpec<TestEntity>
    {
        public ActiveSpec() => Where(e => e.IsActive);
    }

    private class NamedSpec : MsSpec<TestEntity>
    {
        public NamedSpec() => Where(e => e.Name == "Test");
    }

    private class NoTrackingSpec : MsSpec<TestEntity>
    {
        public NoTrackingSpec()
        {
            Where(e => e.IsActive);
            EnableNoTracking();
        }
    }

    private class OrderedSpec : MsSpec<TestEntity>
    {
        public OrderedSpec()
        {
            Where(e => e.IsActive);
            OrderByAscending(e => e.Name);
        }
    }

    private class PagedSpec : MsSpec<TestEntity>
    {
        public PagedSpec()
        {
            Where(e => e.IsActive);
            Paginate(10, 20);
        }
    }

    private class TaggedSpec : MsSpec<TestEntity>
    {
        public TaggedSpec()
        {
            Where(e => e.IsActive);
            TagWith("left-tag");
        }
    }

    private class RightTaggedSpec : MsSpec<TestEntity>
    {
        public RightTaggedSpec()
        {
            Where(e => e.Name == "Test");
            TagWith("right-tag");
        }
    }

    private class RawSqlSpec : MsSpec<TestEntity>
    {
        public RawSqlSpec()
        {
            FromSqlRaw("SELECT * FROM Entities");
        }
    }

    private class AnotherRawSqlSpec : MsSpec<TestEntity>
    {
        public AnotherRawSqlSpec()
        {
            FromSqlRaw("SELECT * FROM OtherEntities");
        }
    }

    [Fact]
    public void And_MergesCriteria()
    {
        var left = new ActiveSpec();
        var right = new NamedSpec();
        var combined = left.And(right);

        Assert.NotNull(combined.Criteria);
        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Name = "Test" }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Name = "Other" }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Name = "Test" }));
    }

    [Fact]
    public void Or_MergesCriteria()
    {
        var left = new ActiveSpec();
        var right = new NamedSpec();
        var combined = left.Or(right);

        Assert.NotNull(combined.Criteria);
        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Name = "Other" }));
        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Name = "Test" }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Name = "Other" }));
    }

    [Fact]
    public void And_FlagsUseOrLogic_NoTracking()
    {
        var left = new NoTrackingSpec();
        var right = new ActiveSpec();
        var combined = left.And(right);

        Assert.True(combined.AsNoTracking);
    }

    [Fact]
    public void And_OrderingUsesLeft()
    {
        var left = new OrderedSpec();
        var right = new ActiveSpec();
        var combined = left.And(right);

        Assert.NotNull(combined.OrderBy);
    }

    [Fact]
    public void And_PaginationUsesLeft()
    {
        var left = new PagedSpec();
        var right = new ActiveSpec();
        var combined = left.And(right);

        Assert.Equal(10, combined.Skip);
        Assert.Equal(20, combined.Take);
    }

    [Fact]
    public void And_BothWithRawSql_UsesLeft()
    {
        var left = new RawSqlSpec();
        var right = new AnotherRawSqlSpec();
        var combined = left.And(right);

        Assert.Equal("SELECT * FROM Entities", combined.RawSql);
    }

    [Fact]
    public void Or_QueryTag_LeftPreferredOverRight()
    {
        var left = new TaggedSpec();
        var right = new RightTaggedSpec();
        var combined = left.Or(right);

        Assert.Equal("left-tag", combined.QueryTag);
    }

    [Fact]
    public void And_WithNullCriteriaLeft_UsesRightCriteria()
    {
        ISpecification<TestEntity> left = new EmptySpec();
        var right = new ActiveSpec();
        var combined = left.And(right);

        Assert.Same(right.Criteria, combined.Criteria);
    }

    [Fact]
    public void And_WithNullCriteriaRight_UsesLeftCriteria()
    {
        var left = new ActiveSpec();
        ISpecification<TestEntity> right = new EmptySpec();
        var combined = left.And(right);

        Assert.Same(left.Criteria, combined.Criteria);
    }

    [Fact]
    public void And_BothNullCriteria_ResultHasNullCriteria()
    {
        ISpecification<TestEntity> left = new EmptySpec();
        ISpecification<TestEntity> right = new EmptySpec();
        var combined = left.And(right);

        Assert.Null(combined.Criteria);
    }

    private class EmptySpec : MsSpec<TestEntity>
    {
        // No criteria
    }
}
