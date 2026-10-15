using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Client;
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
/// <para>Every call goes through the Kaleido clients (<c>AddHttpClients()</c>) of the tool's service,
/// by capability name, as the MCP caller (the handler opens the caller-forwarding scope).</para>
/// <para><b>Queries</b> run the source or view query and return its result.</para>
/// <para><b>Steps</b> execute the step by name, continuing the given process.
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

internal sealed partial class KaleidoToolInvoker(
    IKaleidoProcessorClientFactory processors,
    IKaleidoQueryableClientFactory queryables,
    IKaleidoProcessForwarding processForwarding,
    IInformationRequestElicitor elicitor,
    KaleidoMcpOptions options,
    ILogger<KaleidoToolInvoker> logger)
    : IKaleidoToolInvoker
{
    // The MRTR input-request key and the prefix of the adapter's opaque continuation state.
    private const string AnswersInputKey = "kaleido-answers";
    private const string StatePrefix = "kaleido-mcp:";
    private const string ClientCapabilitiesMetaKey = "io.modelcontextprotocol/clientCapabilities";

    // The service refused or failed the call: its status and errors, for the agent to explain. A
    // conflict (409) means another call changed the process and nothing was saved: the agent rereads
    // the state instead of repeating the call.
    private ToolOutcome ServiceAnswered(KaleidoTool tool, KaleidoHttpClientException exception)
    {
        LogServiceAnswered(logger, tool.Definition.Name, (int)exception.StatusCode, exception.Code);

        if (string.Equals(exception.Code, HttpClientErrorCodes.Conflict, StringComparison.Ordinal))
        {
            return Error(
                McpErrorCodes.ProcessConflict,
                $"{tool.Definition.Name}: the process was changed by another call, so nothing was saved. Read its state with {ToolCatalogBuilder.GetProcessStateTool} and decide with the user what to do next; do not repeat the call.");
        }

        // A server failure says nothing the agent can act on (and may name internal types): tell it
        // what to check instead of what failed inside the service.
        if ((int)exception.StatusCode >= 500 && exception.Errors.Count == 0)
        {
            return Error(
                McpErrorCodes.ServiceError,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{tool.Definition.Name}: the service failed ({(int)exception.StatusCode}) and reported no reason. Check your arguments against this tool's inputs; if they are right, tell the user and do not repeat the same call. If it was a step, check the process with {ToolCatalogBuilder.GetProcessStateTool} before trying again."),
                Serialize(new { status = (int)exception.StatusCode, errors = exception.Errors }));
        }

        return Error(
            McpErrorCodes.ServiceError,
            $"{tool.Definition.Name}: {exception.Message}",
            Serialize(new { status = (int)exception.StatusCode, errors = exception.Errors }));
    }

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
            // An MRTR retry of a guided lookup: the user searched, or picked a result.
            if (call.RequestState?.StartsWith(LookupStatePrefix, StringComparison.Ordinal) == true)
            {
                return await ContinueLookupAsync(call, catalog, cancellationToken);
            }

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
                KaleidoToolKind.SearchTools => SearchTools(call.Arguments, catalog),
                KaleidoToolKind.DescribeTool => DescribeTool(call.Arguments, catalog),
                _ => Error(McpErrorCodes.UnknownTool, $"Tool '{tool.Definition.Name}' has no handler."),
            };
        }
        catch (KaleidoHttpClientException exception)
        {
            return ServiceAnswered(tool, exception);
        }
        catch (MissingClientException exception)
        {
            return Error(McpErrorCodes.ServiceUnavailable, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            LogServiceUnreachable(logger, exception, tool.Definition.Name);

            return Error(
                McpErrorCodes.ServiceUnavailable,
                $"{tool.Definition.Name}: the Kaleido service could not be reached. If it was a step, check the process with get_process_state before trying again.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never a protocol error: the agent gets a result it can report. A cancelled call has
            // nobody waiting for an answer, so cancellation still propagates.
            LogToolFailed(logger, exception, tool.Definition.Name);

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
        QueryApiBody? body;
        JsonObject? parameters = null;

        try
        {
            body = QueryBody(arguments).Deserialize<QueryApiBody>(KaleidoJsonOptions.Options);

            if (arguments.TryGetValue("parameters", out var given) && given.ValueKind == JsonValueKind.Object)
            {
                parameters = JsonNode.Parse(given.GetRawText())?.AsObject();
            }
        }
        catch (JsonException exception)
        {
            return Error(McpErrorCodes.InvalidArguments, $"The query arguments have the wrong shape: {exception.Message}");
        }

        // One processId, at the top level. A nested parameters.processId (the earlier shape) is still
        // accepted when the top level has none.
        var processIdText =
            Text(arguments, ToolCatalogBuilder.ProcessIdArgument) ??
            (parameters?[ToolCatalogBuilder.ProcessIdArgument] is JsonValue nested && nested.TryGetValue<string>(out var nestedText) ? nestedText : null);

        Guid? processId = null;

        if (!string.IsNullOrWhiteSpace(processIdText))
        {
            if (!Guid.TryParse(processIdText, out var parsed))
            {
                return Error(McpErrorCodes.InvalidArguments, $"processId '{processIdText}' is not a valid id.");
            }

            processId = parsed;
        }

        if (MissingArgument(tool, arguments, processId) is { } missing)
        {
            return missing;
        }

        return await RunQueryAsync(tool, body, parameters, processId, cancellationToken);
    }

    // { searchText?, filter?, sort?, page? } from the tool's arguments; several filters are and-ed.
    private static JsonObject QueryBody(IReadOnlyDictionary<string, JsonElement> arguments)
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
                    ["group"] = new JsonObject { ["operator"] = "and", ["filters"] = new JsonArray(conditions) },
                };
            }
        }

        CopyArgument(arguments, "sort", query);
        CopyArgument(arguments, "page", query);

        return query;
    }

    private async Task<CallToolResult> RunQueryAsync(
        KaleidoTool tool,
        QueryApiBody? body,
        JsonObject? parameters,
        Guid? processId,
        CancellationToken cancellationToken)
    {

        if (tool.ParametersTakeProcessId && processId is { } scoped)
        {
            parameters ??= [];
            parameters[ToolCatalogBuilder.ProcessIdArgument] = scoped.ToString();
        }

        var client = Queryable(tool.Service);
        var request = new QueryApiRequest<JsonObject>(parameters, body);
        var source = tool.SourceName ?? tool.CapabilityName;

        // The process id goes with the query as X-Kaleido-Process-Id, so the service records it.
        using var forwarded = processId is null ? null : processForwarding.ForwardProcess(processId);

        var result = tool.ViewName is { } view
            ? await client.QueryViewAsync<JsonObject, JsonObject>(source, view, request, cancellationToken)
            : await client.QuerySourceAsync<JsonObject, JsonObject>(source, request, cancellationToken);

        var answer = Serialize(result) as JsonObject ?? [];

        if (processId is { } id)
        {
            answer[ToolCatalogBuilder.ProcessIdArgument] = id.ToString();
        }

        return Success(answer);
    }

    // The tool's own required inputs, checked before anything is sent, so a missing value comes back
    // as a fix the agent can make (the input's own description) rather than a service failure.
    private static CallToolResult? MissingArgument(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        Guid? processId)
    {
        var schema = tool.Definition.InputSchema;

        if (schema.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in RequiredNames(schema))
        {
            if (!properties.TryGetProperty(name, out var property))
            {
                continue;
            }

            var present = string.Equals(name, ToolCatalogBuilder.ProcessIdArgument, StringComparison.Ordinal)
                ? processId is not null
                : arguments.TryGetValue(name, out var value) && IsPresent(value);

            // A missing required object is reported by its first required member: what the agent must supply.
            var missing = present
                ? MissingMember(name, property, arguments.TryGetValue(name, out var given) ? given : default)
                : RequiredNames(property).FirstOrDefault() is { } inner
                    ? ($"{name}.{inner}", MemberDescription(property, inner))
                    : (name, Description(property));

            if (missing is var (path, description))
            {
                return Error(
                    McpErrorCodes.InvalidArguments,
                    $"{tool.Definition.Name}: {path} is required" + (string.IsNullOrWhiteSpace(description) ? "." : $": {description.Trim()}") + " Nothing was sent.");
            }
        }

        return null;
    }

    // The first required member a given object argument leaves out.
    private static (string Path, string? Description)? MissingMember(string name, JsonElement property, JsonElement given)
    {
        if (given.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var inner in RequiredNames(property))
        {
            if (!given.TryGetProperty(inner, out var value) || !IsPresent(value))
            {
                return ($"{name}.{inner}", MemberDescription(property, inner));
            }
        }

        return null;
    }

    private static IEnumerable<string> RequiredNames(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty("required", out var required) &&
        required.ValueKind == JsonValueKind.Array
            ? required.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).OfType<string>()
            : [];

    private static bool IsPresent(JsonElement value) =>
        value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
        (value.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(value.GetString()));

    private static string? MemberDescription(JsonElement property, string member) =>
        property.TryGetProperty("properties", out var members) && members.TryGetProperty(member, out var inner)
            ? Description(inner)
            : null;

    private static string? Description(JsonElement property) =>
        property.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String
            ? description.GetString()
            : null;

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

        // A lookup the agent did not fill: when the client shows forms, the user searches and picks
        // the result, and the adapter fills the inputs from it, so the ids never pass through the model.
        if (ElicitationMode(call) != Elicitation.None && MissingLookup(tool, call.Arguments) is { } lookup)
        {
            return await GuideLookupAsync(tool, lookup, call, catalog, processId, cancellationToken);
        }

        return await RunStepAsync(tool, call.Arguments, processId, call, catalog, cancellationToken);
    }

    private async Task<ToolOutcome> RunStepAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        Guid? processId,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        var step = new JsonObject();

        foreach (var (name, value) in arguments.Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase)))
        {
            step[name] = JsonNode.Parse(value.GetRawText());
        }

        var response =
            await Processor(tool.Service).ExecuteStepAsync(
                tool.CapabilityName,
                JsonSerializer.SerializeToElement(step, KaleidoJsonOptions.Options),
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
                    Answers = values,
                });
            }
        }

        var response = await SubmitAsync(service, pending.Step, pending.Request, items, processId, cancellationToken);

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
        var response = await SubmitAsync(state.Service, pending.Step, pending.Request, items, state.ProcessId, cancellationToken);

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
        if (await Processor(service).GetProcessStateAsync(processId, cancellationToken) is not { } state)
        {
            return Error(McpErrorCodes.ServiceError, $"Process {processId} was not found on '{service}', or you may not see it.");
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
            ["failedSteps"] = new JsonArray(state.Steps
                .Where(x => x.Status is StepExecutionStatus.Failed or StepExecutionStatus.Exception)
                .Select(x => (JsonNode?)x.StepName)
                .ToArray()),
            ["next"] = Next(service, state.RequiredStep, state.AvailableSteps, catalog, elicited: false, processId),
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
        StepExecutionResponse<JsonElement> step,
        ToolCatalog catalog,
        KaleidoToolCall call,
        JsonArray answered,
        int round,
        CancellationToken cancellationToken)
    {
        string? note = null;

        for (; ; round++)
        {
            var pendingService = service;
            var required = step.RequiredStep;
            var available = step.AvailableSteps;

            if (!string.IsNullOrWhiteSpace(step.TargetProcessorName))
            {
                pendingService = step.TargetProcessorName;

                var targetState = await Processor(pendingService).GetProcessStateAsync(step.ProcessId, cancellationToken);

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
                    return AskForAnswers(pendingService, step.ProcessId, pending, round, answered);
                }
                else
                {
                    var elicited = await call.Server.ElicitAsync(elicitor.ToElicitation(pending), cancellationToken);

                    if (elicited.IsAccepted && elicited.Content is { } content)
                    {
                        var items = elicitor.ToAnswers(pending, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

                        answered.Add(pending.InformationRequestId);
                        service = pendingService;
                        step = await SubmitAsync(pendingService, required, pending, items, step.ProcessId, cancellationToken);

                        continue;
                    }

                    note = $"The user did not answer the questions ({elicited.Action}). Nothing was submitted; the process is still waiting for them.";
                }
            }

            return Success(Summary(service, pendingService, step, required, available, answered, note, catalog));
        }
    }

    private Task<StepExecutionResponse<JsonElement>> SubmitAsync(
        string service,
        ProcessRequiredStep step,
        InformationRequest request,
        IReadOnlyList<InformationResponseItem> items,
        Guid processId,
        CancellationToken cancellationToken) =>
        Processor(service).ExecuteStepAsync(
            step.Name,
            JsonSerializer.SerializeToElement(
                new JsonObject
                {
                    ["informationRequestId"] = request.InformationRequestId,
                    ["items"] = Serialize(items),
                },
                KaleidoJsonOptions.Options),
            processId,
            cancellationToken);

    // The questions a process is waiting for, read fresh from Kaleido (never from the client).
    private async Task<PendingQuestions> PendingAsync(
        string service,
        Guid processId,
        CancellationToken cancellationToken)
    {
        var state = await Processor(service).GetProcessStateAsync(processId, cancellationToken);

        return state?.RequiredStep is { InformationRequest: { } request } step
            ? new PendingQuestions(step, request, Error: null)
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
            ["outcome"] = Camel(step.Outcome.ToString()),
        };

        if (step.Result.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            summary["result"] = JsonNode.Parse(step.Result.GetRawText());
        }

        // Business messages are the implementer's and always shown; framework messages only when they
        // matter to the caller (warnings and errors), not informational notes such as a repeatable step.
        var messages = step.BusinessMessages
            .Concat(step.FrameworkMessages.Where(x => x.Type != MessageType.Information))
            .ToArray();

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

        summary["next"] = Next(pendingService, required, available, catalog, elicited: answered.Count > 0, step.ProcessId);

        // A rejected step, or one that failed unexpectedly, changed nothing: the process is where it
        // was, so the user can correct the input and retry, or choose another step.
        note ??= step.Outcome switch
        {
            StepExecutionOutcome.Rejected =>
                "The step was rejected and nothing was changed. Tell the user why (messages); they can correct it and try again, or choose another step.",
            StepExecutionOutcome.Failed when required is not null || available.Count > 0 =>
                "The step did not complete and nothing was recorded; the process is where it was. The user can try again or choose another step.",
            _ => null,
        };

        if (note is not null)
        {
            summary["note"] = note;
        }

        return summary;
    }

    private ToolOutcome AskForAnswers(string service, Guid processId, InformationRequest pending, int round, JsonArray answered) =>
        new()
        {
            InputRequired = new InputRequiredResult
            {
                InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                {
                    [AnswersInputKey] = InputRequest.ForElicitation(elicitor.ToElicitation(pending)),
                },
                RequestState = EncodeState(new ContinuationState(
                    service,
                    processId,
                    pending.InformationRequestId,
                    round,
                    answered.Select(x => x?.GetValue<string>()).OfType<string>().ToArray())),
            },
        };

    private static JsonObject Next(
        string service,
        ProcessRequiredStep? required,
        IReadOnlyCollection<ProcessStepSummary> available,
        ToolCatalog catalog,
        bool elicited,
        Guid? processId)
    {
        var next = new JsonObject { ["service"] = service };

        if (required is { InformationRequest: { } pending })
        {
            next["required"] = new JsonObject
            {
                ["tool"] = ToolCatalogBuilder.AnswerInformationRequestTool,
                ["step"] = required.Name,
                ["displayName"] = required.DisplayName,
            };
            next["informationRequest"] = Serialize(pending);
            next["instruction"] =
                (elicited ? "More questions followed. " : string.Empty) +
                "Ask the user every question exactly as written and submit only the user's answers with answer_information_request (service, processId, informationRequestId, answers). " +
                (HasProposals(pending.Items)
                    ? "Questions with proposedAnswers were pre-filled from what was said earlier: read each one back with its proposed answer and have the user confirm or correct it. "
                    : string.Empty) +
                "If the user already answered a question earlier in this conversation, read that answer back for confirmation instead of asking again. Never answer for the user, and never submit a proposed answer the user has not confirmed.";
        }
        else if (required is not null)
        {
            next["required"] = catalog.FindByUrl(required.ExecuteUrl) is { } tool
                ? WithDetails(new JsonObject { ["tool"] = tool.Definition.Name, ["step"] = required.Name, ["displayName"] = required.DisplayName }, tool, catalog, processId)
                : new JsonObject { ["step"] = required.Name, ["displayName"] = required.DisplayName, ["note"] = "You are not allowed to run this step." };
        }

        var tools = OfferedTools(available, catalog, processId);

        if (catalog.IsFrontDoor && HasDetails(next, tools))
        {
            next["howToRun"] = FrontDoorHowToRun;
        }

        next["available"] = new JsonArray(tools);

        if (tools.Length > 1)
        {
            next["choice"] = "Several steps are available: offer them to the user and let the user choose; do not pick one yourself. A step's 'closes' lists the steps it ends.";
        }

        if (required is null && tools.Length == 0)
        {
            next["note"] = "No further steps: the process is complete here.";
        }

        return next;
    }

    // The available steps the caller may run, each with the steps it closes - from the published
    // availableUntil - so the user decides in which order to go, knowing what each choice ends.
    private static JsonNode[] OfferedTools(IReadOnlyCollection<ProcessStepSummary> available, ToolCatalog catalog, Guid? processId)
    {
        var offered = available
            .Select(x => (Step: x, Tool: catalog.FindByUrl(x.ExecuteUrl)))
            .Where(x => x.Tool is not null)
            .ToArray();

        return offered
            .Select(x =>
            {
                var entry = new JsonObject { ["tool"] = x.Tool?.Definition.Name, ["displayName"] = x.Step.DisplayName ?? x.Step.Name };

                var closes = offered
                    .Where(other => other.Tool?.ClosedBy.Contains(x.Step.Name, StringComparer.Ordinal) == true)
                    .Select(other => (JsonNode?)other.Tool?.Definition.Name)
                    .ToArray();

                if (closes.Length > 0)
                {
                    entry["closes"] = new JsonArray(closes);
                }

                return (JsonNode)(x.Tool is { } tool ? WithDetails(entry, tool, catalog, processId) : entry);
            })
            .ToArray();
    }

    private static bool HasProposals(IEnumerable<InformationItem> items) =>
        items.Any(x => x.ProposedAnswers.Count > 0 || HasProposals(x.Items));

    // ── Continuation state (MRTR) ────────────────────────────────────────────

    // Opaque to the client. It only says which process and round the form belongs to; the pending
    // questions and the URL to submit to are always read fresh from Kaleido, never trusted from here.
    private static string EncodeState(ContinuationState state) =>
        StatePrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state, KaleidoJsonOptions.Options));

    private static ContinuationState? DecodeState(string? value)
    {
        if (value?.StartsWith(StatePrefix, StringComparison.Ordinal) != true)
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

    // The service's Kaleido clients. A tool's service always comes from the registry, so a missing
    // client means the host publishes capabilities it cannot call (e.g. its own, without a client
    // for itself).
    private IKaleidoProcessorClient Processor(string service) =>
        processors.TryGetClientForService(service, out var client)
            ? client
            : throw new MissingClientException(service);

    private IKaleidoQueryableClient Queryable(string service) =>
        queryables.TryGetClientForService(service, out var client)
            ? client
            : throw new MissingClientException(service);

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

    private static JsonNode? Serialize<T>(T value) =>
        JsonSerializer.SerializeToNode(value, KaleidoJsonOptions.Options);

    private static string Camel(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

    private static CallToolResult Success(JsonNode body) =>
        new()
        {
            Content = [new TextContentBlock { Text = body.ToJsonString(KaleidoJsonOptions.Options) }],
        };

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
            Content = [new TextContentBlock { Text = body.ToJsonString(KaleidoJsonOptions.Options) }],
        };
    }

    private sealed class MissingClientException(string service)
        : Exception($"No Kaleido client is configured for service '{service}' on this host, so its tools cannot be called. Add it under Kaleido:Clients.");

    private enum Elicitation
    {
        None,
        Mrtr,
        Session,
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP tool {Tool}: the service answered {StatusCode} ({Code}).")]
    private static partial void LogServiceAnswered(ILogger logger, string tool, int statusCode, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP tool {Tool}: the service could not be reached.")]
    private static partial void LogServiceUnreachable(ILogger logger, Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido MCP tool {Tool} failed unexpectedly.")]
    private static partial void LogToolFailed(ILogger logger, Exception exception, string tool);
}
