using System.Linq.Expressions;

namespace MsSpecification.Core.Models;

/// <summary>
/// Specifies an ordering clause for a specification query.
/// </summary>
public readonly struct OrderByClause<T>
{
    public readonly Expression<Func<T, object?>> KeySelector;
    public readonly bool Descending;

    public OrderByClause(Expression<Func<T, object?>> keySelector, bool descending)
    {
        KeySelector = keySelector;
        Descending = descending;
    }
}
