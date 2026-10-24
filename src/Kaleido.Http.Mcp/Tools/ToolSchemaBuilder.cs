using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Maps Kaleido's published input metadata to JSON Schema, the language MCP tools use to tell
/// the model which arguments exist, their types and their limits.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>dataType</c> → <c>"type"</c> / <c>"format"</c> / <c>"enum"</c> / <c>"items"</c>
/// (so the model sends valid values: a uuid, an ISO date, one of the enum names).</description></item>
/// <item><description><c>constraints</c> → <c>"required"</c>, <c>"minLength"</c>/<c>"maxLength"</c>,
/// <c>"minimum"</c>/<c>"maximum"</c>, <c>"pattern"</c>, <c>"format"</c> (so the model fills what is needed and
/// stays inside the limits the service validates).</description></item>
/// <item><description><c>description</c> (and step field <c>displayName</c>/<c>prompt</c> hints) →
/// <c>description</c>: what the argument means. Undescribed inputs leave the model guessing from the
/// name, which is why KAL2014/KAL2015 ask for descriptions.</description></item>
/// </list>
/// </remarks>
internal interface IToolSchemaBuilder
{
    /// <summary>The schema of one value of <paramref name="dataType"/>.</summary>
    JsonObject ForValue(
        DataTypeMetadata dataType,
        string? description = null,
        IReadOnlyCollection<ConstraintMetadata>? constraints = null);

    /// <summary>True when the constraints mark the value as required.</summary>
    bool IsRequired(IReadOnlyCollection<ConstraintMetadata>? constraints);
}

internal sealed class ToolSchemaBuilder : IToolSchemaBuilder
{
    public JsonObject ForValue(
        DataTypeMetadata dataType,
        string? description = null,
        IReadOnlyCollection<ConstraintMetadata>? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(dataType);

        var schema = TypeSchema(dataType);

        if (!string.IsNullOrWhiteSpace(description))
        {
            schema["description"] = description.Trim();
        }

        foreach (var constraint in constraints ?? [])
        {
            ApplyConstraint(schema, constraint);
        }

        return schema;
    }

    public bool IsRequired(IReadOnlyCollection<ConstraintMetadata>? constraints) =>
        constraints?.Any(x => string.Equals(x.Type, "Required", StringComparison.OrdinalIgnoreCase)) == true;

    private static JsonObject TypeSchema(DataTypeMetadata dataType)
    {
        if (dataType.EnumValues is { Count: > 0 } values)
        {
            // Kaleido's wire format serializes enums as camelCase names.
            var schema = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(values.Select(x => (JsonNode?)JsonNamingPolicy.CamelCase.ConvertName(x.Name)).ToArray()),
            };

            var described = values.Where(x => !string.IsNullOrWhiteSpace(x.Description)).ToArray();

            if (described.Length > 0)
            {
                schema["description"] = string.Join(
                    "; ",
                    described.Select(x => $"{JsonNamingPolicy.CamelCase.ConvertName(x.Name)}: {x.Description}"));
            }

            return schema;
        }

        switch (dataType.Type)
        {
            case "array":
                return new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = dataType.ItemType is { } item ? TypeSchema(item) : AnyValueSchema(),
                };

            case "integer":
            case "number":
            case "boolean":
                return new JsonObject { ["type"] = dataType.Type };

            case "string":
                return StringSchema(dataType.Format);

            // A dictionary: its keys are the property names, each value of the item type (#446).
            case "object" when dataType.ItemType is { } value:
                return new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = TypeSchema(value),
                };

            default:
                // object or anything unknown: accept any JSON value of that shape.
                return string.IsNullOrWhiteSpace(dataType.Type)
                    ? AnyValueSchema()
                    : new JsonObject { ["type"] = dataType.Type };
        }
    }

    // The empty JSON Schema object: it accepts any value.
    private static JsonObject AnyValueSchema() => [];

    private static JsonObject StringSchema(string? format)
    {
        var schema = new JsonObject { ["type"] = "string" };

        switch (format)
        {
            case "uuid":
            case "date":
            case "time":
                schema["format"] = format;
                break;

            case "date-time":
            case "date-time-offset":
                schema["format"] = "date-time";
                break;

            case "duration":
                schema["format"] = "duration";
                schema["description"] = "An ISO 8601 duration, e.g. PT30M.";
                break;
        }

        return schema;
    }

    private static void ApplyConstraint(
        JsonObject schema,
        ConstraintMetadata constraint)
    {
        switch (constraint.Type)
        {
            case "StringLength":
                SetNumber(schema, "maxLength", Parameter(constraint, "MaximumLength"));
                SetNumber(schema, "minLength", Parameter(constraint, "MinimumLength"), skipZero: true);
                break;

            case "MaxLength":
                SetNumber(schema, IsArray(schema) ? "maxItems" : "maxLength", Parameter(constraint, "Length"));
                break;

            case "MinLength":
                SetNumber(schema, IsArray(schema) ? "minItems" : "minLength", Parameter(constraint, "Length"));
                break;

            case "Range":
                SetNumber(schema, "minimum", Parameter(constraint, "Minimum"));
                SetNumber(schema, "maximum", Parameter(constraint, "Maximum"));
                break;

            case "RegularExpression":
                if (Parameter(constraint, "Pattern") is { } pattern && !string.IsNullOrWhiteSpace(pattern))
                {
                    schema["pattern"] = pattern;
                }

                break;

            case "EmailAddress":
                schema["format"] = "email";
                break;

            case "Url":
                schema["format"] = "uri";
                break;

            case "Required" when IsString(schema):
                // [Required] on a string rejects empty text.
                schema["minLength"] ??= 1;
                break;
        }
    }

    // Constraint values are objects in-process and JsonElements after a registry round trip.
    private static string? Parameter(
        ConstraintMetadata constraint,
        string name)
    {
        var value =
            constraint.Parameters
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?
                .Value;

        return value switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement element => element.GetRawText(),
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }

    private static void SetNumber(
        JsonObject schema,
        string keyword,
        string? value,
        bool skipZero = false)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
            (skipZero && number == 0))
        {
            return;
        }

        schema[keyword] = decimal.Truncate(number) == number
            ? JsonValue.Create((long)number)
            : JsonValue.Create(number);
    }

    private static bool IsArray(JsonObject schema) =>
        string.Equals(schema["type"]?.GetValue<string>(), "array", StringComparison.Ordinal);

    private static bool IsString(JsonObject schema) =>
        string.Equals(schema["type"]?.GetValue<string>(), "string", StringComparison.Ordinal);
}
