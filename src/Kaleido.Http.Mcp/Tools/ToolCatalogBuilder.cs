using System.Globalization;
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

internal sealed partial class ToolCatalogBuilder(
    IToolNameBuilder nameBuilder,
    IToolSchemaBuilder schemaBuilder,
    KaleidoMcpOptions options)
    : IToolCatalogBuilder
{
    /// <summary>The built-in tool that submits the user's answers to a pending information request.</summary>
    public const string AnswerInformationRequestTool = "answer_information_request";

    /// <summary>The built-in tool that reads a process's state.</summary>
    public const string GetProcessStateTool = "get_process_state";

    /// <summary>Front door: the built-in tool that finds the caller's tools by what they do.</summary>
    public const string SearchToolsTool = "search_tools";

    /// <summary>Front door: the built-in tool that returns one tool's full inputs.</summary>
    public const string DescribeToolTool = "describe_tool";

    /// <summary>Front door: the built-in tool that runs any of the caller's tools by name.</summary>
    public const string CallToolTool = "call_tool";

    /// <summary>The argument that carries the process id on step and query tools.</summary>
    public const string ProcessIdArgument = "processId";

    private const int MaxListedOutputFields = 15;

    public ToolCatalog Build(AggregatedRegistryResponse registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var processors = registry.Processes.OrderBy(x => x.ServiceName, StringComparer.Ordinal).ToArray();

        var processServices = processors
            .Where(x => (x.Steps ?? []).Count > 0)
            .Select(x => x.ServiceName)
            .ToList();

        // Queries take a processId only where processes exist: it records the query with the process.
        var context = new BuildContext { HasProcesses = processServices.Count > 0 };

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

        foreach (var processor in processors)
        {
            foreach (var step in (processor.Steps ?? []).Where(x => !x.IsInformationStep).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                AddStepTool(context, processor, step);
            }
        }

        if (processServices.Count > 0)
        {
            AddBuiltInTools(context, processServices);
        }

        var frontDoor = options.ToolListing == McpToolListing.FrontDoor;
        var unknownListed = frontDoor ? ListFrontDoor(context) : [];

        return new ToolCatalog
        {
            Tools = context.Tools,
            IsFrontDoor = frontDoor,
            UnknownListedTools = unknownListed,
            Revision = registry.Revision,
            IsPartial = registry.IsPartial || registry.ClientErrors.Count > 0,
            UnavailableServices = registry.ClientErrors.Select(x => x.ClientName).ToArray(),
            ProcessServices = processServices,
            MissingDescriptions = context.MissingDescriptions,
            TruncatedNames = context.TruncatedNames,
            UnresolvedLookups = context.UnresolvedLookups,
            NameCollisions = context.NameCollisions,
        };
    }

    // ── Query tools ──────────────────────────────────────────────────────────

    private void AddSourceTool(
        BuildContext context,
        QueryableSourceResponse source)
    {
        var name = Name(context, source.ServiceName, source.Name, "source");
        var query = QuerySchema(context, name, FieldOwner(source), source.Fields, source.Parameters, source.Pageable);

        var description =
            Describe(context, name, source.DisplayName, source.Description) +
            (string.IsNullOrWhiteSpace(source.Source) ? string.Empty : $" Data source: {source.Source}.") +
            Returns(source.OutputFields) +
            ProcessIdHint(query) +
            " Read-only.";

        context.Add(new KaleidoTool
        {
            Definition = QueryDefinition(
                name,
                source.DisplayName ?? source.Name,
                description,
                query.Schema),
            Kind = KaleidoToolKind.Query,
            Service = source.ServiceName,
            CapabilityName = source.Name,
            SourceName = source.Name,
            Url = source.QueryUrl ?? string.Empty,
            ParametersTakeProcessId = query.ParametersTakeProcessId,
        });
    }

    private void AddViewTool(
        BuildContext context,
        QueryableSourceResponse source,
        QueryableViewResponse view)
    {
        var name = Name(context, source.ServiceName, view.Name, "view");
        var query = QuerySchema(context, name, FieldOwner(source), source.Fields, view.Parameters, view.Pageable);

        var description =
            Describe(context, name, view.DisplayName, view.Description) +
            $" A view of {source.DisplayName ?? source.Name}." +
            Returns(view.OutputFields) +
            ProcessIdHint(query) +
            " Read-only.";

        context.Add(new KaleidoTool
        {
            Definition = QueryDefinition(
                name,
                view.DisplayName ?? view.Name,
                description,
                query.Schema),
            Kind = KaleidoToolKind.Query,
            Service = source.ServiceName,
            CapabilityName = view.Name,
            SourceName = source.Name,
            ViewName = view.Name,
            Url = view.QueryUrl,
            ParametersTakeProcessId = query.ParametersTakeProcessId,
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
                OpenWorldHint = false,
            },
        };

    // { processId?, searchText?, filters?, sort?, page?, parameters? } — only what the source supports.
    // processId is the adapter's argument, not the source's: it is sent as the query's process id
    // (X-Kaleido-Process-Id) so the query is recorded with the process. A source that declares its
    // own processId parameter gets it filled from the same argument, so there is one processId,
    // always at the top level.
    private QueryToolSchema QuerySchema(
        BuildContext context,
        string toolName,
        string fieldOwner,
        IReadOnlyCollection<QueryableFieldMetadata> fields,
        IReadOnlyCollection<QueryableQueryParameter> parameters,
        QueryablePagingMetadata? pageable)
    {
        var properties = new JsonObject();
        var required = new List<string>();
        var parametersRequired = false;

        var processIdParameter = parameters.FirstOrDefault(x => string.Equals(x.Name, ProcessIdArgument, StringComparison.OrdinalIgnoreCase));
        var processIdRequired = processIdParameter is not null && schemaBuilder.IsRequired(processIdParameter.Constraints);

        if (context.HasProcesses || processIdParameter is not null)
        {
            properties[ProcessIdArgument] = QueryProcessIdSchema(processIdParameter);

            if (processIdRequired)
            {
                required.Add(ProcessIdArgument);
            }
        }

        parameters = parameters.Where(x => !ReferenceEquals(x, processIdParameter)).ToArray();

        AddQueryProperties(context, fieldOwner, properties, fields, pageable);

        if (parameters.Count > 0)
        {
            (properties["parameters"], parametersRequired) = ParametersSchema(context, toolName, parameters);
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        if (parametersRequired)
        {
            required.Add("parameters");
        }

        if (required.Count > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(x => (JsonNode)x)]);
        }

        return new QueryToolSchema(schema, processIdParameter is not null, processIdRequired);
    }

    // searchText, filters, sort and page: only what the source supports.
    // fieldOwner names the query context's fields once ("service.Source"), so a source and its views
    // report a missing field description once, not once per tool.
    private static void AddQueryProperties(
        BuildContext context,
        string fieldOwner,
        JsonObject properties,
        IReadOnlyCollection<QueryableFieldMetadata> fields,
        QueryablePagingMetadata? pageable)
    {
        var searchable = fields.Where(x => x.IsSearchable).OrderBy(x => x.SearchPriority ?? int.MaxValue).ToArray();
        var filterable = fields.Where(x => x.IsFilterable && x.FilterOperators.Count > 0).ToArray();
        var sortable = fields.Where(x => x.IsSortable).ToArray();

        if (searchable.Length > 0)
        {
            properties["searchText"] = SearchTextSchema(searchable, filterable);
        }

        if (filterable.Length > 0)
        {
            properties["filters"] = FiltersSchema(filterable);
        }

        if (sortable.Length > 0)
        {
            properties["sort"] = SortSchema(sortable, [.. filterable.Concat(searchable)]);
        }

        if (pageable is not null)
        {
            properties["page"] = PageSchema(pageable);
        }

        foreach (var field in filterable.Concat(searchable).Concat(sortable).Distinct().Where(x => string.IsNullOrWhiteSpace(x.Description)))
        {
            context.ReportMissingDescription($"{fieldOwner}.{field.Name}");
        }
    }

    private static string FieldOwner(QueryableSourceResponse source) =>
        $"{source.ServiceName}.{source.Name}";

    private static JsonObject QueryProcessIdSchema(QueryableQueryParameter? declared) =>
        new()
        {
            ["type"] = "string",
            ["format"] = "uuid",
            ["description"] = declared is null
                ? "The process this query is part of, when there is one (returned by an earlier step). Sent with the query so it is recorded with the process."
                : (string.IsNullOrWhiteSpace(declared.Description) ? "The process this query is for (returned by an earlier step)." : declared.Description.Trim()) +
                  " Also sent with the query so it is recorded with the process.",
        };

    // The query's one sentence about processId, so an agent passes it without reading the schema.
    private static string ProcessIdHint(QueryToolSchema query) =>
        query.ProcessIdRequired
            ? " Scoped to one process: pass its processId."
            : query.Schema["properties"]?[ProcessIdArgument] is not null
                ? " Pass the processId of the process you are working on, when there is one."
                : string.Empty;

    private sealed record QueryToolSchema(JsonObject Schema, bool ParametersTakeProcessId, bool ProcessIdRequired);

    // Fields the filters already explain are only named here.
    private static JsonObject SearchTextSchema(IEnumerable<QueryableFieldMetadata> searchable, IReadOnlyCollection<QueryableFieldMetadata> explained) =>
        new()
        {
            ["type"] = "string",
            ["description"] =
                "Free-text search. Matches: " +
                string.Join(", ", searchable.Select(x => $"{x.Name} ({Camel(x.MatchMode?.ToString() ?? "contains")}{(explained.Contains(x) ? string.Empty : Explain(x.Description))})")) + ".",
        };

    private static JsonObject FiltersSchema(QueryableFieldMetadata[] filterable) =>
        new()
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
                    ["values"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject() },
                },
                ["required"] = new JsonArray("field", "operator"),
            },
        };

    // A field's description is said once per tool, where it first appears (filters, then search),
    // so the sort list only explains fields that are sortable and nothing else.
    private static JsonObject SortSchema(IEnumerable<QueryableFieldMetadata> sortable, IReadOnlyCollection<QueryableFieldMetadata> explained) =>
        new()
        {
            ["type"] = "array",
            ["description"] =
                "Sort order, first entry first." +
                (sortable.Where(x => !explained.Contains(x) && !string.IsNullOrWhiteSpace(x.Description)).ToArray() is { Length: > 0 } unexplained
                    ? " Fields: " + string.Join("; ", unexplained.Select(x => $"{x.Name} ({x.Description?.Trim().TrimEnd('.')})")) + "."
                    : string.Empty),
            ["items"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["field"] = new JsonObject { ["type"] = "string", ["enum"] = Strings(sortable.Select(x => x.Name)) },
                    ["direction"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("ascending", "descending") },
                },
                ["required"] = new JsonArray("field", "direction"),
            },
        };

    private static JsonObject PageSchema(QueryablePagingMetadata pageable) =>
        new()
        {
            ["type"] = "object",
            ["description"] = string.Create(CultureInfo.InvariantCulture, $"Paging. Default size {pageable.DefaultSize}; at most {pageable.MaxSize} results per page."),
            ["properties"] = new JsonObject
            {
                ["size"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = pageable.MaxSize },
                ["offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            },
        };

    // The view's parameters, and whether any of them is required.
    private (JsonObject Schema, bool HasRequired) ParametersSchema(
        BuildContext context,
        string toolName,
        IReadOnlyCollection<QueryableQueryParameter> parameters)
    {
        var parameterProperties = new JsonObject();
        var requiredParameters = new List<string>();

        foreach (var parameter in parameters)
        {
            var argument = Camel(parameter.Name);

            if (string.IsNullOrWhiteSpace(parameter.Description))
            {
                context.ReportMissingDescription($"{toolName}.parameters.{argument}");
            }

            parameterProperties[argument] = schemaBuilder.ForValue(parameter.DataType, WithSensitivity(parameter.Description, parameter.Classifications), parameter.Constraints);

            if (schemaBuilder.IsRequired(parameter.Constraints))
            {
                requiredParameters.Add(argument);
            }
        }

        var parametersSchema = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "The query's parameters.",
            ["properties"] = parameterProperties,
        };

        if (requiredParameters.Count > 0)
        {
            parametersSchema["required"] = new JsonArray([.. requiredParameters.Select(x => (JsonNode)x)]);
        }

        return (parametersSchema, requiredParameters.Count > 0);
    }

    // ── Step tools ───────────────────────────────────────────────────────────

    private void AddStepTool(
        BuildContext context,
        ProcessorRegistryResponse processor,
        ProcessStepResponse step)
    {
        var name = Name(context, processor.ServiceName, step.Name, "step");

        var lookups = Lookups(context, name, step.Fields);
        var schema = StepInputSchema(context, name, step, lookups);

        var description =
            Describe(context, name, step.DisplayName, step.Description) +
            (step.IsInitial
                ? " Can start a new process (call without processId) or continue one."
                : " Continues an existing process: pass its processId.") +
            LookupSummary(lookups) +
            Returns(step.Result?.OutputFields) +
            " Changes state." +
            Behavior(step);

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

                    // Declared by the implementer; idempotent because a non-repeatable step runs once
                    // per process (a second call is refused).
                    DestructiveHint = step.Destructive,
                    OpenWorldHint = step.External,
                    IdempotentHint = !step.Repeatable,
                },
            },
            Kind = KaleidoToolKind.Step,
            Service = processor.ServiceName,
            CapabilityName = step.Name,
            Url = step.ExecuteUrl,
            IsInitial = step.IsInitial,
            Lookups = lookups,
            ClosedBy = step.AvailableUntil.Select(x => x.Name).ToArray(),
        });
    }

    // A step's lookups, by group, resolved to the caller's query tools (already in the context:
    // query tools are built before step tools).
    private static List<ToolLookup> Lookups(
        BuildContext context,
        string toolName,
        IReadOnlyCollection<ProcessFieldMetadata> fields)
    {
        var lookups = new List<ToolLookup>();

        foreach (var group in fields.Where(x => x.Lookup is not null).GroupBy(x => x.Lookup?.Group ?? string.Empty, StringComparer.Ordinal))
        {
            var first = group.Select(x => x.Lookup).OfType<ProcessFieldLookup>().First();

            var query = context.Tools.Find(x =>
                x.Kind == KaleidoToolKind.Query &&
                string.Equals(x.Service, first.Service, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.SourceName, first.Source, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ViewName, first.View, StringComparison.OrdinalIgnoreCase));

            var inputs = group
                .Select(x => (Argument: Camel(x.Name), Field: x.Lookup?.Field ?? string.Empty))
                .ToArray();

            if (query is null)
            {
                context.UnresolvedLookups.AddRange(inputs.Select(x =>
                    $"{toolName}.{x.Argument} -> {first.Service}/{first.Source}{(first.View is null ? string.Empty : "/" + first.View)}"));
            }

            lookups.Add(new ToolLookup
            {
                Group = group.Key,
                Service = first.Service,
                Source = first.Source,
                View = first.View,
                QueryTool = query?.Definition.Name,
                QueryTitle = query?.Definition.Title,
                Inputs = inputs,
                Display = first.Display,
            });
        }

        return lookups;
    }

    // One sentence per lookup in the step's description, so an agent plans the searches first.
    private static string LookupSummary(IReadOnlyList<ToolLookup> lookups) =>
        string.Concat(lookups.Select(x =>
            x.QueryTool is { } tool
                ? $" Before calling, find {string.Join(" and ", x.Inputs.Select(i => i.Argument))} with {tool} (pass the processId when there is one): the user identifies the record and picks one result."
                : $" {string.Join(" and ", x.Inputs.Select(i => i.Argument))} come from {x.View ?? x.Source} on {x.Service}, which you cannot search: ask the user."));

    // Step fields carry [Display] hints next to the description; agents may phrase questions
    // their own way, so they are offered as hints. A lookup says exactly where the value comes from.
    private static string? FieldDescription(
        ProcessFieldMetadata field,
        string argument,
        ToolLookup? lookup)
    {
        var hints = new List<string>();

        if (lookup is { QueryTool: { } tool })
        {
            var resultField = lookup.Inputs.First(x => string.Equals(x.Argument, argument, StringComparison.Ordinal)).Field;
            var siblings = lookup.Inputs.Where(x => !string.Equals(x.Argument, argument, StringComparison.Ordinal)).Select(x => x.Argument).ToArray();

            hints.Add(
                $"Value: the {resultField} of one {lookup.QueryTitle ?? tool} result. Ask the user for details that identify the record, search with {tool}, show the matches" +
                (lookup.Display.Count > 0 ? $" ({string.Join(", ", lookup.Display)})" : string.Empty) +
                " and let the user pick one; never guess or invent it." +
                (siblings.Length > 0 ? $" Take {string.Join(" and ", siblings)} from the same result." : string.Empty));
        }
        else if (lookup is not null)
        {
            hints.Add($"Value: a {lookup.View ?? lookup.Source} result's {lookup.Inputs.First(x => string.Equals(x.Argument, argument, StringComparison.Ordinal)).Field} on {lookup.Service}, which you cannot search: ask the user.");
        }

        if (!string.IsNullOrWhiteSpace(field.DisplayName))
        {
            hints.Add($"Label: {field.DisplayName}.");
        }

        if (!string.IsNullOrWhiteSpace(field.Prompt))
        {
            hints.Add($"Ask: {field.Prompt}");
        }

        if (Sensitivity(field.Classifications) is { } sensitivity)
        {
            hints.Add(sensitivity);
        }

        var parts = new[] { field.Description?.Trim() }.Concat(hints).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    // ── Built-in tools ───────────────────────────────────────────────────────

    // The step's arguments: processId (required unless the step can start a process) and its fields.
    private JsonObject StepInputSchema(BuildContext context, string name, ProcessStepResponse step, List<ToolLookup> lookups)
    {
        var properties = new JsonObject
        {
            ["processId"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "uuid",
                ["description"] = step.IsInitial
                    ? "The process to continue. Omit it to start a new process."
                    : "The process to continue (returned by an earlier step).",
            },
        };

        var required = new List<string>();

        if (!step.IsInitial)
        {
            required.Add("processId");
        }

        foreach (var field in step.Fields)
        {
            var argument = Camel(field.Name);

            if (string.IsNullOrWhiteSpace(field.Description))
            {
                context.ReportMissingDescription($"{name}.{argument}");
            }

            var lookup = lookups.Find(x => x.Inputs.Any(i => string.Equals(i.Argument, argument, StringComparison.Ordinal)));

            properties[argument] = schemaBuilder.ForValue(field.DataType, FieldDescription(field, argument, lookup), field.Constraints);

            if (schemaBuilder.IsRequired(field.Constraints))
            {
                required.Add(argument);
            }
        }

        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };

        if (required.Count > 0)
        {
            schema["required"] = new JsonArray([.. required.Select(x => (JsonNode)x)]);
        }

        return schema;
    }

    private static void AddBuiltInTools(
        BuildContext context,
        IReadOnlyCollection<string> processServices)
    {
        var serviceSchema = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = Strings(processServices),
            ["description"] = "The service that owns the process step (the `service` reported by the last step result).",
        };

        var processIdSchema = new JsonObject
        {
            ["type"] = "string",
            ["format"] = "uuid",
            ["description"] = "The process id.",
        };

        context.Add(new KaleidoTool
        {
            Definition = new Tool
            {
                Name = AnswerInformationRequestTool,
                Title = "Answer information request",
                Description =
                    "Submits the user's answers to the questions a process asked (an informationRequest in a step result). " +
                    "Ask the user each question exactly as written and pass only the user's answers; a proposed answer, or one the user gave earlier in the conversation, counts only after the user confirms it. Never answer on the user's behalf. Changes state.",
                InputSchema = JsonSerializer.SerializeToElement(AnswerInformationRequestSchema(serviceSchema, processIdSchema)),
                Annotations = new ToolAnnotations { ReadOnlyHint = false, OpenWorldHint = false },
            },
            Kind = KaleidoToolKind.AnswerInformationRequest,
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
                        ["processId"] = processIdSchema.DeepClone(),
                    },
                    ["required"] = new JsonArray("service", "processId"),
                }),
                Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true, OpenWorldHint = false },
            },
            Kind = KaleidoToolKind.GetProcessState,
        });
    }

    private static JsonObject AnswerInformationRequestSchema(JsonObject serviceSchema, JsonObject processIdSchema) =>
        new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["service"] = serviceSchema.DeepClone(),
                ["processId"] = processIdSchema.DeepClone(),
                ["informationRequestId"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The informationRequestId of the questions being answered.",
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
                            ["values"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
                        },
                        ["required"] = new JsonArray("itemId", "values"),
                    },
                },
                [KaleidoToolInvoker.AnswersTokenArgument] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Only when a previous answer_information_request result returned further questions with an answersToken: pass it back unchanged with the answers to those questions.",
                },
            },
            ["required"] = new JsonArray("service", "processId", "informationRequestId", "answers"),
        };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string Name(
        BuildContext context,
        string service,
        string capabilityTypeName,
        string kind)
    {
        var built = nameBuilder.Build(service, capabilityTypeName);

        if (built.Truncated)
        {
            context.TruncatedNames.Add($"{service}/{capabilityTypeName} -> {built.Value}");
        }

        if (!context.Names.Contains(built.Value))
        {
            return built.Value;
        }

        // Two capabilities derive the same name (e.g. a source and its view): the name says what
        // differs - its kind - and a number is the last resort. Owners are told to rename.
        var name = WithTail(built.Value, $"_{kind}");

        for (var suffix = 2; context.Names.Contains(name); suffix++)
        {
            name = WithTail(built.Value, string.Create(CultureInfo.InvariantCulture, $"_{kind}_{suffix}"));
        }

        context.NameCollisions.Add($"{service}/{capabilityTypeName} -> {name} (derives {built.Value}, already used)");

        return name;
    }

    private static string WithTail(string name, string tail) =>
        (name.Length + tail.Length > ToolNameBuilder.MaxLength
            ? name[..(ToolNameBuilder.MaxLength - tail.Length)]
            : name) + tail;

    private static string Describe(
        BuildContext context,
        string toolName,
        string? displayName,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            context.ReportMissingDescription(toolName);
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
                QueryableQueryProperty p => Classified(p.Name, p.Classifications),
                ProcessOutputFieldMetadata p => Classified(p.Name, p.Classifications),
                _ => null,
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

    // What the implementer declared about the step's effects, for the agent deciding whether to call it.
    private static string Behavior(ProcessStepResponse step) =>
        (step.Destructive ? " Cannot be undone." : string.Empty) +
        (step.External ? " Reaches outside systems." : string.Empty) +
        (step.RequiresConfirmation ? " Ask the user to confirm before calling." : string.Empty);

    // "Sensitive (PHI, PII)." for classified values (e.g. "PriorAuth:PHI"), so an agent does not repeat
    // or store them carelessly; null when the value is not classified.
    private static string? Sensitivity(IReadOnlyList<string> classifications) =>
        classifications.Count == 0
            ? null
            : $"Sensitive ({string.Join(", ", classifications.Select(ClassificationValue))}).";

    private static string? WithSensitivity(string? description, IReadOnlyList<string> classifications) =>
        Sensitivity(classifications) is { } sensitivity
            ? string.Join(' ', new[] { description?.Trim(), sensitivity }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : description;

    private static string Classified(string name, IReadOnlyList<string> classifications) =>
        classifications.Count == 0
            ? name
            : $"{name} ({string.Join(", ", classifications.Select(ClassificationValue))})";

    private static string ClassificationValue(string classification) =>
        classification[(classification.LastIndexOf(':') + 1)..];

    private static string DescribeType(DataTypeMetadata type) =>
        type.EnumValues is { Count: > 0 } values
            ? string.Join('|', values.Select(x => Camel(x.Name)))
            : type.Format ?? type.Type;

    private static string Explain(string? description) =>
        string.IsNullOrWhiteSpace(description) ? string.Empty : $", {description.Trim().TrimEnd('.')}";

    private static string Camel(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

    private static JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Select(x => (JsonNode?)x)];

    private sealed class BuildContext
    {
        public bool HasProcesses { get; init; }

        public List<KaleidoTool> Tools { get; } = [];

        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

        public List<string> MissingDescriptions { get; } = [];

        public List<string> TruncatedNames { get; } = [];

        public List<string> UnresolvedLookups { get; } = [];

        public List<string> NameCollisions { get; } = [];

        public void Add(KaleidoTool tool)
        {
            Names.Add(tool.Definition.Name);
            Tools.Add(tool);
        }

        // Each missing description is reported once, in the order it was found.
        public void ReportMissingDescription(string what)
        {
            if (!MissingDescriptions.Contains(what, StringComparer.Ordinal))
            {
                MissingDescriptions.Add(what);
            }
        }
    }
}
