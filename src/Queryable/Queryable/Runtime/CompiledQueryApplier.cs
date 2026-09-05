using Kaleido.Queryable.Query;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Kaleido.Queryable.Runtime;

internal sealed class CompiledQueryApplier<TQueryContext> : ICompiledQueryApplier<TQueryContext>
    where TQueryContext : class
{
    public IQueryable<TQueryContext> ApplyFilter(
        IQueryable<TQueryContext> query,
        CompiledFilterExpression? filter)
    {
        if (filter is null)
        {
            return query;
        }

        var parameter =
            Expression.Parameter(
                typeof(TQueryContext),
                "x");

        var body =
            BuildFilter(
                parameter,
                filter);

        return query.Where(
            Expression.Lambda<Func<TQueryContext, bool>>(
                body,
                parameter));
    }

    public IQueryable<TQueryContext> ApplySearch(
        IQueryable<TQueryContext> query,
        CompiledSearch? search)
    {
        if (search is null)
        {
            return query;
        }

        var parameter =
            Expression.Parameter(
                typeof(TQueryContext),
                "x");

        var body =
            BuildSearch(
                parameter,
                search);

        return query.Where(
            Expression.Lambda<Func<TQueryContext, bool>>(
                body,
                parameter));
    }

    public IQueryable<TQueryContext> ApplySort(
        IQueryable<TQueryContext> query,
        IReadOnlyList<CompiledSort> sort)
    {
        var ordered =
            false;

        foreach (var item in sort.OrderBy(x => x.Sequence))
        {
            query =
                ApplySortItem(
                    query,
                    item,
                    ordered);

            ordered = true;
        }

        return query;
    }

    private static Expression BuildFilter(
        ParameterExpression parameter,
        CompiledFilterExpression expression)
    {
        return expression switch
        {
            CompiledFilterCondition condition =>
                BuildFilterCondition(
                    parameter,
                    condition),

            CompiledFilterGroup group =>
                BuildGroup(
                    group.Operator,
                    group.Filters
                        .Select(x => BuildFilter(parameter, x))
                        .ToArray()),

            _ => throw new NotSupportedException(
                $"Unsupported compiled filter type '{expression.GetType().Name}'.")
        };
    }

    private static Expression BuildSearch(
        ParameterExpression parameter,
        CompiledSearch search)
    {
        var expressions =
            search.Fields
                .Select(field =>
                    BuildSearchField(
                        parameter,
                        field,
                        search.SearchText))
                .ToArray();

        if (expressions.Length == 0)
        {
            return Expression.Constant(true);
        }

        return expressions.Aggregate(
            Expression.OrElse);
    }

    private static Expression BuildSearchField(
        ParameterExpression parameter,
        CompiledSearchField field,
    string searchText)
    {
        var member =
            Expression.PropertyOrField(
                parameter,
                field.Field.Name);

        return field.MatchMode switch
        {
            MatchMode.Exact =>
                EqualityCall(
                    member,
                    searchText,
                    negate: false),

            MatchMode.StartsWith =>
                StringCall(
                    member,
                    nameof(string.StartsWith),
                    searchText,
                    negate: false),

            MatchMode.EndsWith =>
                StringCall(
                    member,
                    nameof(string.EndsWith),
                    searchText,
                    negate: false),

            MatchMode.Contains =>
                StringCall(
                    member,
                    nameof(string.Contains),
                    searchText,
                    negate: false),

            _ => throw new NotSupportedException(
                $"Match mode '{field.MatchMode}' is not supported by the IQueryable provider.")
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
        var member =
            Expression.PropertyOrField(
                parameter,
                condition.Field.Name);

        return condition.Operator switch
        {
            FilterOperator.Equals =>
                EqualityCall(
                    member,
                    GetValue(condition, 0),
                    negate: false),

            FilterOperator.NotEquals =>
                EqualityCall(
                    member,
                    GetValue(condition, 0),
                    negate: true),

            FilterOperator.GreaterThan =>
                Expression.GreaterThan(
                    member,
                    Constant(
                        member.Type,
                        GetValue(condition, 0))),

            FilterOperator.GreaterThanOrEqual =>
                Expression.GreaterThanOrEqual(
                    member,
                    Constant(
                        member.Type,
                        GetValue(condition, 0))),

            FilterOperator.LessThan =>
                Expression.LessThan(
                    member,
                    Constant(
                        member.Type,
                        GetValue(condition, 0))),

            FilterOperator.LessThanOrEqual =>
                Expression.LessThanOrEqual(
                    member,
                    Constant(
                        member.Type,
                        GetValue(condition, 0))),

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
                    Expression.Constant(
                        null,
                        member.Type)),

            FilterOperator.IsNotNull =>
                Expression.NotEqual(
                    member,
                    Expression.Constant(
                        null,
                        member.Type)),

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

    private static Expression EqualityCall(
        Expression member,
        object? value,
        bool negate)
    {
        if (member.Type != typeof(string))
        {
            var expression1 =
                negate
                    ? Expression.NotEqual(
                        member,
                        Constant(
                            member.Type,
                            value))
                    : Expression.Equal(
                        member,
                        Constant(
                            member.Type,
                            value));

            return expression1;
        }

        if (value is null)
        {
            return negate
                ? Expression.NotEqual(
                    member,
                    Expression.Constant(
                        null,
                        typeof(string)))
                : Expression.Equal(
                    member,
                    Expression.Constant(
                        null,
                        typeof(string)));
        }

        var stringValue =
            ValidateValue(
                value,
                typeof(string)) as string
            ?? string.Empty;

        var notNull =
            Expression.NotEqual(
                member,
                Expression.Constant(
                    null,
                    typeof(string)));

        var normalizedMember =
            ToLower(member);

        var normalizedValue =
            Expression.Constant(
                stringValue.ToLowerInvariant(),
                typeof(string));

        var equals =
            Expression.Equal(
                normalizedMember,
                normalizedValue);

        var expression =
            Expression.AndAlso(
                notNull,
                equals);

        return negate
            ? Expression.Not(expression)
            : expression;
    }

    private static IQueryable<TQueryContext> ApplySortItem(
        IQueryable<TQueryContext> query,
        CompiledSort sort,
        bool thenBy)
    {
        var parameter =
            Expression.Parameter(
                typeof(TQueryContext),
                "x");

        var member =
            Expression.PropertyOrField(
                parameter,
                sort.Field.Name);

        var lambda =
            Expression.Lambda(
                member,
                parameter);

        var methodName =
            (thenBy, sort.Direction) switch
            {
                (false, SortDirection.Ascending) =>
                    nameof(System.Linq.Queryable.OrderBy),

                (false, SortDirection.Descending) =>
                    nameof(System.Linq.Queryable.OrderByDescending),

                (true, SortDirection.Ascending) =>
                    nameof(System.Linq.Queryable.ThenBy),

                (true, SortDirection.Descending) =>
                    nameof(System.Linq.Queryable.ThenByDescending),

                _ => throw new NotSupportedException(
                    $"Sort direction '{sort.Direction}' is not supported.")
            };

        var method =
            typeof(System.Linq.Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == methodName &&
                    m.GetParameters().Length == 2)
                .MakeGenericMethod(
                    typeof(TQueryContext),
                    member.Type);

        return (IQueryable<TQueryContext>)
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

        var stringValue =
            ValidateValue(
                value,
                typeof(string)) as string
            ?? string.Empty;

        var method =
            typeof(string).GetMethod(
                methodName,
                new[] { typeof(string) })!;

        var notNull =
            Expression.NotEqual(
                member,
                Expression.Constant(
                    null,
                    typeof(string)));

        var normalizedMember =
            ToLower(member);

        var normalizedValue =
            Expression.Constant(
                stringValue.ToLowerInvariant(),
                typeof(string));

        var call =
            Expression.Call(
                normalizedMember,
                method,
                normalizedValue);

        var expression =
            Expression.AndAlso(
                notNull,
                call);

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
            var emptyResult =
                Expression.Constant(false);

            return negate
                ? Expression.Not(emptyResult)
                : emptyResult;
        }

        if (member.Type == typeof(string))
        {
            var normalizedValues =
                values
                    .Select(value =>
                        ValidateValue(
                            value,
                            typeof(string)) as string
                        ?? string.Empty)
                    .Select(value =>
                        value.ToLowerInvariant())
                    .ToArray();

            var method =
                typeof(Enumerable)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m =>
                        m.Name == nameof(Enumerable.Contains) &&
                        m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(string));

            var notNull =
                Expression.NotEqual(
                    member,
                    Expression.Constant(
                        null,
                        typeof(string)));

            var normalizedMember =
                ToLower(member);

            var call =
                Expression.Call(
                    method,
                    Expression.Constant(normalizedValues),
                    normalizedMember);

            var expression =
                Expression.AndAlso(
                    notNull,
                    call);

            return negate
                ? Expression.Not(expression)
                : expression;
        }

        var array =
            CreateTypedArray(
                member.Type,
                values);

        var containsMethod =
            typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == nameof(Enumerable.Contains) &&
                    m.GetParameters().Length == 2)
                .MakeGenericMethod(member.Type);

        var containsCall =
            Expression.Call(
                containsMethod,
                Expression.Constant(array),
                member);

        return negate
            ? Expression.Not(containsCall)
            : containsCall;
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

        var lower =
            Constant(
                member.Type,
                values[0]);

        var upper =
            Constant(
                member.Type,
                values[1]);

        var greaterThanOrEqual =
            Expression.GreaterThanOrEqual(
                member,
                lower);

        var lessThanOrEqual =
            Expression.LessThanOrEqual(
                member,
                upper);

        var between =
            Expression.AndAlso(
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
        var targetType =
            Nullable.GetUnderlyingType(member.Type)
            ?? member.Type;

        if (targetType != typeof(bool))
        {
            throw new NotSupportedException(
                $"Boolean operator can only be applied to bool fields. Field expression type was '{member.Type.Name}'.");
        }

        return Expression.Equal(
            member,
            Constant(
                member.Type,
                expected));
    }

    private static object CreateTypedArray(
        Type memberType,
        IReadOnlyList<object?> values)
    {
        var array =
            Array.CreateInstance(
                memberType,
                values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            array.SetValue(
                ValidateValue(
                    values[i],
                    memberType),
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

    private static Expression Constant(
        Type targetType,
        object? value)
    {
        var compatibleValue =
            ValidateValue(
                value,
                targetType);

        if (compatibleValue is null)
        {
            return Expression.Constant(
                null,
                targetType);
        }

        var nullableType =
            Nullable.GetUnderlyingType(targetType);

        if (nullableType is null)
        {
            return Expression.Constant(
                compatibleValue,
                targetType);
        }

        return Expression.Convert(
            Expression.Constant(
                compatibleValue,
                nullableType),
            targetType);
    }

    private static object? ValidateValue(
        object? value,
        Type targetType)
    {
        if (value is null)
        {
            if (!CanBeNull(targetType))
            {
                throw new InvalidOperationException(
                    $"Value for non-nullable type '{targetType.Name}' cannot be null.");
            }

            return null;
        }

        var expectedType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        var actualType =
            value.GetType();

        if (expectedType.IsAssignableFrom(actualType))
        {
            return value;
        }

        if (expectedType.IsEnum &&
            value is string enumValue)
        {
            return Enum.Parse(
                expectedType,
                enumValue,
                ignoreCase: true);
        }

        if (IsNumericType(expectedType) &&
            IsNumericType(actualType))
        {
            return Convert.ChangeType(
                value,
                expectedType,
                CultureInfo.InvariantCulture);
        }

        throw new InvalidOperationException(
            $"Value type '{actualType.Name}' is not assignable to expected type '{expectedType.Name}'.");
    }

    private static Expression ToLower(
        Expression expression)
    {
        return Expression.Call(
            expression,
            typeof(string).GetMethod(
                nameof(string.ToLower),
                Type.EmptyTypes)!);
    }

    private static bool CanBeNull(
        Type type)
    {
        return !type.IsValueType ||
               Nullable.GetUnderlyingType(type) is not null;
    }

    private static bool IsNumericType(
        Type type)
    {
        var actualType =
            Nullable.GetUnderlyingType(type)
            ?? type;

        return actualType == typeof(byte) ||
               actualType == typeof(sbyte) ||
               actualType == typeof(short) ||
               actualType == typeof(ushort) ||
               actualType == typeof(int) ||
               actualType == typeof(uint) ||
               actualType == typeof(long) ||
               actualType == typeof(ulong) ||
               actualType == typeof(float) ||
               actualType == typeof(double) ||
               actualType == typeof(decimal);
    }
}