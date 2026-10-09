using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.Http;
using Kaleido.Processor;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>One tool call: the arguments, and what the MCP client sent along with them.</summary>
[ExcludeFromCodeCoverage]
internal sealed record KaleidoToolCall
{
    public required IReadOnlyDictionary<string, JsonElement> Arguments { get; init; }

    /// <summary>The request-scoped MCP server (client capabilities, elicitation).</summary>
    public required McpServer Server { get; init; }

    /// <summary>The request's <c>_meta</c> (per-request client capabilities on newer protocol revisions).</summary>
    public JsonObject? Meta { get; init; }

    /// <summary>MRTR: the client's answers to the input requests of the previous round.</summary>
    public IReadOnlyDictionary<string, InputResponse>? InputResponses { get; init; }

    /// <summary>MRTR: the opaque state the adapter returned with the previous round.</summary>
    public string? RequestState { get; init; }
}

/// <summary>
/// Runs a tool call against Kaleido and turns the outcome into what the model reads.
/// </summary>
/// <remarks>
/// <para><b>Queries</b> post the query body to the source or view and return its result.</para>
/// <para><b>Steps</b> post the step to its execute URL (process id in <c>X-Kaleido-Process-Id</c>).
/// The result is summarized for the model: the outcome, the business messages (verbatim), and
/// <c>next</c>: the required tool, or the available tools, of the service the process continues on.
/// After a handoff the target service's state is read, so <c>next</c> names that service's tools.</para>
/// <para><b>Questions</b> (a required information step) go to the <b>user</b> when the client can
/// show forms:</para>
/// <list type="bullet">
/// <item><description>Clients on the stateless MCP revisions (2026-07-28+) use MRTR: the call
/// returns "input required" with the form; the client shows it, then calls the same tool again with
/// the answers, which the adapter submits. Nothing is held between the calls.</description></item>
/// <item><description>Clients with a session (older revisions) get a classic elicitation request
/// during the call.</description></item>
/// <item><description>Otherwise the questions are returned in <c>next.informationRequest</c>, for the
/// agent to ask the user and submit with <c>answer_information_request</c>.</description></item>
/// </list>
/// </remarks>
internal interface IKaleidoToolInvoker
{
    /// <summary>
    /// Runs the tool. Always a well-formed answer: a tool result (failures are results with
    /// <c>isError: true</c>), or "input required" when the user must answer questions first.
    /// </summary>
    Task<ToolOutcome> InvokeAsync(
        KaleidoTool tool,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A tool's answer: a finished <see cref="CallToolResult"/>, or an <see cref="InputRequiredResult"/>
/// (MRTR) asking the client to collect the user's answers and call the tool again. Both are
/// successful protocol responses.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolOutcome
{
    public CallToolResult? Result { get; init; }

    public InputRequiredResult? InputRequired { get; init; }

    public static implicit operator ToolOutcome(CallToolResult result) => new() { Result = result };
}

internal sealed class KaleidoToolInvoker(
    IKaleidoApiClient apiClient,
    IInformationRequestElicitor elicitor,
    KaleidoMcpOptions options,
    ILogger<KaleidoToolInvoker> logger)
    : IKaleidoToolInvoker
{
    // The MRTR input-request key and the prefix of the adapter's opaque continuation state.
    private const string AnswersInputKey = "kaleido-answers";
    private const string StatePrefix = "kaleido-mcp:";
    private const string ClientCapabilitiesMetaKey = "io.modelcontextprotocol/clientCapabilities";

    public async Task<ToolOutcome> InvokeAsync(
        KaleidoTool tool,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            // An MRTR retry: the user answered the form returned with the previous round.
            if (call.RequestState?.StartsWith(StatePrefix, StringComparison.Ordinal) == true)
            {
                return await ContinueAsync(call, catalog, cancellationToken);
            }

            return tool.Kind switch
            {
                KaleidoToolKind.Query => await QueryAsync(tool, call.Arguments, cancellationToken),
                KaleidoToolKind.Step => await StepAsync(tool, call, catalog, cancellationToken),
                KaleidoToolKind.AnswerInformationRequest => await AnswerAsync(call, catalog, cancellationToken),
                KaleidoToolKind.GetProcessState => await StateAsync(call.Arguments, catalog, cancellationToken),
                _ => Error(McpErrorCodes.UnknownTool, $"Tool '{tool.Definition.Name}' has no handler.")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never a protocol error: the agent gets a result it can report. A cancelled call has
            // nobody waiting for an answer, so cancellation still propagates.
            logger.LogError(exception, "Kaleido MCP tool {Tool} failed unexpectedly.", tool.Definition.Name);

            return Error(
                McpErrorCodes.InternalError,
                $"{tool.Definition.Name} did not complete because of an unexpected error in the MCP adapter. If it was a step, check the process with get_process_state before trying again.");
        }
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

    private async Task<ToolOutcome> StepAsync(
        KaleidoTool tool,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        Guid? processId = null;

        if (Text(call.Arguments, "processId") is { } text)
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

        foreach (var (name, value) in call.Arguments.Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase)))
        {
            step[name] = JsonNode.Parse(value.GetRawText());
        }

        var response =
            await apiClient.PostAsync(
                tool.Url,
                new JsonObject { ["processStep"] = step },
                processId,
                cancellationToken);

        return await FollowUpAsync(tool.Service, response, catalog, call, [], round: 0, cancellationToken);
    }

    private async Task<ToolOutcome> AnswerAsync(
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        var arguments = call.Arguments;

        if (Text(arguments, "service") is not { } service ||
            !Guid.TryParse(Text(arguments, "processId"), out var processId) ||
            Text(arguments, "informationRequestId") is not { } informationRequestId)
        {
            return Error(McpErrorCodes.InvalidArguments, "Pass service, processId, informationRequestId and answers.");
        }

        var pending = await PendingAsync(service, processId, cancellationToken);

        if (pending.Error is { } error)
        {
            return error;
        }

        if (!string.Equals(pending.Request.InformationRequestId, informationRequestId, StringComparison.Ordinal))
        {
            return Error(
                McpErrorCodes.InvalidArguments,
                $"The process is waiting for answers to '{pending.Request.InformationRequestId}', not '{informationRequestId}'. Ask the user the current questions.",
                Serialize(pending.Request));
        }

        var items = new List<InformationResponseItem>();

        if (arguments.TryGetValue("answers", out var answers) && answers.ValueKind == JsonValueKind.Array)
        {
            foreach (var answer in answers.EnumerateArray())
            {
                var values = answer.TryGetProperty("values", out var given) && given.ValueKind == JsonValueKind.Array
                    ? given.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : x.GetRawText())
                        .Select(x => new InformationAnswer { Value = x })
                        .ToArray()
                    : [];

                items.Add(new InformationResponseItem
                {
                    ItemId = answer.TryGetProperty("itemId", out var itemId) ? itemId.GetString() ?? string.Empty : string.Empty,
                    Answers = values
                });
            }
        }

        var response = await SubmitAsync(pending.Step, pending.Request, items, processId, cancellationToken);

        return await FollowUpAsync(service, response, catalog, call, [], round: 0, cancellationToken);
    }

