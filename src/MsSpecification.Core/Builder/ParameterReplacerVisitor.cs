using System.Linq.Expressions;

namespace MsSpecification.Core.Builder;

/// <summary>
/// Rewrites references to one <see cref="ParameterExpression"/> as another while preserving
/// the rest of the expression tree. Used to merge two predicates that were authored against
/// different parameter instances into a single lambda with one parameter.
/// </summary>
internal sealed class ParameterReplacerVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _oldParam;
    private readonly ParameterExpression _newParam;

    public ParameterReplacerVisitor(ParameterExpression oldParam, ParameterExpression newParam)
    {
        _oldParam = oldParam;
        _newParam = newParam;
    }

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _oldParam ? _newParam : base.VisitParameter(node);
}
