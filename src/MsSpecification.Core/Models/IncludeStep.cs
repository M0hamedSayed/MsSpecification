using System.Linq.Expressions;

namespace MsSpecification.Core.Models;
/// <summary>
/// Represents a single step in a navigation include chain.
/// Defined as a readonly struct to avoid heap allocation per step.
/// </summary>
public readonly struct IncludeStep
{
    /// <summary>The lambda expression representing the navigation path</summary>
    public readonly LambdaExpression NavigationPropertyPath;

    /// <summary>The entity type being navigated from</summary>
    public readonly Type FromType;

    /// <summary>The property type being navigated to (may be a collection type)</summary>
    public readonly Type ToType;

    /// <summary>Whether this navigation is a collection (IEnumerable, ICollection, etc.)</summary>
    public readonly bool IsCollection;

    /// <summary>The property name extracted from the expression</summary>
    public readonly string PropertyName;

    /// <summary>
    /// True when the expression contains a filtering operation (Where/OrderBy/Take/Skip etc.)
    /// rather than a plain member access. Filtered steps CANNOT be expressed as a dot-notation
    /// string path and require EF Core's strongly-typed Include/ThenInclude fluent API.
    /// </summary>
    public readonly bool IsFiltered;

    public IncludeStep(
        LambdaExpression navigationPropertyPath,
        Type fromType,
        Type toType,
        bool isCollection,
        string propertyName,
        bool isFiltered = false)
    {
        NavigationPropertyPath = navigationPropertyPath;
        FromType = fromType;
        ToType = toType;
        IsCollection = isCollection;
        PropertyName = propertyName;
        IsFiltered = isFiltered;
    }

    public override string ToString() =>
        $"{FromType.Name} -> {PropertyName} ({ToType.Name})" +
        $"{(IsCollection ? " [Collection]" : "")}" +
        $"{(IsFiltered ? " [Filtered]" : "")}";
}