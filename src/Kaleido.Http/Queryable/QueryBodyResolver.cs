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

    // Each is one of the enum's named values: Enum.TryParse alone also accepts any number ("999"),
    // which the runtime would read as an Or group or fail to sort by (#519).
    private static FilterOperator ParseFilterOperator(string raw) =>
        ParseDefined<FilterOperator>(raw)
        ?? throw new KaleidoValidationException(
            QueryableErrorCodes.UnsupportedOperator,
            $"The filter operator is not valid. Expected one of: {Names<FilterOperator>()}.");

    private static LogicalOperator ParseLogicalOperator(string raw) =>
        ParseDefined<LogicalOperator>(raw)
        ?? throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidLogicalOperator,
            "The logical operator of a filter group is not valid. Expected 'and' or 'or'.");

    private static SortDirection ParseSortDirection(string raw) =>
        ParseDefined<SortDirection>(raw)
        ?? throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidSortDirection,
            "The sort direction is not valid. Expected 'ascending' or 'descending'.");

    private static TEnum? ParseDefined<TEnum>(string raw)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var result) && Enum.IsDefined(result) ? result : null;

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
            JsonValueKind.Number => ResolveNumber(element, targetType, field),
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

    // A number is read as the field's own type, so the whole range of an unsigned or wide type
    // reaches the query, and a number the type cannot hold is the caller's mistake (400), never an
    // exception of the reader (#519). An enum field takes a number as its underlying value; the runtime
    // checks that it names a member.
    private static object ResolveNumber(JsonElement element, Type targetType, string field)
    {
        if (targetType.IsEnum)
        {
            return ReadNumber(element, Enum.GetUnderlyingType(targetType)) is { } underlying
                ? Enum.ToObject(targetType, underlying)
                : throw InvalidValue(field, $"a valid '{targetType.Name}' value");
        }

        if (!IsNumber(targetType))
        {
            // Not a number field: the raw text, which the runtime refuses for the field's type.
            return element.GetRawText();
        }

        return ReadNumber(element, targetType)
            ?? throw InvalidValue(field, $"a number that fits '{targetType.Name}'");
    }

    private static bool IsNumber(Type type) =>
        Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
            or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    // The number as exactly this type, or null when the type cannot hold it.
    private static object? ReadNumber(JsonElement element, Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => element.TryGetByte(out var value) ? value : null,
            TypeCode.SByte => element.TryGetSByte(out var value) ? value : null,
            TypeCode.Int16 => element.TryGetInt16(out var value) ? value : null,
            TypeCode.UInt16 => element.TryGetUInt16(out var value) ? value : null,
            TypeCode.Int32 => element.TryGetInt32(out var value) ? value : null,
            TypeCode.UInt32 => element.TryGetUInt32(out var value) ? value : null,
            TypeCode.Int64 => element.TryGetInt64(out var value) ? value : null,
            TypeCode.UInt64 => element.TryGetUInt64(out var value) ? value : null,
            TypeCode.Single => element.TryGetSingle(out var value) && float.IsFinite(value) ? value : null,
            TypeCode.Double => element.TryGetDouble(out var value) && double.IsFinite(value) ? value : null,
            TypeCode.Decimal => element.TryGetDecimal(out var value) ? value : null,
            _ => null,
        };

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
