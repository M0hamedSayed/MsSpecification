using System.Linq.Expressions;
using MsSpecification.Core.Builder;
using Xunit;

namespace MsSpecification.Core.Tests;

public class ExpressionNameCacheTests
{
    private class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public TestChild? Child { get; set; }
    }

    private class TestChild
    {
        public string Label { get; set; } = string.Empty;
    }

    [Fact]
    public void SimpleMember_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, string>> expr = e => e.Name;
        var name = ExpressionNameCache.GetPropertyName(expr);
        Assert.Equal("Name", name);
    }

    [Fact]
    public void SimpleMember_IntProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, int>> expr = e => e.Age;
        var name = ExpressionNameCache.GetPropertyName(expr);
        Assert.Equal("Age", name);
    }

    [Fact]
    public void NestedMember_ReturnsDotPath()
    {
        Expression<Func<TestEntity, string>> expr = e => e.Child!.Label;
        var name = ExpressionNameCache.GetPropertyName(expr);
        Assert.Equal("Child.Label", name);
    }

    [Fact]
    public void FilteredWhere_DetectedAsFiltered()
    {
        Expression<Func<TestEntity, string>> expr = e => e.Name;
        var isFiltered = ExpressionNameCache.IsFilteredExpression(expr);
        // Simple member access without Where/OrderBy is not filtered
        Assert.False(isFiltered);
    }

    [Fact]
    public void SimpleMember_NotFiltered()
    {
        Expression<Func<TestEntity, string>> expr = e => e.Name;
        var isFiltered = ExpressionNameCache.IsFilteredExpression(expr);
        Assert.False(isFiltered);
    }
}
