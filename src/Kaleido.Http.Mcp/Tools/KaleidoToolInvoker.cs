using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.Http;
using Kaleido.Processor;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Runs a tool call against Kaleido and turns the outcome into what the model reads.
/// </summary>
/// <remarks>
/// <para><b>Queries</b> post the query body to the source or view and return its result.</para>
/// <para><b>Steps</b> post the step to its execute URL (process id in <c>X-Kaleido-Process-Id</c>).
/// The result is summarized for the model: the outcome, the business messages (verbatim), and
/// <c>next</c>: the required tool, or the available tools, of the service the process continues on.
/// After a handoff the target service's state is read, so <c>next</c> names that service's tools.</para>
/// <para><b>Questions</b> (a required information step): when the client supports elicitation, the
/// user answers them in a form and the answers are submitted directly, round after round. Otherwise
/// the questions are returned in <c>next.informationRequest</c> for the agent to ask the user and
/// submit with <c>answer_information_request</c>.</para>
/// </remarks>
internal interface IKaleidoToolInvoker
{
    Task<CallToolResult> InvokeAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        McpServer server,
        CancellationToken cancellationToken = default);
}

internal sealed class KaleidoToolInvoker(
    IKaleidoApiClient apiClient,
    IInformationRequestElicitor elicitor,
    KaleidoMcpOptions options)
    : IKaleidoToolInvoker
{
    public Task<CallToolResult> InvokeAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(server);

        return tool.Kind switch
        {
            KaleidoToolKind.Query => QueryAsync(tool, arguments, cancellationToken),
            KaleidoToolKind.Step => StepAsync(tool, arguments, catalog, server, cancellationToken),
            KaleidoToolKind.AnswerInformationRequest => AnswerAsync(arguments, catalog, server, cancellationToken),
            KaleidoToolKind.GetProcessState => StateAsync(arguments, catalog, cancellationToken),
            _ => Task.FromResult(Error(McpErrorCodes.UnknownTool, $"Tool '{tool.Definition.Name}' has no handler."))
        };
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    private async Task<CallToolResult> QueryAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var query = new JsonObject();

        if (Text(arguments, "searchText") is { } searchText)
        {
            query["searchText"] = searchText;
        }

        if (arguments.TryGetValue("filters", out var filters) && filters.ValueKind == JsonValueKind.Array)
        {
            var conditions = filters.EnumerateArray()
                .Select(x => (JsonNode?)new JsonObject { ["condition"] = JsonNode.Parse(x.GetRawText()) })
                .ToArray();

            if (conditions.Length == 1)
            {
                query["filter"] = conditions[0];
            }
            else if (conditions.Length > 1)
            {
                query["filter"] = new JsonObject
                {
                    ["group"] = new JsonObject { ["operator"] = "and", ["filters"] = new JsonArray(conditions) }
                };
            }
        }

        CopyArgument(arguments, "sort", query);
        CopyArgument(arguments, "page", query);

        var body = new JsonObject { ["query"] = query };

        CopyArgument(arguments, "parameters", body);

        var response = await apiClient.PostAsync(tool.Url, body, cancellationToken: cancellationToken);

        return response.IsSuccess
            ? Success(response.Body ?? new JsonObject())
            : ServiceError(tool.Definition.Name, response);
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    private async Task<CallToolResult> StepAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        McpServer server,
        CancellationToken cancellationToken)
    {
        Guid? processId = null;

        if (Text(arguments, "processId") is { } text)
        {
            if (!Guid.TryParse(text, out var parsed))
            {
                return Error(McpErrorCodes.InvalidArguments, $"processId '{text}' is not a valid id.");
            }

            processId = parsed;
        }
        else if (!tool.IsInitial)
        {
            return Error(
                McpErrorCodes.InvalidArguments,
                $"{tool.Definition.Name} continues an existing process: pass the processId returned by an earlier step.");
        }

        var step = new JsonObject();

        foreach (var (name, value) in arguments.Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase)))
        {
            step[name] = JsonNode.Parse(value.GetRawText());
        }

        var response =
            await apiClient.PostAsync(
                tool.Url,
                new JsonObject { ["processStep"] = step },
                processId,
                cancellationToken);

        return await FollowUpAsync(tool.Service, response, catalog, server, cancellationToken);
    }

    private async Task<CallToolResult> AnswerAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        McpServer server,
        CancellationToken cancellationToken)
    {
        if (Text(arguments, "service") is not { } service ||
            !Guid.TryParse(Text(arguments, "processId"), out var processId) ||
            Text(arguments, "informationRequestId") is not { } informationRequestId)
        {
            return Error(McpErrorCodes.InvalidArguments, "Pass service, processId, informationRequestId and answers.");
        }

        var state = await apiClient.GetAsync(StatePath(service, processId), cancellationToken);

        if (!state.IsSuccess)
        {
            return ServiceError(ToolCatalogBuilder.AnswerInformationRequestTool, state);
        }

        if (Deserialize<ProcessStateResponse>(state.Body)?.RequiredStep is not { InformationRequest: { } pending } required)
        {
            return Error(
                McpErrorCodes.NoPendingInformationRequest,
                $"Process {processId} on '{service}' is not waiting for answers. Use get_process_state to see what it needs.");
        }

        if (!string.Equals(pending.InformationRequestId, informationRequestId, StringComparison.Ordinal))
        {
            return Error(
                McpErrorCodes.InvalidArguments,
                $"The process is waiting for answers to '{pending.InformationRequestId}', not '{informationRequestId}'. Ask the user the current questions.",
                Serialize(pending));
        }

        var items = new JsonArray();

        if (arguments.TryGetValue("answers", out var answers) && answers.ValueKind == JsonValueKind.Array)
        {
            foreach (var answer in answers.EnumerateArray())
            {
                var values = answer.TryGetProperty("values", out var given) && given.ValueKind == JsonValueKind.Array
                    ? given.EnumerateArray().Select(x => (JsonNode?)new JsonObject { ["value"] = x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText() }).ToArray()
                    : [];

                items.Add(new JsonObject
                {
                    ["itemId"] = answer.TryGetProperty("itemId", out var itemId) ? itemId.GetString() : null,
                    ["answers"] = new JsonArray(values)
                });
            }
        }

        var response =
            await apiClient.PostAsync(
                required.ExecuteUrl,
                new JsonObject
                {
                    ["processStep"] = new JsonObject
                    {
                        ["informationRequestId"] = informationRequestId,
                        ["items"] = items
                    }
                },
                processId,
                cancellationToken);

        return await FollowUpAsync(service, response, catalog, server, cancellationToken);
    }

    private async Task<CallToolResult> StateAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (Text(arguments, "service") is not { } service ||
            !Guid.TryParse(Text(arguments, "processId"), out var processId))
        {
            return Error(McpErrorCodes.InvalidArguments, "Pass service and processId.");
        }

        var response = await apiClient.GetAsync(StatePath(service, processId), cancellationToken);

        if (!response.IsSuccess)
        {
            return ServiceError(ToolCatalogBuilder.GetProcessStateTool, response);
        }

        var state = Deserialize<ProcessStateResponse>(response.Body);

        if (state is null)
        {
            return Error(McpErrorCodes.ServiceError, $"The state of process {processId} could not be read.");
        }

        if (!string.IsNullOrWhiteSpace(state.TargetProcessorName))
        {
            return await StateAsync(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["service"] = JsonSerializer.SerializeToElement(state.TargetProcessorName),
                    ["processId"] = JsonSerializer.SerializeToElement(processId)
                },
                catalog,
                cancellationToken);
        }

        var summary = new JsonObject
        {
            ["processId"] = processId.ToString(),
            ["service"] = service,
            ["state"] = Camel(state.State.ToString()),
            ["completedSteps"] = new JsonArray(state.Steps
                .Where(x => x.Status == StepExecutionStatus.Completed)
                .Select(x => (JsonNode?)x.StepName)
                .ToArray())
        };

        summary["next"] = Next(service, state.RequiredStep, state.AvailableSteps, catalog, elicited: false);

        return Success(summary);
    }

    // After a step: follow a handoff to the target's state, answer questions through
    // elicitation while the client supports it, and summarize where the process stands.
    private async Task<CallToolResult> FollowUpAsync(
        string service,
        KaleidoApiResponse response,
        ToolCatalog catalog,
        McpServer server,
        CancellationToken cancellationToken)
    {
        var answered = new JsonArray();
        string? note = null;

        for (var round = 0; ; round++)
        {
            if (!response.IsSuccess)
            {
                return ServiceError(service, response);
            }

            var step = Deserialize<StepExecutionResponse<JsonElement>>(response.Body);

            if (step is null)
            {
                return Error(McpErrorCodes.ServiceError, "The step response could not be read.", response.Body);
            }

            var pendingService = service;
            var required = step.RequiredStep;
            IReadOnlyCollection<ProcessStepSummary> available = step.AvailableSteps;

            if (!string.IsNullOrWhiteSpace(step.TargetProcessorName))
            {
                pendingService = step.TargetProcessorName;

                var target = await apiClient.GetAsync(StatePath(pendingService, step.ProcessId), cancellationToken);
                var targetState = target.IsSuccess ? Deserialize<ProcessStateResponse>(target.Body) : null;

                required = targetState?.RequiredStep;
                available = targetState?.AvailableSteps ?? [];
            }

            if (required?.InformationRequest is { } pending &&
                server.ClientCapabilities?.Elicitation is not null &&
                step.Outcome == StepExecutionOutcome.Completed)
            {
                if (round >= options.MaxElicitationRounds)
                {
                    note = "The process asked more rounds of questions than are answered automatically; ask the user the remaining questions.";
                }
                else
                {
                    var elicited = await server.ElicitAsync(elicitor.ToElicitation(pending), cancellationToken);

                    if (elicited.IsAccepted && elicited.Content is { } content)
                    {
                        var items = elicitor.ToAnswers(pending, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

                        answered.Add(new JsonObject
                        {
                            ["step"] = required.Name,
                            ["informationRequestId"] = pending.InformationRequestId
                        });

                        service = pendingService;
                        response =
                            await apiClient.PostAsync(
                                required.ExecuteUrl,
                                new JsonObject
                                {
                                    ["processStep"] = Serialize(new { pending.InformationRequestId, Items = items })
                                },
                                step.ProcessId,
                                cancellationToken);

                        continue;
                    }

                    note = $"The user did not answer the questions ({elicited.Action}). Nothing was submitted; the process is still waiting for them.";
                }
            }

            return Success(Summary(service, pendingService, step, required, available, answered, note, catalog));
        }
    }

    // ── Summaries ────────────────────────────────────────────────────────────

    private static JsonObject Summary(
        string service,
        string pendingService,
        StepExecutionResponse<JsonElement> step,
        ProcessRequiredStep? required,
        IReadOnlyCollection<ProcessStepSummary> available,
        JsonArray answered,
        string? note,
        ToolCatalog catalog)
    {
        var summary = new JsonObject
        {
            ["processId"] = step.ProcessId.ToString(),
            ["service"] = service,
            ["step"] = step.StepName,
            ["outcome"] = Camel(step.Outcome.ToString())
        };

        if (step.Result.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            summary["result"] = JsonNode.Parse(step.Result.GetRawText());
        }

        var messages = step.BusinessMessages.Concat(step.FrameworkMessages).ToArray();

        if (messages.Length > 0)
        {
            summary["messages"] = Serialize(messages);
        }

        if (answered.Count > 0)
        {
            summary["answered"] = answered;
        }

        if (!string.IsNullOrWhiteSpace(step.TargetProcessorName))
        {
            summary["handoff"] = step.TargetProcessorName;
        }

        summary["next"] = Next(pendingService, required, available, catalog, elicited: answered.Count > 0);

        if (note is not null)
        {
            summary["note"] = note;
        }

        return summary;
    }

    private static JsonObject Next(
        string service,
        ProcessRequiredStep? required,
        IReadOnlyCollection<ProcessStepSummary> available,
        ToolCatalog catalog,
        bool elicited)
    {
        var next = new JsonObject { ["service"] = service };

        if (required is { InformationRequest: { } pending })
        {
            next["required"] = new JsonObject
            {
                ["tool"] = ToolCatalogBuilder.AnswerInformationRequestTool,
                ["step"] = required.Name,
                ["displayName"] = required.DisplayName
            };
            next["informationRequest"] = Serialize(pending);
            next["instruction"] =
                (elicited ? "More questions followed. " : string.Empty) +
                "Ask the user every question exactly as written and submit only the user's answers with answer_information_request (service, processId, informationRequestId, answers). Never answer for the user.";
        }
        else if (required is not null)
        {
            next["required"] = catalog.FindByUrl(required.ExecuteUrl) is { } tool
                ? new JsonObject { ["tool"] = tool.Definition.Name, ["step"] = required.Name, ["displayName"] = required.DisplayName }
                : new JsonObject { ["step"] = required.Name, ["displayName"] = required.DisplayName, ["note"] = "You are not allowed to run this step." };
        }

        var tools = available
            .Select(x => catalog.FindByUrl(x.ExecuteUrl) is { } tool
                ? (JsonNode?)new JsonObject { ["tool"] = tool.Definition.Name, ["displayName"] = x.DisplayName ?? x.Name }
                : null)
            .OfType<JsonNode>()
            .ToArray();

        next["available"] = new JsonArray(tools);

        if (required is null && tools.Length == 0)
        {
            next["note"] = "No further steps: the process is complete here.";
        }

        return next;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string StatePath(string service, Guid processId) =>
        $"/{service.Trim().Trim('/')}/processes/{processId}";

    private static string? Text(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name) =>
        arguments.TryGetValue(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static void CopyArgument(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        JsonObject target)
    {
        if (arguments.TryGetValue(name, out var value) &&
            value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            target[name] = JsonNode.Parse(value.GetRawText());
        }
    }

    private static T? Deserialize<T>(JsonNode? body)
        where T : class =>
        body?.Deserialize<T>(KaleidoJsonOptions.Options);

    private static JsonNode? Serialize<T>(T value) =>
        JsonSerializer.SerializeToNode(value, KaleidoJsonOptions.Options);

    private static string Camel(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

    private static CallToolResult Success(JsonNode body) =>
        new()
        {
            Content = [new TextContentBlock { Text = body.ToJsonString(KaleidoJsonOptions.Options) }]
        };

    private static CallToolResult ServiceError(
        string source,
        KaleidoApiResponse response) =>
        Error(
            McpErrorCodes.ServiceError,
            $"{source}: the Kaleido service answered HTTP {response.StatusCode}.",
            response.Body);

    private static CallToolResult Error(
        string code,
        string message,
        JsonNode? details = null)
    {
        var body = new JsonObject { ["code"] = code, ["message"] = message };

        if (details is not null)
        {
            body["details"] = details.DeepClone();
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = body.ToJsonString(KaleidoJsonOptions.Options) }]
        };
    }
}