    // The MRTR retry: submit the form answers to the process's pending request and carry on.
    private async Task<ToolOutcome> ContinueAsync(
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (DecodeState(call.RequestState) is not { } state)
        {
            return Error(McpErrorCodes.InvalidArguments, "The continuation state is not readable; call the tool again.");
        }

        var answered = new JsonArray(state.Answered.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

        ElicitResult? elicited = null;

        if (call.InputResponses?.TryGetValue(AnswersInputKey, out var input) == true)
        {
            elicited = input.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        }

        if (elicited is not { IsAccepted: true, Content: { } content })
        {
            return await StateAsync(
                state.Service,
                state.ProcessId,
                catalog,
                $"The user did not answer the questions ({elicited?.Action ?? "no answer"}). Nothing was submitted; the process is still waiting for them.",
                cancellationToken);
        }

        var pending = await PendingAsync(state.Service, state.ProcessId, cancellationToken);

        if (pending.Error is { } error)
        {
            return error;
        }

        // The answers belong to the round the form showed; a stale form is not submitted.
        if (!string.Equals(pending.Request.InformationRequestId, state.InformationRequestId, StringComparison.Ordinal))
        {
            return await StateAsync(
                state.Service,
                state.ProcessId,
                catalog,
                "The process is now asking different questions; the answered form was not submitted.",
                cancellationToken);
        }

        var items = elicitor.ToAnswers(pending.Request, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
        var response = await SubmitAsync(pending.Step, pending.Request, items, state.ProcessId, cancellationToken);

        answered.Add(state.InformationRequestId);

        return await FollowUpAsync(
            state.Service,
            response,
            catalog,
            call with { InputResponses = null, RequestState = null },
            answered,
            state.Round + 1,
            cancellationToken);
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

        return await StateAsync(service, processId, catalog, note: null, cancellationToken);
    }

    private async Task<CallToolResult> StateAsync(
        string service,
        Guid processId,
        ToolCatalog catalog,
        string? note,
        CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync(StatePath(service, processId), cancellationToken);

        if (!response.IsSuccess)
        {
            return ServiceError(ToolCatalogBuilder.GetProcessStateTool, response);
        }

        if (Deserialize<ProcessStateResponse>(response.Body) is not { } state)
        {
            return Error(McpErrorCodes.ServiceError, $"The state of process {processId} could not be read.");
        }

        if (!string.IsNullOrWhiteSpace(state.TargetProcessorName))
        {
            return await StateAsync(state.TargetProcessorName, processId, catalog, note, cancellationToken);
        }

        var summary = new JsonObject
        {
            ["processId"] = processId.ToString(),
            ["service"] = service,
            ["state"] = Camel(state.State.ToString()),
            ["completedSteps"] = new JsonArray(state.Steps
                .Where(x => x.Status == StepExecutionStatus.Completed)
                .Select(x => (JsonNode?)x.StepName)
                .ToArray()),
            ["next"] = Next(service, state.RequiredStep, state.AvailableSteps, catalog, elicited: false)
        };

        if (note is not null)
        {
            summary["note"] = note;
        }

        return Success(summary);
    }

    // After a step: follow a handoff to the target's state, put questions to the user while the
    // client can show forms, and summarize where the process stands.
    private async Task<ToolOutcome> FollowUpAsync(
        string service,
        KaleidoApiResponse response,
        ToolCatalog catalog,
        KaleidoToolCall call,
        JsonArray answered,
        int round,
        CancellationToken cancellationToken)
    {
        string? note = null;

        for (; ; round++)
        {
            if (!response.IsSuccess)
            {
                return ServiceError(service, response);
            }

            if (Deserialize<StepExecutionResponse<JsonElement>>(response.Body) is not { } step)
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

            var mode = ElicitationMode(call);

            if (required?.InformationRequest is { } pending &&
                step.Outcome == StepExecutionOutcome.Completed &&
                mode != Elicitation.None)
            {
                if (round >= options.MaxElicitationRounds)
                {
                    note = "The process asked more rounds of questions than are put to the user automatically; ask the user the remaining questions.";
                }
                else if (mode == Elicitation.Mrtr)
                {
                    // Return the form; the client shows it and calls this tool again with the answers.
                    return new ToolOutcome
                    {
                        InputRequired = new InputRequiredResult
                        {
                            InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                            {
                                [AnswersInputKey] = InputRequest.ForElicitation(elicitor.ToElicitation(pending))
                            },
                            RequestState = EncodeState(new ContinuationState(
                                pendingService,
                                step.ProcessId,
                                pending.InformationRequestId,
                                round,
                                answered.Select(x => x?.GetValue<string>()).OfType<string>().ToArray()))
                        }
                    };
                }
                else
                {
                    var elicited = await call.Server.ElicitAsync(elicitor.ToElicitation(pending), cancellationToken);

                    if (elicited.IsAccepted && elicited.Content is { } content)
                    {
                        var items = elicitor.ToAnswers(pending, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

                        answered.Add(pending.InformationRequestId);
                        service = pendingService;
                        response = await SubmitAsync(required, pending, items, step.ProcessId, cancellationToken);

                        continue;
                    }

                    note = $"The user did not answer the questions ({elicited.Action}). Nothing was submitted; the process is still waiting for them.";
                }
            }

            return Success(Summary(service, pendingService, step, required, available, answered, note, catalog));
        }
    }

    private Task<KaleidoApiResponse> SubmitAsync(
        ProcessRequiredStep step,
        InformationRequest request,
        IReadOnlyList<InformationResponseItem> items,
        Guid processId,
        CancellationToken cancellationToken) =>
        apiClient.PostAsync(
            step.ExecuteUrl,
            new JsonObject
            {
                ["processStep"] = new JsonObject
                {
                    ["informationRequestId"] = request.InformationRequestId,
                    ["items"] = Serialize(items)
                }
            },
            processId,
            cancellationToken);

    // The questions a process is waiting for, read fresh from Kaleido (never from the client).
    private async Task<PendingQuestions> PendingAsync(
        string service,
        Guid processId,
        CancellationToken cancellationToken)
    {
        var response = await apiClient.GetAsync(StatePath(service, processId), cancellationToken);

        if (!response.IsSuccess)
        {
            return PendingQuestions.Failed(ServiceError(ToolCatalogBuilder.AnswerInformationRequestTool, response));
        }

        return Deserialize<ProcessStateResponse>(response.Body)?.RequiredStep is { InformationRequest: { } request } step
            ? new PendingQuestions(step, request, null)
            : PendingQuestions.Failed(Error(
                McpErrorCodes.NoPendingInformationRequest,
                $"Process {processId} on '{service}' is not waiting for answers. Use get_process_state to see what it needs."));
    }

    private static Elicitation ElicitationMode(KaleidoToolCall call)
    {
        var supportsForms =
            call.Server.ClientCapabilities?.Elicitation is not null ||
            call.Meta?[ClientCapabilitiesMetaKey]?["elicitation"] is not null;

        if (!supportsForms)
        {
            return Elicitation.None;
        }

        return call.Server.IsMrtrSupported ? Elicitation.Mrtr : Elicitation.Session;
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
            summary["answered"] = answered.DeepClone();
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

    // ── Continuation state (MRTR) ────────────────────────────────────────────

    // Opaque to the client. It only says which process and round the form belongs to; the pending
    // questions and the URL to submit to are always read fresh from Kaleido, never trusted from here.
    private static string EncodeState(ContinuationState state) =>
        StatePrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state, KaleidoJsonOptions.Options));

    private static ContinuationState? DecodeState(string? value)
    {
        if (value is null || !value.StartsWith(StatePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ContinuationState>(
                Encoding.UTF8.GetString(Convert.FromBase64String(value[StatePrefix.Length..])),
                KaleidoJsonOptions.Options);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
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

    private enum Elicitation
    {
        None,
        Mrtr,
        Session
    }

    private sealed record ContinuationState(
        string Service,
        Guid ProcessId,
        string InformationRequestId,
        int Round,
        IReadOnlyList<string> Answered);

    private sealed record PendingQuestions(
        ProcessRequiredStep Step,
        InformationRequest Request,
        CallToolResult? Error)
    {
        public static PendingQuestions Failed(CallToolResult error) =>
            new(new ProcessRequiredStep { Name = string.Empty }, new InformationRequest { InformationRequestId = string.Empty }, error);
    }
}
