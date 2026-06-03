using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using MsSpecification.Core.Models;

namespace MsSpecification.Core.Builder;

/// <summary>
/// Represents a complete navigation include chain (root + optional ThenInclude steps).
/// Backed by a single right-sized array that grows by doubling. Typical include chains are
/// shallow (1-3 levels), so the default capacity of 4 avoids any resizing in practice.
/// </summary>
/// <typeparam name="T">Root entity type</typeparam>
public sealed class IncludeChain<T>
{
    private IncludeStep[] _steps;
    private int _count;
    private bool _returned;

    internal IncludeChain(int initialCapacity = 4)
    {
        _steps = new IncludeStep[initialCapacity];
        _count = 0;
        _returned = false;
    }

    /// <summary>
    /// Returns the steps as a span — no allocation, no boxing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed after <see cref="Return"/> has been called.</exception>
    public ReadOnlySpan<IncludeStep> Steps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectNotReturned();
            return _steps.AsSpan(0, _count);
        }
    }

    /// <summary>Root (first) include step</summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed after <see cref="Return"/> has been called.</exception>
    public ref readonly IncludeStep RootStep
    {
        get
        {
            ObjectNotReturned();
            return ref _steps[0];
        }
    }

    /// <summary>Number of steps in the chain</summary>
    public int Count => _count;

    internal void AddStep(in IncludeStep step)
    {
        ObjectNotReturned();
        if (_count == _steps.Length)
            Grow();

        _steps[_count++] = step;
    }

    private void Grow()
    {
        var newArray = new IncludeStep[_steps.Length * 2];
        _steps.AsSpan(0, _count).CopyTo(newArray);
        _steps = newArray;
    }

    /// <summary>
    /// Marks the chain as released and drops its backing array. Internal because callers cannot
    /// reason about the chain's lifetime safely — specs are shared across requests, and an early
    /// release would cause use-after-free in concurrent consumers. The GC handles normal cleanup;
    /// this exists only to deterministically invalidate a chain that is known to be done.
    /// </summary>
    internal void Return()
    {
        if (_returned) return;
        _returned = true;
        _steps = Array.Empty<IncludeStep>();
        _count = 0;
    }

    public override string ToString()
    {
        var steps = Steps;
        if (steps.IsEmpty) return "(empty)";

        var parts = new string[steps.Length];
        for (int i = 0; i < steps.Length; i++)
            parts[i] = steps[i].PropertyName;

        return string.Join(" -> ", parts);
    }

    private void ObjectNotReturned()
    {
        if (_returned)
            throw new InvalidOperationException("Cannot access IncludeChain after Return() has been called.");
    }
}
