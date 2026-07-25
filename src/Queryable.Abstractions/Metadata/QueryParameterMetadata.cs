using System.Linq.Expressions;
using System.Reflection;

namespace Kaleido.Queryable.Metadata;

public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    bool Required,
    string? Description,
    object? DefaultValue)
{
    public static QueryParameterMetadata ForField<TRecord>(
        Expression<Func<TRecord, object?>> field,
        bool required,
        string description)
    {
        ArgumentNullException.ThrowIfNull(field);

        var member = GetProperty(field);

        return new QueryParameterMetadata(
            member.Name,
            member.PropertyType,
            required,
            description,
            null);
    }

    private static PropertyInfo GetProperty<TRecord>(
        Expression<Func<TRecord, object?>> expression)
    {
        if (expression.Body is MemberExpression member)
        {
            return (PropertyInfo)member.Member;
        }

        // Handles value types being boxed to object
        if (expression.Body is UnaryExpression unary &&
            unary.Operand is MemberExpression unaryMember)
        {
            return (PropertyInfo)unaryMember.Member;
        }

        throw new InvalidOperationException(
            $"Expression '{expression}' does not reference a property.");
    }
}
