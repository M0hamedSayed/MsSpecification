using MsSpecification.Core.Contracts;
using Xunit;

namespace MsSpecification.Core.Tests;

public class CriteriaCompositionTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
    }

    [Fact]
    public void And_ParameterRebinding_ProducesCorrectExpression()
    {
        var spec = new AndCombinedSpec();
        var entity = new TestEntity { IsActive = true, Price = 100 };
        Assert.True(spec.IsSatisfiedBy(entity));
    }

    private class AndCombinedSpec : MsSpec<TestEntity>
    {
        public AndCombinedSpec()
        {
            Where(e => e.IsActive);
            And(e => e.Price > 50);
        }
    }

    [Fact]
    public void Or_ParameterRebinding_ProducesCorrectExpression()
    {
        var spec = new OrCombinedSpec();
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10 }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 10 }));
    }

    private class OrCombinedSpec : MsSpec<TestEntity>
    {
        public OrCombinedSpec()
        {
            Where(e => e.IsActive);
            Or(e => e.Price > 50);
        }
    }

    [Fact]
    public void NestedAndOr_CombinesCorrectly()
    {
        var spec = new NestedAndOrSpec();
        // (IsActive AND Price > 50) OR (Name == "Special")
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100, Name = "Normal" }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 10, Name = "Special" }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10, Name = "Normal" }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 10, Name = "Normal" }));
    }

    private class NestedAndOrSpec : MsSpec<TestEntity>
    {
        public NestedAndOrSpec()
        {
            Where(e => e.IsActive);
            And(e => e.Price > 50);
            Or(e => e.Name == "Special");
        }
    }

    [Fact]
    public void CombinedWithNot_NegatesEntireExpression()
    {
        var spec = new NotCombinedSpec();
        // NOT (IsActive AND Price > 50)
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100 }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10 }));
    }

    private class NotCombinedSpec : MsSpec<TestEntity>
    {
        public NotCombinedSpec()
        {
            Where(e => e.IsActive);
            And(e => e.Price > 50);
            Not();
        }
    }

    [Fact]
    public void DoubleNegation_ReturnsToOriginal()
    {
        var spec = new DoubleNotSpec();
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true }));
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false }));
    }

    private class DoubleNotSpec : MsSpec<TestEntity>
    {
        public DoubleNotSpec()
        {
            Where(e => e.IsActive);
            Not();
            Not();
        }
    }
}
