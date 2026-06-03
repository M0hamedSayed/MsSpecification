using MsSpecification.Core.Builder;
using Xunit;

namespace MsSpecification.Core.Tests;

public class TypeMetadataCacheTests
{
    [Fact]
    public void String_IsNotCollection()
    {
        Assert.False(TypeMetadataCache<string, string>.IsCollection);
    }

    [Fact]
    public void List_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, List<int>>.IsCollection);
    }

    [Fact]
    public void ICollection_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, ICollection<int>>.IsCollection);
    }

    [Fact]
    public void IEnumerable_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, IEnumerable<int>>.IsCollection);
    }

    [Fact]
    public void HashSet_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, HashSet<int>>.IsCollection);
    }

    [Fact]
    public void IReadOnlyList_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, IReadOnlyList<int>>.IsCollection);
    }

    [Fact]
    public void IReadOnlyCollection_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, IReadOnlyCollection<int>>.IsCollection);
    }

    [Fact]
    public void ReferenceType_IsNotCollection()
    {
        Assert.False(TypeMetadataCache<object, string>.IsCollection);
    }

    [Fact]
    public void Int_IsNotCollection()
    {
        Assert.False(TypeMetadataCache<object, int>.IsCollection);
    }

    [Fact]
    public void Array_IsCollection()
    {
        Assert.True(TypeMetadataCache<object, int[]>.IsCollection);
    }

    [Fact]
    public void FromType_IsCorrect()
    {
        Assert.Equal(typeof(object), TypeMetadataCache<object, string>.FromType);
    }

    [Fact]
    public void ToType_IsCorrect()
    {
        Assert.Equal(typeof(string), TypeMetadataCache<object, string>.ToType);
    }
}
