using System.Globalization;
using System.Text.Json;

namespace Kaleido.Http.Queryable;

/// <summary>
/// Converts a <see cref="QueryApiBody"/> (the HTTP transport shape with string enums
/// and raw JsonElement values) into a <see cref="QueryBody"/> with typed CLR values.
/// This is the HTTP transport's responsibility — core expects filter values to be
/// plain CLR scalars or enums.
/// </summary>
/// <remarks>
/// A refusal names the field and what was expected, never the submitted value: a filter value may be
/// a member's name or identifier, and the message is logged and returned (#520).
/// </remarks>
internal static class QueryBodyResolver
{
    /// <summary>
    /// Converts a transport-level <see cref="QueryApiBody"/> to a runtime
    /// <see cref="QueryBody"/>. Resolves string enum names to their CLR enum values
    /// and coerces <see cref="JsonElement"/> filter values to the field's CLR type.
    /// </summary>
    internal static QueryBody? ToQueryBody(
        this QueryApiBody? apiBody,
        IReadOnlyCollection<QueryableFieldDescriptor> fields)
    {
        if (apiBody is null)
        {
            return null;
        }

        return new QueryBody(
            SearchText: apiBody.SearchText,
            Filter: ResolveFilterNode(apiBody.Filter, fields),
            Sort: ResolveSort(apiBody.Sort),
            Page: ResolvePage(apiBody.Page));
    }

    // ── Filter tree ──────────────────────────────────────────────────────────

    // The node's shape is kept as sent - both parts or neither included - so the runtime's
    // validation rejects an invalid node (qry_invalid_filter_node) instead of the transport
    // silently dropping part of the filter.
    private static QueryFilterNode? ResolveFilterNode(
        QueryApiFilterNode? node,
        IReadOnlyCollection<QueryableFieldDescriptor> fields) =>
        node is null
            ? null
            : new QueryFilterNode(
                node.Condition is null ? null : ResolveCondition(node.Condition, fields),
                node.Group is null ? null : ResolveGroup(node.Group, fields));

    private static QueryFilterCondition ResolveCondition(
        QueryApiFilterCondition condition,
        IReadOnlyCollection<QueryableFieldDescriptor> fields)
    {
        var @operator = ParseFilterOperator(condition.Operator);

        var field = fields.FirstOrDefault(
            f => string.Equals(f.Name, condition.Field, StringComparison.OrdinalIgnoreCase));

        var targetType = field is null
            ? null
            : Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

        var values =
            condition.Values?
                .Select(v => ResolveValue(v, targetType, condition.Field))
                .Cast<object?>()
                .ToArray()
            ?? [];

        return new QueryFilterCondition(
            condition.Field,
            @operator,
            values);
    }

    private static QueryFilterGroup ResolveGroup(
        QueryApiFilterGroup group,
        IReadOnlyCollection<QueryableFieldDescriptor> fields) =>
        new(
            ParseLogicalOperator(group.Operator),
            group.Filters?
                .Select(x => ResolveFilterNode(x, fields) ?? new QueryFilterNode(Condition: null, Group: null))
                .ToArray()
            ?? []);

    // ── Sort / Page ──────────────────────────────────────────────────────────

    private static QuerySort[]? ResolveSort(
        IReadOnlyList<QueryApiSort>? sort) =>
        sort?
            .Select(x => new QuerySort(
                x.Field,
                ParseSortDirection(x.Direction),
                x.Sequence))
            .ToArray();

    private static QueryPage? ResolvePage(QueryApiPage? page) =>
        page is null
            ? null
            : new QueryPage(page.Size, page.Offset);

    // ── Enum parsing ─────────────────────────────────────────────────────────

    private static FilterOperator ParseFilterOperator(string raw) =>
        Enum.TryParse<FilterOperator>(raw, ignoreCase: true, out var result)
            ? result
            : throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterValue,
                $"The filter operator is not valid. Expected one of: {Names<FilterOperator>()}.");

    private static LogicalOperator ParseLogicalOperator(string raw) =>
        Enum.TryParse<LogicalOperator>(raw, ignoreCase: true, out var result)
            ? result
            : throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterValue,
                "The logical operator of a filter group is not valid. Expected 'and' or 'or'.");

    private static SortDirection ParseSortDirection(string raw) =>
        Enum.TryParse<SortDirection>(raw, ignoreCase: true, out var result)
            ? result
            : throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterValue,
                "The sort direction is not valid. Expected 'ascending' or 'descending'.");

    // The values a caller may send, as the wire writes them (camelCase).
    private static string Names<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(JsonNamingPolicy.CamelCase.ConvertName));

    // ── JsonElement value resolution ──────────────────────────────────────────

    private static object? ResolveValue(JsonElement element, Type? targetType, string field)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        // No field metadata available — return raw text; QueryRequestValidator will reject
        if (targetType is null)
        {
            return element.GetRawText();
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => ResolveNumber(element, targetType),
            JsonValueKind.String => ResolveString(
                element.GetString()
                ?? throw new KaleidoValidationException(
                    QueryableErrorCodes.InvalidFilterValue,
                    "String JSON element has a null value."),
                targetType,
                field),
            _ => throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterValue,
                $"A value for field '{field}' is a JSON {element.ValueKind.ToString().ToLowerInvariant()}; expected a value of type '{targetType.Name}'."),
        };
    }

    private static object ResolveNumber(JsonElement element, Type targetType)
    {
        if (targetType == typeof(int) || targetType == typeof(uint) ||
            targetType == typeof(short) || targetType == typeof(ushort) ||
            targetType == typeof(byte) || targetType == typeof(sbyte))
        {
            return element.GetInt32();
        }

        if (targetType == typeof(long) || targetType == typeof(ulong))
        {
            return element.GetInt64();
        }

        if (targetType == typeof(float))
        {
            return element.GetSingle();
        }

        if (targetType == typeof(double))
        {
            return element.GetDouble();
        }

        if (targetType == typeof(decimal))
        {
            return element.GetDecimal();
        }

        return element.GetRawText();
    }

    private static object ResolveString(string raw, Type targetType, string field)
    {
        if (targetType == typeof(string))
        {
            return raw;
        }

        if (targetType == typeof(Guid))
        {
            return Guid.TryParse(raw, CultureInfo.InvariantCulture, out var guid)
                ? guid
                : throw InvalidValue(field, "a valid Guid");
        }

        if (targetType == typeof(DateOnly))
        {
            return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var d)
                ? d
                : throw InvalidValue(field, "a valid date (yyyy-MM-dd)");
        }

        if (targetType == typeof(TimeOnly))
        {
            return TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var t)
                ? t
                : throw InvalidValue(field, "a valid time");
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt
                : throw InvalidValue(field, "a valid date-time");
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
                ? dto
                : throw InvalidValue(field, "a valid date-time-offset");
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts)
                ? ts
                : throw InvalidValue(field, "a valid duration");
        }

        if (targetType.IsEnum)
        {
            return Enum.TryParse(targetType, raw, ignoreCase: true, out var enumVal)
                ? enumVal
                : throw InvalidValue(field, $"a valid '{targetType.Name}' value");
        }

        // Unknown type — return raw; QueryRequestValidator will reject with a useful error
        return raw;
    }

    private static KaleidoValidationException InvalidValue(string field, string expected) =>
        new(QueryableErrorCodes.InvalidFilterValue, $"A value for field '{field}' is not {expected}.");
}
