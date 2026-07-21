using Kaleido.Extensions;
using System.Linq.Expressions;
using System.Reflection;

namespace Kaleido.Queryable;

public sealed class QueryableCompiledQueryApplier<TRecord> : IQueryableCompiledQueryApplier<TRecord>
    where TRecord : class
{
    public IQueryable<TRecord> ApplyFilter(
        IQueryable<TRecord> query,
        CompiledFilterExpression? filter)
    {
        if (filter is null) return query;

        var parameter = Expression.Parameter(typeof(TRecord), "x");
        var body = BuildFilter(parameter, filter);

        return query.Where(Expression.Lambda<Func<TRecord, bool>>(body, parameter));
    }

    public IQueryable<TRecord> ApplySearch(
        IQueryable<TRecord> query,
        CompiledSearchExpression? search)
    {
        if (search is null) return query;

        var parameter = Expression.Parameter(typeof(TRecord), "x");
        var body = BuildSearch(parameter, search);

        return query.Where(Expression.Lambda<Func<TRecord, bool>>(body, parameter));
    }

    public IQueryable<TRecord> ApplySort(
        IQueryable<TRecord> query,
        IReadOnlyList<CompiledSort> sort)
    {
        var ordered = false;

        foreach (var item in sort.OrderBy(x => x.Sequence))
        {
            query = ApplySortItem(query, item, ordered);
            ordered = true;
        }

        return query;
    }

    public IQueryable<TRecord> ApplyPage(
        IQueryable<TRecord> query,
        CompiledPage page)
    {
        return query
            .Skip(page.Offset)
            .Take(page.Size);
    }

    private static Expression BuildFilter(
        ParameterExpression parameter,
        CompiledFilterExpression expression)
    {
        return expression switch
        {
            CompiledFilterCondition condition =>
                BuildFilterCondition(parameter, condition),

            CompiledFilterGroup group =>
                BuildGroup(
                    group.Operator,
                    group.Expressions
                        .Select(x => BuildFilter(parameter, x))
                        .ToArray()),

            _ => throw new NotSupportedException(
                $"Unsupported compiled filter type '{expression.GetType().Name}'.")
        };
    }

    private static Expression BuildSearch(
        ParameterExpression parameter,
        CompiledSearchExpression expression)
    {
        return expression switch
        {
            CompiledSearchCondition condition =>
                BuildSearchCondition(parameter, condition),

            CompiledSearchGroup group =>
                BuildGroup(
                    group.Operator,
                    group.Expressions
                        .Select(x => BuildSearch(parameter, x))
                        .ToArray()),

            _ => throw new NotSupportedException(
                $"Unsupported compiled search type '{expression.GetType().Name}'.")
        };
    }

    private static Expression BuildGroup(
        LogicalOperator op,
        IReadOnlyList<Expression> expressions)
    {
        if (expressions.Count == 0)
        {
            return Expression.Constant(true);
        }

        return op == LogicalOperator.And
            ? expressions.Aggregate(Expression.AndAlso)
            : expressions.Aggregate(Expression.OrElse);
    }

    private static Expression BuildFilterCondition(
        ParameterExpression parameter,
        CompiledFilterCondition condition)
    {
        var member = Expression.PropertyOrField(parameter, condition.Field.Name);

        return condition.Operator switch
        {
            FilterOperator.Eq =>
                Expression.Equal(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Ne =>
                Expression.NotEqual(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Gt =>
                Expression.GreaterThan(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Gte =>
                Expression.GreaterThanOrEqual(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Lt =>
                Expression.LessThan(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Lte =>
                Expression.LessThanOrEqual(
                    member,
                    Constant(member.Type, GetValue(condition, 0))),

            FilterOperator.Contains =>
                StringCall(
                    member,
                    nameof(string.Contains),
                    GetValue(condition, 0),
                    negate: false),

            FilterOperator.NotContains =>
                StringCall(
                    member,
                    nameof(string.Contains),
                    GetValue(condition, 0),
                    negate: true),

            FilterOperator.StartsWith =>
                StringCall(
                    member,
                    nameof(string.StartsWith),
                    GetValue(condition, 0),
                    negate: false),

            FilterOperator.EndsWith =>
                StringCall(
                    member,
                    nameof(string.EndsWith),
                    GetValue(condition, 0),
                    negate: false),

            FilterOperator.In =>
                InCall(
                    member,
                    condition.Values,
                    negate: false),

            FilterOperator.NotIn =>
                InCall(
                    member,
                    condition.Values,
                    negate: true),

            FilterOperator.Between =>
                BetweenCall(
                    member,
                    condition.Values,
                    negate: false),

            FilterOperator.NotBetween =>
                BetweenCall(
                    member,
                    condition.Values,
                    negate: true),

            FilterOperator.IsNull =>
                Expression.Equal(
                    member,
                    Expression.Constant(null, member.Type)),

            FilterOperator.IsNotNull =>
                Expression.NotEqual(
                    member,
                    Expression.Constant(null, member.Type)),

            FilterOperator.IsTrue =>
                BooleanCall(
                    member,
                    expected: true),

            FilterOperator.IsFalse =>
                BooleanCall(
                    member,
                    expected: false),

            _ => throw new NotSupportedException(
                $"Filter operator '{condition.Operator}' is not supported by the IQueryable provider.")
        };
    }

    private static Expression BuildSearchCondition(
        ParameterExpression parameter,
        CompiledSearchCondition condition)
    {
        var member = Expression.PropertyOrField(parameter, condition.Field.Name);

        return condition.MatchMode switch
        {
            MatchMode.Exact =>
                Expression.Equal(
                    member,
                    Constant(member.Type, condition.SearchText)),

            MatchMode.StartsWith =>
                StringCall(
                    member,
                    nameof(string.StartsWith),
                    condition.SearchText,
                    negate: false),

            MatchMode.EndsWith =>
                StringCall(
                    member,
                    nameof(string.EndsWith),
                    condition.SearchText,
                    negate: false),

            MatchMode.Contains =>
                StringCall(
                    member,
                    nameof(string.Contains),
                    condition.SearchText,
                    negate: false),

            MatchMode.Fuzzy =>
                throw new NotSupportedException(
                    "Match mode 'Fuzzy' requires provider-specific support and is not supported by the generic IQueryable applier."),

            MatchMode.Soundex =>
                throw new NotSupportedException(
                    "Match mode 'Soundex' requires provider-specific support and is not supported by the generic IQueryable applier."),

            MatchMode.FullText =>
                throw new NotSupportedException(
                    "Match mode 'FullText' requires provider-specific support and is not supported by the generic IQueryable applier."),

            _ => throw new NotSupportedException(
                $"Match mode '{condition.MatchMode}' is not supported by the IQueryable provider.")
        };
    }

    private static IQueryable<TRecord> ApplySortItem(
        IQueryable<TRecord> query,
        CompiledSort sort,
        bool thenBy)
    {
        var parameter = Expression.Parameter(
            typeof(TRecord),
            "x");

        var member = Expression.PropertyOrField(
            parameter,
            sort.Field.Name);

        Expression sortExpression = member;

        if (member.Type.IsEnum)
        {
            sortExpression = Expression.Call(
                typeof(EnumExtensions),
                nameof(EnumExtensions.GetDescription),
                Type.EmptyTypes,
                Expression.Convert(member, typeof(Enum)));
        }

        var lambda = Expression.Lambda(
            sortExpression,
            parameter);

        var methodName = (thenBy, sort.Direction) switch
        {
            (false, SortDirection.Ascending) => nameof(System.Linq.Queryable.OrderBy),
            (false, SortDirection.Descending) => nameof(System.Linq.Queryable.OrderByDescending),
            (true, SortDirection.Ascending) => nameof(System.Linq.Queryable.ThenBy),
            (true, SortDirection.Descending) => nameof(System.Linq.Queryable.ThenByDescending),

            _ => throw new NotSupportedException(
                $"Sort direction '{sort.Direction}' is not supported.")
        };

        var method = typeof(System.Linq.Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m =>
                m.Name == methodName &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(
                typeof(TRecord),
                sortExpression.Type);

        return (IQueryable<TRecord>)
            method.Invoke(
                null,
                new object[] { query, lambda })!;
    }

    private static Expression StringCall(
        Expression member,
        string methodName,
        object? value,
        bool negate)
    {
        if (member.Type != typeof(string))
        {
            throw new NotSupportedException(
                $"String operator '{methodName}' can only be applied to string fields. Field expression type was '{member.Type.Name}'.");
        }

        var method = typeof(string).GetMethod(
            methodName,
            new[] { typeof(string) })!;

        var notNull = Expression.NotEqual(
            member,
            Expression.Constant(null, typeof(string)));

        var call = Expression.Call(
            member,
            method,
            Expression.Constant(value?.ToString() ?? string.Empty));

        var expression = Expression.AndAlso(notNull, call);

        return negate
            ? Expression.Not(expression)
            : expression;
    }

    private static Expression InCall(
        Expression member,
        IReadOnlyList<object?> values,
        bool negate)
    {
        if (values.Count == 0)
        {
            // IN () should never match.
            var emptyResult = Expression.Constant(false);
            return negate ? Expression.Not(emptyResult) : emptyResult;
        }

        var array = CreateTypedArray(member.Type, values);

        var method = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m =>
                m.Name == nameof(Enumerable.Contains) &&
                m.GetParameters().Length == 2)
            .MakeGenericMethod(member.Type);

        var call = Expression.Call(
            method,
            Expression.Constant(array),
            member);

        return negate
            ? Expression.Not(call)
            : call;
    }

    private static Expression BetweenCall(
        Expression member,
        IReadOnlyList<object?> values,
        bool negate)
    {
        if (values.Count < 2)
        {
            throw new InvalidOperationException(
                "Between and NotBetween require exactly two values.");
        }

        var lower = Constant(member.Type, values[0]);
        var upper = Constant(member.Type, values[1]);

        var greaterThanOrEqual = Expression.GreaterThanOrEqual(member, lower);
        var lessThanOrEqual = Expression.LessThanOrEqual(member, upper);

        var between = Expression.AndAlso(
            greaterThanOrEqual,
            lessThanOrEqual);

        return negate
            ? Expression.Not(between)
            : between;
    }

    private static Expression BooleanCall(
        Expression member,
        bool expected)
    {
        var targetType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;

        if (targetType != typeof(bool))
        {
            throw new NotSupportedException(
                $"Boolean operator can only be applied to bool fields. Field expression type was '{member.Type.Name}'.");
        }

        return Expression.Equal(
            member,
            Constant(member.Type, expected));
    }

    private static object CreateTypedArray(
        Type memberType,
        IReadOnlyList<object?> values)
    {
        var array = Array.CreateInstance(memberType, values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            array.SetValue(
                ConvertValue(values[i], memberType),
                i);
        }

        return array;
    }

    private static object? GetValue(
        CompiledFilterCondition condition,
        int index)
    {
        if (condition.Values.Count <= index)
        {
            throw new InvalidOperationException(
                $"Filter operator '{condition.Operator}' requires a value at index {index}.");
        }

        return condition.Values[index];
    }

    private static ConstantExpression Constant(
        Type targetType,
        object? value)
    {
        return Expression.Constant(
            ConvertValue(value, targetType),
            targetType);
    }

    private static object? ConvertValue(
        object? value,
        Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var nullableType = Nullable.GetUnderlyingType(targetType);
        var effectiveType = nullableType ?? targetType;

        if (effectiveType.IsEnum)
        {
            var stringValue = value.ToString();

            if (EnumExtensions.TryParseFromDescription(
                    effectiveType,
                    stringValue,
                    out var enumValue))
            {
                return enumValue;
            }

            return Enum.Parse(
                effectiveType,
                stringValue!,
                ignoreCase: true);
        }

        if (effectiveType == typeof(Guid))
        {
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(DateOnly))
        {
            return value is DateOnly dateOnly
                ? dateOnly
                : DateOnly.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(DateTime))
        {
            return value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(TimeOnly))
        {
            return value is TimeOnly timeOnly
                ? timeOnly
                : TimeOnly.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(TimeSpan))
        {
            return value is TimeSpan timeSpan
                ? timeSpan
                : TimeSpan.Parse(value.ToString()!);
        }

        if (effectiveType == typeof(bool))
        {
            return value is bool boolValue
                ? boolValue
                : bool.Parse(value.ToString()!);
        }

        return Convert.ChangeType(value, effectiveType);
    }
}
