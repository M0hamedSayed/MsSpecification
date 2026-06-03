namespace MsSpecification.Core.Builder;

/// <summary>
/// Provides zero-cost metadata lookups via C# generic specialization.
/// The JIT generates a distinct static class per (TFrom, TProperty) pair,
/// so all metadata is resolved once at class-initialization time and then
/// accessed as a simple static field read — equivalent to a constant.
/// </summary>
internal static class TypeMetadataCache<TFrom, TProperty>
{
    /// <summary>Source type — cached to avoid typeof() overhead in hot paths</summary>
    public static readonly Type FromType = typeof(TFrom);

    /// <summary>Destination property type</summary>
    public static readonly Type ToType = typeof(TProperty);

    /// <summary>
    /// Whether TProperty is a collection navigation.
    /// Evaluated once per type pair — never again.
    /// </summary>
    public static readonly bool IsCollection = CollectionDetector<TProperty>.IsCollection;
}

/// <summary>
/// Detects whether type T is a collection navigation property.
/// Specialized once per T by the JIT — result is a static readonly field (constant at runtime).
/// </summary>
internal static class CollectionDetector<T>
{
    public static readonly bool IsCollection = Compute();

    private static bool Compute()
    {
        var type = typeof(T);

        // String implements IEnumerable<char> but is not a navigation collection
        if (type == typeof(string)) return false;

        // Arrays (e.g. Product[])
        if (type.IsArray) return true;

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(IEnumerable<>) ||
                def == typeof(ICollection<>) ||
                def == typeof(IList<>) ||
                def == typeof(List<>) ||
                def == typeof(IReadOnlyCollection<>) ||
                def == typeof(IReadOnlyList<>) ||
                def == typeof(HashSet<>) ||
                def == typeof(ISet<>))
                return true;
        }

        // Covers custom collection types implementing IEnumerable<T>
        foreach (var iface in type.GetInterfaces())
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;

        return false;
    }
}
