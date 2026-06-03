using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using MsSpecification.Core.Builder;
using MsSpecification.Core.Models;

namespace MsSpecification.Infra.EF;

/// <summary>
/// Applies include chains that contain filtered steps (Where/OrderBy/Take/Skip)
/// using EF Core's strongly-typed Include(...).ThenInclude(...) fluent API.
///
/// WHY THIS EXISTS
/// ═══════════════
/// EF Core's string-based source.Include("Navigation.Child") does NOT support
/// filtered includes — it silently ignores the filter and loads all related data.
/// Only the typed fluent API supports filtered includes:
///   source.Include(x =&gt; x.Collection.Where(e =&gt; e.IsActive))
///         .ThenInclude(e =&gt; e.Reference)
///
/// THE CHALLENGE
/// ═════════════
/// The typed API return type changes per step:
///   IIncludableQueryable&lt;TEntity, TProperty&gt;
/// so you cannot drive it dynamically in a loop without reflection.
///
/// THE SOLUTION
/// ════════════
/// Build typed delegate chains at first use, then cache them. The cache is bounded
/// (default 1024 entries per entity type) with FIFO eviction to prevent memory growth
/// in apps that generate many distinct filter shapes. Cache key is a structural
/// fingerprint that uses control characters as field/step separators — these cannot
/// appear in CLR type names, so the fingerprint is collision-free for any valid input.
///
/// THREAD SAFETY
/// ═════════════
/// ConcurrentDictionary&lt;K,V&gt;.GetOrAdd is thread-safe; concurrent first-calls for
/// the same fingerprint may each build a delegate independently, but all will
/// produce identical results and only one will be stored.
/// </summary>
internal static class FilteredIncludeApplier
{
    // Default cache cap per entity type. Sized for typical applications that ship
    // a fixed set of specs at design time; long-running apps generating dynamic
    // shapes are bounded against memory growth.
    private const int DefaultCacheCapacity = 1024;

    // Control characters used as fingerprint separators — guaranteed not to appear
    // in CLR type names, property names, or expression ToString() output.
    private const char FieldSeparator = '';
    private const char StepSeparator = '';

    /// <summary>
    /// Per-entity-type bounded FIFO cache: structural fingerprint → compiled applicator delegate.
    /// Generic holder gives each T its own cache (JIT-specialised).
    /// </summary>
    internal static class FilteredIncludeCache<T> where T : class
    {
        internal static readonly BoundedFifoCache<string, Func<IQueryable<T>, IQueryable<T>>> Cache
            = new(DefaultCacheCapacity);
    }

    /// <summary>
    /// Returns the current number of cached applicator delegates for entity type T.
    /// Exposed for test assertions only.
    /// </summary>
    internal static int GetCacheCount<T>() where T : class => FilteredIncludeCache<T>.Cache.Count;

    /// <summary>
    /// Replaces the cache's capacity and clears existing entries. Test-only hook.
    /// Production code should not call this — the default capacity is sufficient.
    /// </summary>
    internal static void SetCacheCapacityForTesting<T>(int capacity) where T : class
        => FilteredIncludeCache<T>.Cache.ResizeAndClear(capacity);

