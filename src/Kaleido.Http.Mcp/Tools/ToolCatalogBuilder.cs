using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Translates a caller-filtered registry into MCP tools:
/// <list type="bullet">
/// <item><description>every directly queryable source and every view → a read-only query tool;</description></item>
/// <item><description>every step that is not an information step → a step tool;</description></item>
/// <item><description>when processes exist → <c>answer_information_request</c> and <c>get_process_state</c>.</description></item>
/// </list>
/// Information steps are never tools: they are only reachable as a process's required step,
/// with their questions, and are answered by the user.
/// </summary>
internal interface IToolCatalogBuilder
{
    ToolCatalog Build(AggregatedRegistryResponse registry);
}

internal sealed class ToolCatalogBuilder(
    IToolNameBuilder nameBuilder,
    IToolSchemaBuilder schemaBuilder)
    : IToolCatalogBuilder
{
    /// <summary>The built-in tool that submits the user's answers to a pending information request.</summary>
    public const string AnswerInformationRequestTool = "answer_information_request";

    /// <summary>The built-in tool that reads a process's state.</summary>
    public const string GetProcessStateTool = "get_process_state";

    private const int MaxListedOutputFields = 15;

    public ToolCatalog Build(AggregatedRegistryResponse registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var context = new BuildContext();

        foreach (var source in registry.Queryables.OrderBy(x => x.ServiceName, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(source.QueryUrl))
            {
                AddSourceTool(context, source);
            }

            foreach (var view in source.Views.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                AddViewTool(context, source, view);
            }
        }

        var processServices = new List<string>();

        foreach (var processor in registry.Processes.OrderBy(x => x.ServiceName, StringComparer.Ordinal))
        {
            var steps = processor.Steps ?? [];

            if (steps.Count > 0)
            {
                processServices.Add(processor.ServiceName);
            }

            foreach (var step in steps.Where(x => !x.IsInformationStep).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                AddStepTool(context, processor, step);
            }
        }

        if (processServices.Count > 0)
        {
            AddBuiltInTools(context, processServices);
        }

        return new ToolCatalog
        {
            Tools = context.Tools,
            Revision = registry.Revision,
            IsPartial = registry.IsPartial || registry.ClientErrors.Count > 0,
            UnavailableServices = registry.ClientErrors.Select(x => x.ClientName).ToArray(),
            ProcessServices = processServices,
            MissingDescriptions = context.MissingDescriptions,
            TruncatedNames = context.TruncatedNames
        };
    }

    // ── Query tools ──────────────────────────────────────────────────────────

    private void AddSourceTool(
        BuildContext context,
        QueryableSourceResponse source)
    {
        var name = Name(context, source.ServiceName, source.Name);

        var description =
            Describe(context, name, source.DisplayName, source.Description) +
            (string.IsNullOrWhiteSpace(source.Source) ? string.Empty : $" Data source: {source.Source}.") +
            Returns(source.OutputFields) +
            " Read-only.";

        context.Add(new KaleidoTool
        {
            Definition = QueryDefinition(
                name,
                source.DisplayName ?? source.Name,
                description,
                QuerySchema(context, name, source.Fields, source.Parameters, source.Pageable)),
            Kind = KaleidoToolKind.Query,
            Service = source.ServiceName,
            CapabilityName = source.Name,
            Url = source.QueryUrl ?? string.Empty
        });
    }

    private void AddViewTool(
        BuildContext context,
        QueryableSourceResponse source,
        QueryableViewResponse view)
    {
        var name = Name(context, source.ServiceName, view.Name);

        var description =
            Describe(context, name, view.DisplayName, view.Description) +
            $" A view of {source.DisplayName ?? source.Name}." +
            Returns(view.OutputFields) +
            " Read-only.";

        context.Add(new KaleidoTool
        {
            Definition = QueryDefinition(
                name,
                view.DisplayName ?? view.Name,
                description,
                QuerySchema(context, name, source.Fields, view.Parameters, view.Pageable)),
            Kind = KaleidoToolKind.Query,
            Service = source.ServiceName,
            CapabilityName = view.Name,
            Url = view.QueryUrl
        });
    }

    private static Tool QueryDefinition(
        string name,
        string title,
        string description,
        JsonObject schema) =>
        new()
        {
            Name = name,
            Title = title,
            Description = description,
            InputSchema = JsonSerializer.SerializeToElement(schema),
            Annotations = new ToolAnnotations
            {
                Title = title,
                ReadOnlyHint = true,
                IdempotentHint = true,
                DestructiveHint = false,
                OpenWorldHint = false
            }
        };

    // { searchText?, filters?, sort?, page?, parameters? } — only what the source supports.
    private JsonObject QuerySchema(
        BuildContext context,
        string toolName,
        IReadOnlyCollection<QueryableFieldMetadata> fields,
        IReadOnlyCollection<QueryableQueryParameter> parameters,
        PageableMetadata? pageable)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        var searchable = fields.Where(x => x.IsSearchable).OrderBy(x => x.SearchPriority ?? int.MaxValue).ToArray();

        if (searchable.Length > 0)
        {
            properties["searchText"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] =
                    "Free-text search. Matches: " +
                    string.Join(", ", searchable.Select(x => $"{x.Name} ({Camel(x.MatchMode?.ToString() ?? "contains")})")) + "."
            };
        }

        var filterable = fields.Where(x => x.IsFilterable && x.FilterOperators.Count > 0).ToArray();

        if (filterable.Length > 0)
        {
            properties["filters"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] =
                    "Conditions, all of which must match. Fields and their operators: " +
                    string.Join("; ", filterable.Select(x => $"{x.Name} ({DescribeType(x.DataType)}{Explain(x.Description)}): {string.Join(", ", x.FilterOperators.Select(o => Camel(o.ToString())))}")) +
                    ". Pass values typed like the field (in/between take several values; isNull/isTrue take none).",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["field"] = new JsonObject { ["type"] = "string", ["enum"] = Strings(filterable.Select(x => x.Name)) },
                        ["operator"] = new JsonObject { ["type"] = "string", ["enum"] = Strings(filterable.SelectMany(x => x.FilterOperators).Select(x => Camel(x.ToString())).Distinct(StringComparer.Ordinal)) },
                        ["values"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject() }
                    },
                    ["required"] = new JsonArray("field", "operator")
                }
            };
        }

        var sortable = fields.Where(x => x.IsSortable).ToArray();

        if (sortable.Length > 0)
        {
            properties["sort"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Sort order, first entry first.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["field"] = new JsonObject { ["type"] = "string", ["enum"] = Strings(sortable.Select(x => x.Name)) },
                        ["direction"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("ascending", "descending") }
                    },
                    ["required"] = new JsonArray("field", "direction")
                }
            };
        }

        if (pageable is not null)
        {
            properties["page"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = $"Paging. Default size {pageable.DefaultSize}; at most {pageable.MaxSize} results per page.",
                ["properties"] = new JsonObject
                {
                    ["size"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = pageable.MaxSize },
                    ["offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 }
                }
            };
        }

        if (parameters.Count > 0)
        {
            var parameterProperties = new JsonObject();
            var requiredParameters = new JsonArray();

            foreach (var parameter in parameters)
            {
                var argument = Camel(parameter.Name);

                if (string.IsNullOrWhiteSpace(parameter.Description))
                {
                    context.MissingDescriptions.Add($"{toolName}.parameters.{argument}");
                }

                parameterProperties[argument] = schemaBuilder.ForValue(parameter.DataType, parameter.Description, parameter.Constraints);

                if (schemaBuilder.IsRequired(parameter.Constraints))
                {
                    requiredParameters.Add(argument);
                }
            }

            var parametersSchema = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "The query's parameters.",
                ["properties"] = parameterProperties
            };

            if (requiredParameters.Count > 0)
            {
                parametersSchema["required"] = requiredParameters;
                required.Add("parameters");
            }

            properties["parameters"] = parametersSchema;
        }

        foreach (var field in filterable.Concat(searchable).Concat(sortable).Distinct())
        {
            if (string.IsNullOrWhiteSpace(field.Description))
            {
                context.MissingDescriptions.Add($"{toolName}.{field.Name}");
            }
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    // ── Step tools ───────────────────────────────────────────────────────────

    private void AddStepTool(
        BuildContext context,
        ProcessorRegistryResponse processor,
        ProcessStepResponse step)
    {
        var name = Name(context, processor.ServiceName, step.Name);

        var properties = new JsonObject
        {
            ["processId"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "uuid",
                ["description"] = step.IsInitial
                    ? "The process to continue. Omit it to start a new process."
                    : "The process to continue (returned by an earlier step)."
            }
        };

        var required = new JsonArray();

        if (!step.IsInitial)
        {
            required.Add("processId");
        }

        foreach (var field in step.Fields)
        {
            var argument = Camel(field.Name);

            if (string.IsNullOrWhiteSpace(field.Description))
            {
                context.MissingDescriptions.Add($"{name}.{argument}");
            }

            properties[argument] = schemaBuilder.ForValue(field.DataType, FieldDescription(field), field.Constraints);

            if (schemaBuilder.IsRequired(field.Constraints))
            {
                required.Add(argument);
            }
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        var description =
            Describe(context, name, step.DisplayName, step.Description) +
            (step.IsInitial
                ? " Can start a new process (call without processId) or continue one."
                : " Continues an existing process: pass its processId.") +
            Returns(step.Result?.OutputFields) +
            " Changes state.";

        context.Add(new KaleidoTool
        {
            Definition = new Tool
            {
                Name = name,
                Title = step.DisplayName ?? step.Name,
                Description = description,
                InputSchema = JsonSerializer.SerializeToElement(schema),
                Annotations = new ToolAnnotations
                {
                    Title = step.DisplayName ?? step.Name,
                    ReadOnlyHint = false,
                    OpenWorldHint = false
                }
            },
            Kind = KaleidoToolKind.Step,
            Service = processor.ServiceName,
            CapabilityName = step.Name,
            Url = step.ExecuteUrl,
            IsInitial = step.IsInitial
        });
    }

    // Step fields carry [Display] hints next to the description; agents may phrase questions
    // their own way, so they are offered as hints.
    private static string? FieldDescription(ProcessFieldMetadata field)
    {
        var hints = new List<string>();

        if (!string.IsNullOrWhiteSpace(field.DisplayName))
        {
            hints.Add($"Label: {field.DisplayName}.");
        }

        if (!string.IsNullOrWhiteSpace(field.Prompt))
        {
            hints.Add($"Ask: {field.Prompt}");
        }

        var parts = new[] { field.Description?.Trim() }.Concat(hints).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return parts.Length == 0 ? null : string.Join(" ", parts);
    }

    // ── Built-in tools ───────────────────────────────────────────────────────

    private static void AddBuiltInTools(
        BuildContext context,
        IReadOnlyCollection<string> processServices)
    {
        var serviceSchema = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = Strings(processServices),
            ["description"] = "The service that owns the process step (the `service` reported by the last step result)."
        };

        var processIdSchema = new JsonObject
        {
            ["type"] = "string",
            ["format"] = "uuid",
            ["description"] = "The process id."
        };

        context.Add(new KaleidoTool
        {
            Definition = new Tool
            {
                Name = AnswerInformationRequestTool,
                Title = "Answer information request",
                Description =
                    "Submits the user's answers to the questions a process asked (an informationRequest in a step result). " +
                    "Ask the user each question exactly as written and pass only the user's answers; never answer on the user's behalf. Changes state.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["service"] = serviceSchema.DeepClone(),
                        ["processId"] = processIdSchema.DeepClone(),
                        ["informationRequestId"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The informationRequestId of the questions being answered."
                        },
                        ["answers"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "One entry per question: the question's id and the user's answer(s). Choices use the option values; booleans true/false; dates yyyy-MM-dd.",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["itemId"] = new JsonObject { ["type"] = "string" },
                                    ["values"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
                                },
                                ["required"] = new JsonArray("itemId", "values")
                            }
                        }
                    },
                    ["required"] = new JsonArray("service", "processId", "informationRequestId", "answers")
                }),
                Annotations = new ToolAnnotations { ReadOnlyHint = false, OpenWorldHint = false }
            },
            Kind = KaleidoToolKind.AnswerInformationRequest
        });

        context.Add(new KaleidoTool
        {
            Definition = new Tool
            {
                Name = GetProcessStateTool,
                Title = "Get process state",
                Description = "Reads a process: its state, the next steps (as tool names) and any questions waiting for the user. Read-only.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["service"] = serviceSchema.DeepClone(),
                        ["processId"] = processIdSchema.DeepClone()
                    },
                    ["required"] = new JsonArray("service", "processId")
                }),
                Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true, OpenWorldHint = false }
            },
            Kind = KaleidoToolKind.GetProcessState
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string Name(
        BuildContext context,
        string service,
        string capabilityTypeName)
    {
        var built = nameBuilder.Build(service, capabilityTypeName);
        var name = built.Value;

        if (built.Truncated)
        {
            context.TruncatedNames.Add($"{service}/{capabilityTypeName} -> {name}");
        }

        // Two capabilities that derive the same name (e.g. a source and a view) stay distinct.
        for (var suffix = 2; context.Names.Contains(name); suffix++)
        {
            var tail = $"_{suffix}";
            name = (built.Value.Length + tail.Length > ToolNameBuilder.MaxLength
                ? built.Value[..(ToolNameBuilder.MaxLength - tail.Length)]
                : built.Value) + tail;
        }

        return name;
    }

    private static string Describe(
        BuildContext context,
        string toolName,
        string? displayName,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            context.MissingDescriptions.Add(toolName);
        }

        var text = string.Join(
            ": ",
            new[] { displayName?.Trim(), description?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return text.EndsWith('.') ? text : text + ".";
    }

    private static string Returns<TField>(IReadOnlyCollection<TField>? fields)
        where TField : class
    {
        var names = fields?
            .Select(x => x switch
            {
                QueryableQueryProperty p => p.Name,
                ProcessOutputFieldMetadata p => p.Name,
                _ => null
            })
            .OfType<string>()
            .ToArray() ?? [];

        if (names.Length == 0)
        {
            return string.Empty;
        }

        var listed = names.Take(MaxListedOutputFields).ToArray();

        return $" Returns: {string.Join(", ", listed)}{(names.Length > listed.Length ? ", …" : string.Empty)}.";
    }

    private static string DescribeType(DataTypeDescriptor type) =>
        type.EnumValues is { Count: > 0 } values
            ? string.Join("|", values.Select(x => Camel(x.Name)))
            : type.Format ?? type.Type;

    private static string Explain(string? description) =>
        string.IsNullOrWhiteSpace(description) ? string.Empty : $", {description.Trim().TrimEnd('.')}";

    private static string Camel(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Select(x => (JsonNode?)x).ToArray());

    private sealed class BuildContext
    {
        public List<KaleidoTool> Tools { get; } = [];

        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

        public List<string> MissingDescriptions { get; } = [];

        public List<string> TruncatedNames { get; } = [];

        public void Add(KaleidoTool tool)
        {
            Names.Add(tool.Definition.Name);
            Tools.Add(tool);
        }
    }
}
