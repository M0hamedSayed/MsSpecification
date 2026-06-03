using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MsSpecification.Core.Builder;

/// <summary>
/// Caches property name extraction from lambda expressions.
/// Uses ConditionalWeakTable so cached entries are released when the expression itself
/// is garbage collected — no memory leaks.
/// Extraction itself happens once per unique lambda instance.
/// </summary>
internal static class ExpressionNameCache
{
    private static readonly ConditionalWeakTable<LambdaExpression, string> Cache = new();

    // Filtered LINQ methods that EF Core supports in filtered includes
    private static readonly HashSet<string> FilteredIncludeMethods = new(StringComparer.Ordinal)
    {
        "Where", "OrderBy", "OrderByDescending",
        "ThenBy", "ThenByDescending", "Skip", "Take"
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetPropertyName(LambdaExpression expression)
        => Cache.GetValue(expression, static e => Extract(e));

    /// <summary>
    /// Returns true when the expression body contains a filtering method call
    /// (Where/OrderBy/Take/Skip) rather than a plain member access.
    /// EF Core requires the strongly-typed fluent API for these — dot-notation paths won't work.
    /// </summary>
    public static bool IsFilteredExpression(LambdaExpression expression)
    {
        var body = Unwrap(expression.Body);
        if (body is not MethodCallExpression call) return false;
        return FilteredIncludeMethods.Contains(call.Method.Name);
    }

    private static string Extract(LambdaExpression expression)
    {
        var body = Unwrap(expression.Body);

        if (body is MemberExpression member)
            return BuildMemberPath(member);

        if (body is MethodCallExpression call)
        {
            // Instance method: obj.Method(...)
            if (call.Object != null)
            {
                var inner = Unwrap(call.Object);
                if (inner is MemberExpression m) return BuildMemberPath(m);
            }
            // Extension method: Method(obj, ...)
            foreach (var arg in call.Arguments)
            {
                var inner = Unwrap(arg);
                if (inner is MemberExpression m) return BuildMemberPath(m);
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract property name from expression: '{expression}'. " +
            $"Only member access (x => x.Property) and filtered includes (x => x.Collection.Where(...)) are supported.");
    }

    /// <summary>
    /// Walks the MemberExpression chain from leaf to root and builds a dot-separated path
    /// (e.g. "Order.Customer.Name" from expr <c>x => x.Order.Customer.Name</c>).
    /// </summary>
    private static string BuildMemberPath(MemberExpression member)
    {
        // Fast path: single-level access (x => x.Name)
        if (member.Expression is ParameterExpression)
            return member.Member.Name;

        // Slow path: walk the chain and reverse
        var parts = new List<string> { member.Member.Name };
        var current = member.Expression;

        while (current is MemberExpression inner)
        {
            parts.Add(inner.Member.Name);
            current = inner.Expression;
        }

        parts.Reverse();
        return string.Join(".", parts);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert } u)
            expression = u.Operand;
        return expression;
    }
}
