using System.Linq.Expressions;
using MsSpecification.Core.Models;

namespace MsSpecification.Core.Builder;

/// <summary>
/// Fluent builder for constructing type-safe include chains.
/// Defined as a readonly struct — the builder itself lives on the stack, never the heap.
///
/// Type parameters enforce navigation safety at compile time:
///   T         = root entity
///   TProperty = current "tip" of the chain (what you can ThenInclude from)
/// </summary>
/// <typeparam name="T">Root entity type</typeparam>
/// <typeparam name="TProperty">Current property type at the tip of the chain</typeparam>
public readonly struct IncludeChainBuilder<T, TProperty>
{
    private readonly IncludeChain<T> _chain;

    internal IncludeChainBuilder(IncludeChain<T> chain) => _chain = chain;

    // ─────────────────────────────────────────────────────────────────────────────
    // Reference navigation: x => x.Customer
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a ThenInclude for a reference (non-collection) navigation property.
    /// </summary>
    public IncludeChainBuilder<T, TNext> ThenInclude<TNext>(
        Expression<Func<TProperty, TNext>> expression)
    {
        _chain.AddStep(CreateStep<TProperty, TNext>(expression));
        return new IncludeChainBuilder<T, TNext>(_chain);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Collection navigations — overloaded per collection interface so callers
    // don't need to cast their navigation properties.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>ThenInclude for IEnumerable&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, IEnumerable<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, IEnumerable<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>ThenInclude for ICollection&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, ICollection<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, ICollection<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>ThenInclude for List&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, List<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, List<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>ThenInclude for IReadOnlyCollection&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, IReadOnlyCollection<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, IReadOnlyCollection<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>ThenInclude for IReadOnlyList&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, IReadOnlyList<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, IReadOnlyList<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>ThenInclude for HashSet&lt;TElement&gt; navigation</summary>
    public IncludeChainBuilder<T, TElement> ThenInclude<TElement>(
        Expression<Func<TProperty, HashSet<TElement>>> expression)
        where TElement : class
    {
        _chain.AddStep(CreateCollectionStep<TProperty, HashSet<TElement>, TElement>(expression));
        return new IncludeChainBuilder<T, TElement>(_chain);
    }

    /// <summary>Finalizes the chain and returns the built IncludeChain.</summary>
    public IncludeChain<T> Build() => _chain;

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static IncludeStep CreateStep<TFrom, TTo>(System.Linq.Expressions.LambdaExpression expression)
        => new(
            navigationPropertyPath: expression,
            fromType: TypeMetadataCache<TFrom, TTo>.FromType,
            toType: TypeMetadataCache<TFrom, TTo>.ToType,
            isCollection: TypeMetadataCache<TFrom, TTo>.IsCollection,
            propertyName: ExpressionNameCache.GetPropertyName(expression),
            isFiltered: ExpressionNameCache.IsFilteredExpression(expression)
        );

    private static IncludeStep CreateCollectionStep<TFrom, TCollection, TElement>(
        System.Linq.Expressions.LambdaExpression expression)
        => new(
            navigationPropertyPath: expression,
            fromType: typeof(TFrom),
            toType: typeof(TCollection),
            isCollection: true,
            propertyName: ExpressionNameCache.GetPropertyName(expression),
            isFiltered: ExpressionNameCache.IsFilteredExpression(expression)
        );
}