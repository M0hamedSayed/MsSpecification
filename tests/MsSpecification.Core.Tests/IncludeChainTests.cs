using System.Buffers;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Models;
using Xunit;

namespace MsSpecification.Core.Tests;

public class IncludeChainTests
{
    [Fact]
    public void AddStep_IncrementsCount()
    {
        var chain = new IncludeChain<TestEntity>();
        Assert.Equal(0, chain.Count);

        chain.AddStep(CreateStep("Name"));
        Assert.Equal(1, chain.Count);

        chain.AddStep(CreateStep("Price"));
        Assert.Equal(2, chain.Count);
    }

    [Fact]
    public void Steps_ReturnsCorrectSpan()
    {
        var chain = new IncludeChain<TestEntity>();
        chain.AddStep(CreateStep("Name"));
        chain.AddStep(CreateStep("Price"));

        var steps = chain.Steps;
        Assert.Equal(2, steps.Length);
        Assert.Equal("Name", steps[0].PropertyName);
        Assert.Equal("Price", steps[1].PropertyName);
    }

    [Fact]
    public void Grow_DoublesCapacity()
    {
        var chain = new IncludeChain<TestEntity>(initialCapacity: 2);
        chain.AddStep(CreateStep("A"));
        chain.AddStep(CreateStep("B"));
        // At capacity, next AddStep triggers Grow
        chain.AddStep(CreateStep("C"));

        Assert.Equal(3, chain.Count);
        var steps = chain.Steps;
        Assert.Equal("A", steps[0].PropertyName);
        Assert.Equal("B", steps[1].PropertyName);
        Assert.Equal("C", steps[2].PropertyName);
    }

    [Fact]
    public void Return_PreventsUseAfterFree()
    {
        var chain = new IncludeChain<TestEntity>();
        chain.AddStep(CreateStep("Name"));

        chain.Return();

        Assert.Throws<InvalidOperationException>(() => { _ = chain.Steps; });
        Assert.Throws<InvalidOperationException>(() => { _ = chain.RootStep; });
        Assert.Throws<InvalidOperationException>(() => chain.AddStep(CreateStep("Price")));
    }

    [Fact]
    public void Return_CalledTwice_DoesNotThrow()
    {
        var chain = new IncludeChain<TestEntity>();
        chain.AddStep(CreateStep("Name"));

        chain.Return();
        chain.Return(); // Should not throw
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var chain = new IncludeChain<TestEntity>();
        chain.AddStep(CreateStep("Category"));
        chain.AddStep(CreateStep("Products"));

        Assert.Equal("Category -> Products", chain.ToString());
    }

    [Fact]
    public void ToString_EmptyChain_ReturnsEmpty()
    {
        var chain = new IncludeChain<TestEntity>();
        Assert.Equal("(empty)", chain.ToString());
    }

    private static IncludeStep CreateStep(string propertyName)
    {
        return new IncludeStep(
            navigationPropertyPath: null!,
            fromType: typeof(TestEntity),
            toType: typeof(object),
            isCollection: false,
            propertyName: propertyName,
            isFiltered: false);
    }

    private class TestEntity
    {
        public int Id { get; set; }
    }
}
