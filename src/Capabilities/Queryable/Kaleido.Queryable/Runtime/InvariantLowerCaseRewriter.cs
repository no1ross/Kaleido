using System.Linq.Expressions;
using System.Reflection;

namespace Kaleido.Queryable.Runtime;

/// <summary>
/// Makes a case-insensitive comparison culture-invariant on the member side for LINQ-to-objects: every
/// <see cref="string.ToLower()"/> becomes <see cref="string.ToLowerInvariant"/>, as the compared
/// constants already are. Under a culture such as <c>tr-TR</c>, <c>"I".ToLower()</c> is a dotless
/// <c>ı</c>, so <c>"INFO"</c> would not match <c>"info"</c> (#519).
/// </summary>
/// <remarks>
/// Only for an in-memory query: a database provider (EF Core) translates <c>ToLower()</c> to SQL and
/// compares with the database's collation, so its expression is left as built.
/// </remarks>
internal sealed class InvariantLowerCaseRewriter : ExpressionVisitor
{
    private static readonly MethodInfo ToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(string.ToLower)}' on string.");

    private static readonly MethodInfo ToLowerInvariantMethod =
        typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.ReflectionError,
            $"Could not locate method '{nameof(string.ToLowerInvariant)}' on string.");

    private static readonly InvariantLowerCaseRewriter Instance = new();

    /// <summary>The expression with every <c>ToLower()</c> made <c>ToLowerInvariant()</c>.</summary>
    internal static Expression Rewrite(Expression expression) => Instance.Visit(expression);

    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        node.Method == ToLowerMethod && node.Object is { } target
            ? Expression.Call(Visit(target), ToLowerInvariantMethod)
            : base.VisitMethodCall(node);
}