    /// <summary>
    /// Applies a filtered include chain to the source queryable.
    /// On first call for a given chain shape, builds and caches the typed delegate.
    /// On subsequent calls with structurally identical chains, retrieves from cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IQueryable<T> Apply<T>(IQueryable<T> source, IncludeChain<T> chain)
        where T : class
    {
        var steps = chain.Steps;
        if (steps.IsEmpty) return source;

        var fingerprint = BuildFingerprint(steps);
        var applicator = FilteredIncludeCache<T>.Cache.GetOrAdd(fingerprint, _ => BuildApplicator<T>(chain));
        return applicator(source);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fingerprint builder — runs once per unique chain shape
    // ─────────────────────────────────────────────────────────────────────────────

    private static string BuildFingerprint(ReadOnlySpan<IncludeStep> steps)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < steps.Length; i++)
        {
            if (i > 0) sb.Append(StepSeparator);
            var s = steps[i];
            sb.Append(s.FromType.FullName).Append(FieldSeparator)
              .Append(s.PropertyName).Append(FieldSeparator)
              .Append(s.ToType.FullName).Append(FieldSeparator)
              .Append(s.IsCollection ? '1' : '0').Append(FieldSeparator)
              .Append(s.IsFiltered ? '1' : '0').Append(FieldSeparator)
              .Append(s.NavigationPropertyPath?.ToString() ?? string.Empty);
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Delegate builder — runs once per unique fingerprint
    // ─────────────────────────────────────────────────────────────────────────────

    private static Func<IQueryable<T>, IQueryable<T>> BuildApplicator<T>(IncludeChain<T> chain)
        where T : class
    {
        var steps = chain.Steps;
        if (steps.IsEmpty)
            return static q => q;

        return BuildTypedChain<T>(steps);
    }

    /// <summary>
    /// Constructs the full Include(...).ThenInclude(...) delegate chain.
    /// Type tracking uses the actual MethodInfo.ReturnType at each step.
    /// </summary>
    private static Func<IQueryable<T>, IQueryable<T>> BuildTypedChain<T>(
        ReadOnlySpan<IncludeStep> steps)
        where T : class
    {
        var (currentDelegate, currentIncludableType) = BuildRootInclude<T>(in steps[0]);

        for (var i = 1; i < steps.Length; i++)
        {
            (currentDelegate, currentIncludableType) =
                BuildThenInclude<T>(currentDelegate, currentIncludableType, in steps[i]);
        }

        return WrapToQueryable<T>(currentDelegate, currentIncludableType);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reflected MethodInfo caches — populated once per process
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Open-generic <c>Include&lt;TEntity, TProperty&gt;(IQueryable&lt;TEntity&gt;, Expression&lt;Func&lt;TEntity,TProperty&gt;&gt;)</c>.
    /// Resolved once per process at type initialization.
    /// </summary>
    private static readonly MethodInfo IncludeOpenGeneric =
        typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                              && m.IsGenericMethodDefinition
                              && m.GetParameters().Length == 2
                              && m.GetParameters()[1].ParameterType.IsGenericType)
            ?? throw new InvalidOperationException(
                "Cannot find EntityFrameworkQueryableExtensions.Include<TEntity,TProperty>. " +
                "The referenced EF Core assembly may be unexpected or unsupported.");

    /// <summary>
    /// Pre-filtered list of open-generic <c>ThenInclude</c> overloads. Resolved once per process.
    /// </summary>
    private static readonly MethodInfo[] ThenIncludeOpenGenerics =
        typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude)
                     && m.IsGenericMethodDefinition
                     && m.GetParameters().Length == 2)
            .ToArray();

    // ─────────────────────────────────────────────────────────────────────────────
    // Root Include builder
    // ─────────────────────────────────────────────────────────────────────────────

    private static (object Delegate, Type ActualReturnType) BuildRootInclude<T>(
        in IncludeStep step)
        where T : class
    {
        var constructed = IncludeOpenGeneric.MakeGenericMethod(typeof(T), step.ToType);

        var queryParam = Expression.Parameter(typeof(IQueryable<T>), "q");
        var call = Expression.Call(
            null,
            constructed,
            queryParam,
            Expression.Constant(step.NavigationPropertyPath));

        var compiled = Expression.Lambda(call, queryParam).Compile();

        return (compiled, constructed.ReturnType);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ThenInclude builder
    // ─────────────────────────────────────────────────────────────────────────────

    private static (object Delegate, Type ActualReturnType) BuildThenInclude<T>(
        object previousDelegate,
        Type previousIncludableType,
        in IncludeStep step)
        where T : class
    {
        var prevPropertyType = previousIncludableType.GetGenericArguments()[1];

        var thenIncludeMethod = FindThenIncludeMethod(
            typeof(T),
            prevPropertyType,
            step.ToType,
            step.IsCollection,
            previousIncludableType);

        var queryParam = Expression.Parameter(typeof(IQueryable<T>), "q");

        var prevInvoke = Expression.Invoke(
            Expression.Constant(previousDelegate, previousDelegate.GetType()),
            queryParam);

        var castedPrev = Expression.Convert(prevInvoke, previousIncludableType);

        var call = Expression.Call(
            null,
            thenIncludeMethod,
            castedPrev,
            Expression.Constant(step.NavigationPropertyPath));

        var compiled = Expression.Lambda(call, queryParam).Compile();

        return (compiled, thenIncludeMethod.ReturnType);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Method resolution helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static MethodInfo FindThenIncludeMethod(
        Type entityType,
        Type prevPropertyType,
        Type nextPropertyType,
        bool stepTargetsCollection,
        Type previousIncludableType)
    {
        var enumerableElement = TryGetEnumerableElement(prevPropertyType);
        var navigateFromCollectionElements = !stepTargetsCollection && enumerableElement is not null;

        foreach (var method in ThenIncludeOpenGenerics)
        {
            var parameters = method.GetParameters();

            var firstParamType = parameters[0].ParameterType;
            if (!firstParamType.IsGenericType) continue;

            var firstGenericArgs = firstParamType.GetGenericArguments();
            if (firstGenericArgs.Length != 2) continue;

            var secondGenericArg = firstGenericArgs[1];
            var expectsIEnumerableOverload = secondGenericArg.IsGenericType &&
                                             secondGenericArg.GetGenericTypeDefinition() == typeof(IEnumerable<>);

            Type middleTypeForGeneric;
            if (navigateFromCollectionElements)
            {
                if (!expectsIEnumerableOverload)
                    continue;

                middleTypeForGeneric = enumerableElement!;
            }
            else
            {
                if (expectsIEnumerableOverload != stepTargetsCollection)
                    continue;

                middleTypeForGeneric = stepTargetsCollection
                    ? GetElementType(prevPropertyType)
                    : prevPropertyType;
            }

            MethodInfo constructed;
            try
            {
                constructed = method.MakeGenericMethod(entityType, middleTypeForGeneric, nextPropertyType);
            }
            catch
            {
                continue;
            }

            if (!ThenIncludeSecondParameterMatchesFuncFrom(constructed, middleTypeForGeneric))
                continue;

            if (!ThenIncludeFirstParameterMatchesPrevious(constructed, previousIncludableType))
                continue;

            if (navigateFromCollectionElements)
            {
                var genArgs = constructed.GetGenericArguments();
                if (genArgs.Length < 3 || genArgs[1] != enumerableElement)
                    continue;
            }

            return constructed;
        }

        throw new InvalidOperationException(
            $"Cannot find ThenInclude method for " +
            $"entity={entityType.FullName}, " +
            $"prev={prevPropertyType.FullName}, " +
            $"next={nextPropertyType.FullName}, " +
            $"stepTargetsCollection={stepTargetsCollection}. " +
            $"Verify that the navigation property types match your EF Core model.");
    }

    private static bool ThenIncludeSecondParameterMatchesFuncFrom(MethodInfo constructed, Type funcFromType)
    {
        var parameters = constructed.GetParameters();
        if (parameters.Length != 2) return false;

        var p = parameters[1].ParameterType;
        if (!p.IsGenericType || p.GetGenericTypeDefinition() != typeof(Expression<>))
            return false;

        var funcType = p.GetGenericArguments()[0];
        if (!funcType.IsGenericType || funcType.GetGenericTypeDefinition() != typeof(Func<,>))
            return false;

        var funcFrom = funcType.GetGenericArguments()[0];
        return funcFrom == funcFromType;
    }

    private static bool ThenIncludeFirstParameterMatchesPrevious(MethodInfo constructed, Type previousIncludableType)
    {
        var p0 = constructed.GetParameters()[0].ParameterType;
        if (!p0.IsGenericType || !previousIncludableType.IsGenericType) return false;

        var p0Args = p0.GetGenericArguments();
        var prevArgs = previousIncludableType.GetGenericArguments();
        if (p0Args.Length != 2 || prevArgs.Length != 2) return false;

        if (p0Args[0] != prevArgs[0]) return false;

        return p0Args[1].IsAssignableFrom(prevArgs[1]);
    }

    private static Type? TryGetEnumerableElement(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Final wrapper and type helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static Func<IQueryable<T>, IQueryable<T>> WrapToQueryable<T>(
        object lastDelegate,
        Type lastIncludableType)
        where T : class
    {
        var queryParam = Expression.Parameter(typeof(IQueryable<T>), "q");
        var invoke = Expression.Invoke(
            Expression.Constant(lastDelegate, lastDelegate.GetType()),
            queryParam);

        var cast = Expression.Convert(invoke, typeof(IQueryable<T>));
        return Expression.Lambda<Func<IQueryable<T>, IQueryable<T>>>(cast, queryParam).Compile();
    }

    private static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray) return collectionType.GetElementType()!;
        if (collectionType.IsGenericType) return collectionType.GetGenericArguments()[0];
        var iface = collectionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return iface?.GetGenericArguments()[0] ?? typeof(object);
    }
}

