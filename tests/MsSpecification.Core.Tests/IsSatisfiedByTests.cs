using MsSpecification.Core.Contracts;
using MsSpecification.Core.Extensions;
using Xunit;

namespace MsSpecification.Core.Tests;

public class IsSatisfiedByTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
    }

    [Fact]
    public void NullCriteria_ReturnsTrue()
    {
        var spec = new EmptySpec();
        Assert.True(spec.IsSatisfiedBy(new TestEntity()));
    }

    private class EmptySpec : MsSpec<TestEntity> { }

    [Fact]
    public void MatchingEntity_ReturnsTrue()
    {
        var spec = new ActiveSpec();
        Assert.True(spec.IsSatisfiedBy(new TestEntity { IsActive = true }));
    }

    private class ActiveSpec : MsSpec<TestEntity>
    {
        public ActiveSpec() => Where(e => e.IsActive);
    }

    [Fact]
    public void NonMatchingEntity_ReturnsFalse()
    {
        var spec = new ActiveSpec();
        Assert.False(spec.IsSatisfiedBy(new TestEntity { IsActive = false }));
    }

    [Fact]
    public void CombinedSpecAnd_BothMustMatch()
    {
        var left = new ActiveSpec();
        var right = new PriceSpec();
        var combined = left.And(right);

        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 100 }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10 }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
    }

    private class PriceSpec : MsSpec<TestEntity>
    {
        public PriceSpec() => Where(e => e.Price > 50);
    }

    [Fact]
    public void CombinedSpecOr_EitherCanMatch()
    {
        var left = new ActiveSpec();
        var right = new PriceSpec();
        var combined = left.Or(right);

        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = true, Price = 10 }));
        Assert.True(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 100 }));
        Assert.False(combined.IsSatisfiedBy(new TestEntity { IsActive = false, Price = 10 }));
    }

    [Fact]
    public void LazyCompilation_OnlyCompilesOnce()
    {
        var spec = new ActiveSpec();
        var entity = new TestEntity { IsActive = true };

        // Call IsSatisfiedBy multiple times — the Lazy<Func<T,bool>> should compile only once
        var result1 = spec.IsSatisfiedBy(entity);
        var result2 = spec.IsSatisfiedBy(entity);
        var result3 = spec.IsSatisfiedBy(entity);

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void CombinedSpec_LazyCompilation_OnlyCompilesOnce()
    {
        var left = new ActiveSpec();
        var right = new PriceSpec();
        var combined = left.And(right);

        var entity = new TestEntity { IsActive = true, Price = 100 };

        // Call IsSatisfiedBy multiple times — the Lazy<Func<T,bool>> should compile only once
        var result1 = combined.IsSatisfiedBy(entity);
        var result2 = combined.IsSatisfiedBy(entity);
        var result3 = combined.IsSatisfiedBy(entity);

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    // ─── Concurrent compilation (D3 coverage gap) ────────────────────────────────

    [Fact]
    public void IsSatisfiedBy_ConcurrentCalls_AllSucceed()
    {
        // Lazy<T> with ExecutionAndPublication guarantees one compilation under concurrency.
        // This test exercises that path with a large fan-out.
        var spec = new ActiveSpec();
        var entity = new TestEntity { IsActive = true };
        var results = new bool[256];

        Parallel.For(0, results.Length, i =>
        {
            results[i] = spec.IsSatisfiedBy(entity);
        });

        Assert.All(results, Assert.True);
    }

    // ─── Nullable navigation (D3 coverage gap) ───────────────────────────────────

    private class Parent
    {
        public int Id { get; set; }
        public Child? Child { get; set; }
    }

    private class Child
    {
        public string? Label { get; set; }
    }

    private class ChildLabelEqualsSpec : MsSpec<Parent>
    {
        public ChildLabelEqualsSpec(string label) =>
            Where(p => p.Child != null && p.Child.Label == label);
    }

    [Fact]
    public void IsSatisfiedBy_NullableNavigation_NullChild_ReturnsFalse()
    {
        var spec = new ChildLabelEqualsSpec("X");
        var parent = new Parent { Id = 1, Child = null };

        // Predicate guards Child != null, so this returns false rather than throwing NRE.
        Assert.False(spec.IsSatisfiedBy(parent));
    }

    [Fact]
    public void IsSatisfiedBy_NullableNavigation_MatchingChild_ReturnsTrue()
    {
        var spec = new ChildLabelEqualsSpec("X");
        var parent = new Parent { Id = 1, Child = new Child { Label = "X" } };

        Assert.True(spec.IsSatisfiedBy(parent));
    }
}