/// <summary>
/// Bounded FIFO cache used for compiled filtered-include applicator delegates. Thread-safe.
/// </summary>
/// <remarks>
/// FIFO (rather than LRU) keeps the implementation lock-free for reads and serialises only
/// the eviction step. The fingerprint cache is hit-dominated (shapes repeat across spec
/// instances), so true LRU would offer minimal benefit at considerable concurrency cost.
/// </remarks>
internal sealed class BoundedFifoCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _store;
    private readonly ConcurrentQueue<TKey> _insertionOrder;
    private int _capacity;
    private readonly object _evictionGate = new();

    public BoundedFifoCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _store = new ConcurrentDictionary<TKey, TValue>();
        _insertionOrder = new ConcurrentQueue<TKey>();
    }

    public int Count => _store.Count;

    public int Capacity => Volatile.Read(ref _capacity);

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (_store.TryGetValue(key, out var existing))
            return existing;

        var created = factory(key);
        if (_store.TryAdd(key, created))
        {
            _insertionOrder.Enqueue(key);
            EvictIfOverCapacity();
            return created;
        }

        // Lost the race; return the winning value.
        return _store[key];
    }

    public void ResizeAndClear(int newCapacity)
    {
        if (newCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(newCapacity));
        lock (_evictionGate)
        {
            Volatile.Write(ref _capacity, newCapacity);
            _store.Clear();
            while (_insertionOrder.TryDequeue(out _)) { }
        }
    }

    private void EvictIfOverCapacity()
    {
        var cap = Volatile.Read(ref _capacity);
        if (_store.Count <= cap) return;

        // Serialise eviction so we don't over-evict under contention.
        if (!Monitor.TryEnter(_evictionGate)) return;
        try
        {
            while (_store.Count > cap && _insertionOrder.TryDequeue(out var oldest))
                _store.TryRemove(oldest, out _);
        }
        finally
        {
            Monitor.Exit(_evictionGate);
        }
    }
}
