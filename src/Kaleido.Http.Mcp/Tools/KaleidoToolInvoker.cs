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
        CancellationToken cancellationToken);
}

internal sealed partial class KaleidoToolInvoker(
    IKaleidoProcessorClientFactory processors,
    IKaleidoQueryableClientFactory queryables,
    IKaleidoProcessForwarding processForwarding,
    IInformationRequestElicitor elicitor,
    IMcpStateProtector states,
    KaleidoMcpOptions options,
    ILogger<KaleidoToolInvoker> logger)
    : IKaleidoToolInvoker
{
    // The MRTR input-request key of an answers form.
    private const string AnswersInputKey = "kaleido-answers";

    /// <summary>The <c>answer_information_request</c> argument carrying the answers of earlier rounds.</summary>
    internal const string AnswersTokenArgument = "answersToken";
    private const string ClientCapabilitiesMetaKey = "io.modelcontextprotocol/clientCapabilities";

    // The service refused or failed the call: its status and errors, for the agent to explain. A
    // conflict (409) means another call changed the process and nothing was saved: the agent rereads
    // the state instead of repeating the call.
    private ToolOutcome ServiceAnswered(KaleidoTool tool, KaleidoHttpClientException exception)
    {
        LogServiceAnswered(logger, tool.Definition.Name, (int)exception.StatusCode, exception.Code);

        if (UnsavedOutcome(tool, exception) is { } unsaved)
        {
            return unsaved;
        }

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
            Serialize(new { status = (int)exception.StatusCode, errors = exception.Errors }),
            ServiceReasons(exception.Errors));
    }

    // #537: the step ran, but the service could not save the process - repeating it could do its work
    // twice. Or the process is being recovered, and nothing ran: try again shortly. Read from the
    // service's own error code (409 and 5xx alone do not say which).
    private static CallToolResult? UnsavedOutcome(KaleidoTool tool, KaleidoHttpClientException exception)
    {
        var code = exception.Errors.Count > 0 ? exception.Errors[0].Code : null;
        var details = Serialize(new { status = (int)exception.StatusCode, errors = exception.Errors });

        return code switch
        {
            ProcessorErrorCodes.OutcomePendingRecovery => Error(
                McpErrorCodes.ServiceError,
                $"{tool.Definition.Name}: the step ran, but the service could not save the process yet; it saves it when its store is available again. Do not repeat the call: tell the user, and read the process with {ToolCatalogBuilder.GetProcessStateTool} later.",
                details),
            ProcessorErrorCodes.OutcomeNotSaved => Error(
                McpErrorCodes.ServiceError,
                $"{tool.Definition.Name}: the step ran, but the service could not save the process, so it must be reconciled by an operator. Do not repeat the call: tell the user that the step's work may have happened.",
                details),
            ProcessorErrorCodes.ProcessRecovering => Error(
                McpErrorCodes.ServiceUnavailable,
                $"{tool.Definition.Name}: the process is being recovered after a storage failure, and nothing was run. Try the same call again in a moment; do not start a new process.",
                details),
            _ => null,
        };
    }

    public async Task<ToolOutcome> InvokeAsync(
        KaleidoTool tool,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(catalog);

        try
        {
            if (IsRetry(call.RequestState))
            {
                return await RetryAsync(call, catalog, cancellationToken);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never a protocol error: the agent gets a result it can report. A cancelled call has
            // nobody waiting for an answer, so the caller's cancellation still propagates (#517).
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
        AddFieldDescriptions(answer, tool, answer["results"]);

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

    // The call carries one of the adapter's states: it is an MRTR retry of an earlier round, not an
    // ordinary call.
    private static bool IsRetry(string? state) =>
        state is not null &&
        (state.StartsWith(McpStateProtector.Prefix(McpStateKind.Confirmation), StringComparison.Ordinal) ||
         state.StartsWith(McpStateProtector.Prefix(McpStateKind.Lookup), StringComparison.Ordinal) ||
         state.StartsWith(McpStateProtector.Prefix(McpStateKind.Continuation), StringComparison.Ordinal));

    // An MRTR retry, by the state the previous round returned: a confirmation (#439), a guided lookup
    // (the user searched or picked), or answers to an information request's form.
    private async Task<ToolOutcome> RetryAsync(
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken) =>
        call.RequestState switch
        {
            { } state when state.StartsWith(McpStateProtector.Prefix(McpStateKind.Confirmation), StringComparison.Ordinal) => await ContinueConfirmationAsync(state, call, catalog, cancellationToken),
            { } state when state.StartsWith(McpStateProtector.Prefix(McpStateKind.Lookup), StringComparison.Ordinal) => await ContinueLookupAsync(call, catalog, cancellationToken),
            _ => await ContinueAsync(call, catalog, cancellationToken),
        };

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

        return await RunStepAsync(tool, call.Arguments, processId, call, catalog, confirmed: false, cancellationToken);
    }

    // Every step a tool runs goes through here (direct calls, call_tool, guided lookups), so the
    // confirmation round has one place to happen (#439).
    private async Task<ToolOutcome> RunStepAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        Guid? processId,
        KaleidoToolCall call,
        ToolCatalog catalog,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed && await ConfirmAsync(tool, arguments, processId, call, cancellationToken) is { } unconfirmed)
        {
            return unconfirmed;
        }

        // Arguments in a canonical order: the same logical call always sends the same body, so the
        // service's request fingerprint matches when the key repeats.
        var step = new JsonObject();

        foreach (var (name, value) in arguments
                     .Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            step[name] = Canonical(JsonNode.Parse(value.GetRawText()));
        }

        var body = JsonSerializer.SerializeToElement(step, KaleidoJsonOptions.Options);

        // One key per logical tool call (#440): an agent that calls the same step again with the same
        // inputs for the same process - typically after a timeout or a lost answer - gets the stored
        // answer instead of running the step twice. Only for a non-repeatable step continuing a known
        // process: a repeatable step called twice is two operations, and two starts of new processes
        // (no process id) are two processes. The service scopes keys per caller.
        var idempotencyKey =
            processId is { } id && !tool.Repeatable
                ? IdempotencyKey(tool.Service, tool.CapabilityName, id, body.GetRawText())
                : null;

        var response =
            await Processor(tool.Service).ExecuteStepAsync(
                tool.CapabilityName,
                body,
                processId,
                idempotencyKey,
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

        // The answers of earlier rounds of the same request come back in the token (ADR 0024), with the
        // exchange the questions were shown for (#442).
        var token = DecodeAnswers(Text(arguments, AnswersTokenArgument), processId, informationRequestId);

        if (NotThePendingQuestions(pending.Request, informationRequestId, token) is { } notPending)
        {
            return notPending;
        }

        if (token is null)
        {
            return NoAnswersToken(service, pending, catalog, processId);
        }

        var earlier = new Dictionary<string, IReadOnlyList<string>>(token.Answers, StringComparer.Ordinal);
        var exchangeId = token.ExchangeId;
        var given = AnswersArgument(arguments);
        var merged = Merge(earlier, given);

        // These answers decide further questions: hand them to the agent; nothing is submitted yet.
        // Otherwise submit, and Kaleido validates (a missing answer is its unanswered error, as always).
        if (DecidesMoreQuestions(pending.Request, earlier, merged))
        {
            return Success(new JsonObject
            {
                ["processId"] = processId.ToString(),
                ["service"] = service,
                ["next"] = Next(service, pending.Step, [], catalog, elicited: false, processId, merged),
            });
        }

        var submitted = Submitted(pending.Request, earlier, given, merged);

        if (await ConfirmAnswersAsync(pending.Step, processId, new ConfirmedAnswers(service, informationRequestId, exchangeId, submitted, Round: 0, Answered: []), call, cancellationToken) is { } unconfirmed)
        {
            return unconfirmed;
        }

        var response = await SubmitAsync(service, pending.Step, pending.Request, exchangeId, submitted, processId, cancellationToken);

        return await FollowUpAsync(service, response, catalog, call, [], round: 0, cancellationToken);
    }

    // #516: answers are submitted only with the token issued with the questions - never under the
    // current exchange by default, which would take answers to earlier questions for answers to these.
    // The refusal carries the current questions with a fresh token.
    private CallToolResult NoAnswersToken(string service, PendingQuestions pending, ToolCatalog catalog, Guid processId) =>
        Error(
            McpErrorCodes.InvalidArguments,
            $"The {AnswersTokenArgument} is missing, or not the one returned with these questions; nothing was submitted. Ask the user the questions below and answer with their {AnswersTokenArgument}, unchanged.",
            Next(service, pending.Step, [], catalog, elicited: false, processId));

    // The agent's answers as given (Kaleido judges their shape), after the earlier rounds' answers;
    // only answers to questions the answers disable are left out.
    private static InformationResponseItem[] Submitted(
        InformationRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> earlier,
        List<InformationResponseItem> given,
        IReadOnlyDictionary<string, IReadOnlyList<string>> merged)
    {
        var enabled = request.EnabledItemIds(merged);
        var questions = request.Questions().Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var givenIds = given.Select(x => x.ItemId).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. earlier
                .Where(x => !givenIds.Contains(x.Key) && enabled.Contains(x.Key))
                .Select(x => new InformationResponseItem { ItemId = x.Key, Answers = [.. x.Value.Select(value => new InformationAnswer { Value = value })] }),
            .. given.Where(x => !questions.Contains(x.ItemId) || enabled.Contains(x.ItemId)),
        ];
    }

    // The agent answers another questionnaire, or questions that were asked again since the token was
    // issued (#442): nothing is submitted, and the current questions are returned.
    private static CallToolResult? NotThePendingQuestions(InformationRequest pending, string informationRequestId, AnswersState? token)
    {
        var message =
            !string.Equals(pending.InformationRequestId, informationRequestId, StringComparison.Ordinal)
                ? $"The process is waiting for answers to '{pending.InformationRequestId}', not '{informationRequestId}'. Ask the user the current questions."
            : token is not null && !string.Equals(token.ExchangeId, pending.ExchangeId, StringComparison.Ordinal)
                ? $"The questions of '{informationRequestId}' were asked again since these answers were given; nothing was submitted. Ask the user the current questions."
            : null;

        return message is null
            ? null
            : Error(
                McpErrorCodes.InvalidArguments,
                message,
                ForAgent(pending.NextRound(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)) ?? pending));
    }

    // The answers belong to the request the form showed; when the process now asks something else, the
    // form is not submitted and the current state is returned instead.
    private async Task<CallToolResult?> StaleFormAsync(
        ContinuationState state,
        PendingQuestions pending,
        ToolCatalog catalog,
        CancellationToken cancellationToken) =>
        // The exchange the form was shown for (#442): the same questionnaire asked again is another one.
        string.Equals(pending.Request.InformationRequestId, state.InformationRequestId, StringComparison.Ordinal) &&
        string.Equals(pending.Request.ExchangeId, state.ExchangeId, StringComparison.Ordinal)
            ? null
            : await StateAsync(
                state.Service,
                state.ProcessId,
                catalog,
                "The process is now asking different questions; the answered form was not submitted.",
                cancellationToken);

    // The agent's answers: [{ itemId, values: [...] }], values as given (numbers and booleans as written).
    private static List<InformationResponseItem> AnswersArgument(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var items = new List<InformationResponseItem>();

        if (!arguments.TryGetValue("answers", out var answers) || answers.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

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

        return items;
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
        var elicited = call.InputResponses?.TryGetValue(AnswersInputKey, out var input) == true
            ? input.Deserialize(InputResponse.ElicitResultJsonTypeInfo)
            : null;

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

        if (await StaleFormAsync(state, pending, catalog, cancellationToken) is { } stale)
        {
            return stale;
        }

        var merged = Merge(
            state.Answers ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            elicitor.ToAnswers(pending.Request, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)));

        // The answers decide further questions of the same request: the next form, nothing submitted yet.
        if (pending.Request.NextRound(merged) is not null)
        {
            return AskForAnswers(state.Service, state.ProcessId, pending.Request, state.Round, answered, merged);
        }

        return await SubmitFormAnswersAsync(state, pending, Enabled(pending.Request, merged), call with { InputResponses = null, RequestState = null }, catalog, cancellationToken);
    }

    // The last form's answers: confirmed when the step needs it (#516), then submitted, and the
    // follow-up goes on with this request counted as answered.
    private async Task<ToolOutcome> SubmitFormAnswersAsync(
        ContinuationState state,
        PendingQuestions pending,
        InformationResponseItem[] items,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        var confirmed = new ConfirmedAnswers(
            state.Service,
            state.InformationRequestId,
            state.ExchangeId,
            items,
            state.Round + 1,
            [.. state.Answered, state.InformationRequestId]);

        if (await ConfirmAnswersAsync(pending.Step, state.ProcessId, confirmed, call, cancellationToken) is { } unconfirmed)
        {
            return unconfirmed;
        }

        var response = await SubmitAsync(state.Service, pending.Step, pending.Request, state.ExchangeId, items, state.ProcessId, cancellationToken);

        return await FollowUpAsync(
            state.Service,
            response,
            catalog,
            call,
            new JsonArray([.. confirmed.Answered.Select(x => (JsonNode?)x)]),
            confirmed.Round,
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
            return Error(McpErrorCodes.ServiceError, $"Process {processId} was not found on '{service}', or you may not see it.", Serialize(new { status = 404 }));
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
            ["next"] = Next(service, state.RequiredStep, state.AvailableSteps, catalog, elicited: false, processId, notice: state.Notice),
        };

        if (state.Notice is not null)
        {
            summary["notice"] = Serialize(state.Notice);
            note ??= NotPermittedNote;
        }

        if (note is not null)
        {
            summary["note"] = note;
        }

        return Success(summary);
    }

    // The state of the process on the service it was handed off to. The step has completed by now: a
    // failure to read where it continues must not be reported as the step failing (#537).
    private async Task<(ProcessStateResponse? State, string? Unread)> TargetStateAsync(string target, Guid processId, CancellationToken cancellationToken)
    {
        try
        {
            return (await Processor(target).GetProcessStateAsync(processId, cancellationToken), null);
        }
        catch (Exception exception) when (exception is KaleidoHttpClientException or HttpRequestException or MissingClientException)
        {
            LogTargetStateUnread(logger, exception, target, processId);

            return (null, $"The step completed. The process continues on {target}, whose next steps could not be read; check them with {ToolCatalogBuilder.GetProcessStateTool}.");
        }
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
            var notice = step.Notice;

            if (!string.IsNullOrWhiteSpace(step.TargetProcessorName))
            {
                pendingService = step.TargetProcessorName;

                var (targetState, unread) = await TargetStateAsync(pendingService, step.ProcessId, cancellationToken);

                note ??= unread;
                required = targetState?.RequiredStep;
                available = targetState?.AvailableSteps ?? [];
                notice = targetState?.Notice;
            }

            var mode = ElicitationMode(call);

            if (required?.InformationRequest is { } pending &&
                step.Status == StepExecutionStatus.Completed &&
                mode != Elicitation.None)
            {
                if (round >= options.MaxElicitationRounds)
                {
                    note = "The process asked more rounds of questions than are put to the user automatically; ask the user the remaining questions.";
                }
                else if (mode == Elicitation.Mrtr)
                {
                    // Return the form; the client shows it and calls this tool again with the answers.
                    return AskForAnswers(pendingService, step.ProcessId, pending, round, answered, new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
                }
                else
                {
                    var (next, unanswered) = await AnswerInSessionAsync(call, pendingService, required, pending, step.ProcessId, cancellationToken);

                    if (next is not null)
                    {
                        answered.Add(pending.InformationRequestId);
                        service = pendingService;
                        step = next;

                        continue;
                    }

                    note = unanswered;
                }
            }

            return Success(
                Summary(service, pendingService, step, new Continuation(required, available, notice), answered, note, catalog),
                step.BusinessMessages.Select(x => x.Message));
        }
    }

    // Puts the questions to the user and submits the answers. The step before has completed by now: a
    // failure to ask or to submit is a note on its summary, never the step failing (#537, KAL0022).
    private async Task<(StepExecutionResponse<JsonElement>? Next, string? Note)> AnswerInSessionAsync(
        KaleidoToolCall call,
        string service,
        ProcessRequiredStep required,
        InformationRequest pending,
        Guid processId,
        CancellationToken cancellationToken)
    {
        SessionAnswers session;

        try
        {
            session = await ElicitEveryRoundAsync(call, pending, cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogAnswersNotTaken(logger, exception, processId);
            return (null, "The step completed, but its questions could not be put to the user. Ask the user the questions; the process is waiting for them.");
        }

        if (session.Answers is not { } given)
        {
            return (null, $"The user did not answer the questions ({session.Action}). Nothing was submitted; the process is still waiting for them.");
        }

        var items = Enabled(pending, given);

        if (await ConfirmAnswersInSessionAsync(call, required, items, processId, cancellationToken) is { } unconfirmed)
        {
            return (null, unconfirmed);
        }

        try
        {
            return (await SubmitAsync(service, required, pending, pending.ExchangeId, items, processId, cancellationToken), null);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogAnswersNotTaken(logger, exception, processId);
            var reason = exception is KaleidoHttpClientException { Errors.Count: > 0 } refused ? $" ({string.Join("; ", refused.Errors.Select(x => x.Message))})" : string.Empty;
            return (null, $"The step completed, but submitting the user's answers failed{reason}. Read the process with {ToolCatalogBuilder.GetProcessStateTool} before trying again.");
        }
    }

    // Session elicitation: one form per round of the same request (the questions the answers so far
    // decide), until nothing is left to ask; null answers when the user declines a form.
    private async Task<SessionAnswers> ElicitEveryRoundAsync(
        KaleidoToolCall call,
        InformationRequest pending,
        CancellationToken cancellationToken)
    {
        var given = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        while (pending.NextRound(given) is { } round)
        {
            var elicited = await call.Server.ElicitAsync(elicitor.ToElicitation(round), cancellationToken);

            if (!elicited.IsAccepted || elicited.Content is not { } content)
            {
                return new SessionAnswers(Answers: null, elicited.Action);
            }

            var answers = Merge(given, elicitor.ToAnswers(round, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)));

            // A form accepted without answers to its questions ends the rounds (never the same form forever).
            if (answers.Count == given.Count && answers.All(x => given.TryGetValue(x.Key, out var before) && before.SequenceEqual(x.Value, StringComparer.Ordinal)))
            {
                return new SessionAnswers(Answers: null, elicited.Action);
            }

            given = answers;
        }

        return new SessionAnswers(given, Action: null);
    }

    // The answers of a round added to those of the earlier rounds (a later answer replaces an earlier one).
    private static Dictionary<string, IReadOnlyList<string>> Merge(
        IReadOnlyDictionary<string, IReadOnlyList<string>> earlier,
        IEnumerable<InformationResponseItem> items)
    {
        var merged = new Dictionary<string, IReadOnlyList<string>>(earlier, StringComparer.Ordinal);

        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.ItemId)))
        {
            merged[item.ItemId] = [.. item.Answers.Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x))];
        }

        return merged;
    }

    // Whether the answers just given make questions askable that were not before (only conditions do).
    private static bool DecidesMoreQuestions(
        InformationRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> before,
        IReadOnlyDictionary<string, IReadOnlyList<string>> after)
    {
        var askable = request.AskableQuestions(before).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        return request.AskableQuestions(after).Any(x => !askable.Contains(x.Id));
    }

    // What a form round submits: the answers of the questions that apply, in presentation order.
    private static InformationResponseItem[] Enabled(
        InformationRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> answers)
    {
        var enabled = request.EnabledItemIds(answers);

        return
        [
            .. request.Questions()
                .Where(x => enabled.Contains(x.Id) && answers.ContainsKey(x.Id))
                .Select(x => new InformationResponseItem
                {
                    ItemId = x.Id,
                    Answers = [.. answers[x.Id].Select(value => new InformationAnswer { Value = value })],
                }),
        ];
    }

    // The exchange id is the one captured when the questions were shown, never re-read: answers to an
    // earlier exchange are then refused by the service, not taken for answers to the current one (#442).
    private Task<StepExecutionResponse<JsonElement>> SubmitAsync(
        string service,
        ProcessRequiredStep step,
        InformationRequest request,
        string exchangeId,
        IReadOnlyList<InformationResponseItem> items,
        Guid processId,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToElement(
            new JsonObject
            {
                ["informationRequestId"] = request.InformationRequestId,
                ["exchangeId"] = exchangeId,
                ["items"] = Serialize(items),
            },
            KaleidoJsonOptions.Options);

        // An information request is answered once: the same answers to the same request are one call,
        // however often the agent sends them (#440).
        return Processor(service).ExecuteStepAsync(
            step.Name,
            body,
            processId,
            IdempotencyKey(service, step.Name, processId, body.GetRawText()),
            cancellationToken);
    }

    // "mcp-" and 40 hex characters of SHA-256 over what makes the call this call: well within the
    // service's 200-character limit, and never a value an agent chose.
    private static string IdempotencyKey(string service, string step, Guid processId, string body) =>
        "mcp-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes($"{service}\n{step}\n{processId:D}\n{body}")))[..40].ToLowerInvariant();

    // A JSON value with its object properties in ordinal order, recursively, so equal arguments always
    // serialize to the same text.
    private static JsonNode? Canonical(JsonNode? node) =>
        node switch
        {
            JsonObject obj => new JsonObject(
                obj.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => KeyValuePair.Create(x.Key, Canonical(x.Value?.DeepClone())))),
            JsonArray array => new JsonArray([.. array.Select(x => Canonical(x?.DeepClone()))]),
            _ => node,
        };

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

    // Where the process goes next as the user may see it: the steps they may run, and the notice when
    // it continues with steps they may not (#524).
    private sealed record Continuation(
        ProcessRequiredStep? Required,
        IReadOnlyCollection<ProcessStepSummary> Available,
        KaleidoNotice? Notice);

    private const string NotPermittedNote =
        "The process continues with a step this user may not run. Tell them they do not have access to continue it; do not guess or name another step.";

    private JsonObject Summary(
        string service,
        string pendingService,
        StepExecutionResponse<JsonElement> step,
        Continuation continuation,
        JsonArray answered,
        string? note,
        ToolCatalog catalog)
    {
        var (required, available, notice) = continuation;
        var summary = new JsonObject
        {
            ["processId"] = step.ProcessId.ToString(),
            ["service"] = service,
            ["step"] = step.StepName,
            ["outcome"] = Camel(step.Status.ToString()),
        };

        if (step.Result.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            summary["result"] = JsonNode.Parse(step.Result.GetRawText());
            AddFieldDescriptions(summary, catalog.FindStep(service, step.StepName), summary["result"]);
        }

        // Two streams, kept apart as the step response keeps them: business messages are the
        // implementer's words for the user (verbatim, always); framework messages are Kaleido's
        // diagnostics about the call, for the agent to explain, and only when they matter (warnings
        // and errors, not informational notes such as a repeatable step).
        if (step.BusinessMessages.Count > 0)
        {
            summary["messages"] = Serialize(step.BusinessMessages);
        }

        var diagnostics = step.FrameworkMessages.Where(x => x.Type != MessageType.Information).ToArray();

        if (diagnostics.Length > 0)
        {
            summary["diagnostics"] = Serialize(diagnostics);
        }

        if (answered.Count > 0)
        {
            summary["answered"] = answered.DeepClone();
        }

        if (!string.IsNullOrWhiteSpace(step.TargetProcessorName))
        {
            summary["handoff"] = step.TargetProcessorName;
        }

        summary["next"] = Next(pendingService, required, available, catalog, elicited: answered.Count > 0, step.ProcessId, notice: notice);

        if (notice is not null)
        {
            summary["notice"] = Serialize(notice);
            note ??= NotPermittedNote;
        }

        note ??= UnfinishedNote(step.Status, canContinue: required is not null || available.Count > 0);

        if (note is not null)
        {
            summary["note"] = note;
        }

        return summary;
    }

    // A rejected or unfinished step (invalid input, a defect, a cancellation) changed nothing: the user
    // can correct the input and retry, or choose another step.
    private static string? UnfinishedNote(StepExecutionStatus status, bool canContinue) =>
        status switch
        {
            StepExecutionStatus.Rejected =>
                "The step was rejected and nothing was changed. Tell the user why (messages verbatim; explain diagnostics); they can correct it and try again, or choose another step.",
            StepExecutionStatus.ValidationFailed or StepExecutionStatus.Exception or StepExecutionStatus.Canceled when canContinue =>
                "The step did not complete and nothing was recorded; the process is where it was. The user can try again or choose another step.",
            _ => null,
        };

    // The form of the round the answers so far decide; the state carries those answers to the next call.
    private ToolOutcome AskForAnswers(
        string service,
        Guid processId,
        InformationRequest pending,
        int round,
        JsonArray answered,
        IReadOnlyDictionary<string, IReadOnlyList<string>> answersSoFar) =>
        new()
        {
            InputRequired = new InputRequiredResult
            {
                InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                {
                    [AnswersInputKey] = InputRequest.ForElicitation(elicitor.ToElicitation(pending.NextRound(answersSoFar) ?? pending)),
                },
                RequestState = EncodeState(new ContinuationState(
                    service,
                    processId,
                    pending.InformationRequestId,
                    pending.ExchangeId,
                    round,
                    answered.Select(x => x?.GetValue<string>()).OfType<string>().ToArray(),
                    answersSoFar)),
            },
        };

    private JsonObject Next(
        string service,
        ProcessRequiredStep? required,
        IReadOnlyCollection<ProcessStepSummary> available,
        ToolCatalog catalog,
        bool elicited,
        Guid? processId,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? answersSoFar = null,
        KaleidoNotice? notice = null)
    {
        var next = new JsonObject { ["service"] = service };

        if (required is { InformationRequest: { } request })
        {
            // Only the questions that apply now (ADR 0024): Kaleido applies the conditions, the agent
            // never sees them. Later rounds carry the earlier answers in answersToken.
            var given = answersSoFar ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var pending = request.NextRound(given) ?? request;
            var moreRounds = given.Count > 0;

            next["required"] = new JsonObject
            {
                ["tool"] = ToolCatalogBuilder.AnswerInformationRequestTool,
                ["step"] = required.Name,
                ["displayName"] = required.DisplayName,
            };
            next["informationRequest"] = ForAgent(pending);

            // The token ties the answers to the exchange these questions belong to (#442), and carries
            // the answers of earlier rounds.
            if (processId is { } id)
            {
                next[AnswersTokenArgument] = EncodeAnswers(id, request.InformationRequestId, request.ExchangeId, given);
            }

            next["instruction"] = AnswerInstruction(pending, elicited, moreRounds, withToken: processId is not null);
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

        // #516: what the caller cannot see is never called complete. The service offers nothing: no
        // further steps (get_process_state names the state). It offers steps the caller has no tool for,
        // or says the caller may not continue: the caller is told so.
        if (required is null && tools.Length == 0)
        {
            next["note"] = available.Count == 0 && notice is null
                ? "No further steps here."
                : $"No step here is available to you ({ProcessorErrorCodes.NotPermitted}): tell the user they do not have access to continue it; do not guess another step.";
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

    // What the fields of a result mean, for those it contains, so the agent reads the data with its
    // meaning (#213). Nothing is added when the tool has no described fields in the data.
    private static void AddFieldDescriptions(JsonObject answer, KaleidoTool? tool, JsonNode? data)
    {
        if (tool is not { OutputDescriptions.Count: > 0 })
        {
            return;
        }

        var described = new JsonObject();

        foreach (var name in FieldNames(data).Where(tool.OutputDescriptions.ContainsKey))
        {
            described[name] = tool.OutputDescriptions[name];
        }

        if (described.Count > 0)
        {
            answer["fieldDescriptions"] = described;
        }
    }

    // The top-level field names of a result object, or of the rows of a result list.
    private static IEnumerable<string> FieldNames(JsonNode? data) =>
        data switch
        {
            JsonObject item => item.Select(x => x.Key),
            JsonArray rows => rows.OfType<JsonObject>().SelectMany(row => row.Select(x => x.Key)),
            _ => [],
        };

    // How the agent puts pending questions to the user: verbatim, the user's answers only.
    private static string AnswerInstruction(InformationRequest pending, bool elicited, bool moreRounds, bool withToken) =>
        (elicited ? "More questions followed. " : string.Empty) +
        (moreRounds ? "The answers given so far lead to these further questions; nothing was submitted yet. " : string.Empty) +
        "Ask the user every question exactly as written and submit only the user's answers with answer_information_request (service, processId, informationRequestId, answers" +
        (withToken ? ", and this answersToken unchanged" : string.Empty) + (moreRounds ? "; send only the new answers" : string.Empty) + "). " +
        (HasProposals(pending.Items)
            ? "Questions with proposedAnswers were pre-filled from what was said earlier: read each one back with its proposed answer and have the user confirm or correct it. "
            : string.Empty) +
        "If the user already answered a question earlier in this conversation, read that answer back for confirmation instead of asking again. Never answer for the user, and never submit a proposed answer the user has not confirmed.";

    private static bool HasProposals(IEnumerable<InformationItem> items) =>
        items.Any(x => x.ProposedAnswers.Count > 0 || HasProposals(x.Items));

    // ── Continuation state (MRTR) and the answers token ──────────────────────

    // Protected (#516): encrypted, signed, bound to the caller and short-lived, so the client can
    // neither read, change nor replay it. It only says which process and round the form belongs to;
    // the pending questions and the URL to submit to are always read fresh from Kaleido.
    private string EncodeState(ContinuationState state) =>
        states.Protect(McpStateKind.Continuation, state);

    // The answers of earlier rounds of one request, for the agent to send back with the next round
    // (ADR 0024), protected like the continuation state (#516). It must also name this process and
    // request, and Kaleido validates the full set of answers when they are submitted.
    private string EncodeAnswers(Guid processId, string informationRequestId, string exchangeId, IReadOnlyDictionary<string, IReadOnlyList<string>> answers) =>
        states.Protect(McpStateKind.Answers, new AnswersState(processId, informationRequestId, exchangeId, answers));

    // The token's earlier answers and the exchange it was issued for; null when there is no usable
    // token (none, refused by the protector, or for another process or questionnaire).
    private AnswersState? DecodeAnswers(string? value, Guid processId, string informationRequestId) =>
        value is not null &&
        states.Unprotect<AnswersState>(McpStateKind.Answers, value) is { } state &&
        state.ProcessId == processId &&
        string.Equals(state.InformationRequestId, informationRequestId, StringComparison.Ordinal)
            ? state
            : null;

    private ContinuationState? DecodeState(string? value) =>
        states.Unprotect<ContinuationState>(McpStateKind.Continuation, value);

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

    // Questions as the agent receives them: the round's questions without condition fields - Kaleido
    // applies the conditions; the agent only asks (ADR 0024).
    private static JsonNode? ForAgent(InformationRequest round)
    {
        var node = Serialize(round);
        WithoutConditions(node);
        return node;
    }

    private static void WithoutConditions(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject item:
                item.Remove("enableWhen");
                item.Remove("enableBehavior");

                foreach (var child in item.Select(x => x.Value).ToArray())
                {
                    WithoutConditions(child);
                }

                break;

            case JsonArray items:
                foreach (var child in items)
                {
                    WithoutConditions(child);
                }

                break;
        }
    }

    private static JsonNode? Serialize<T>(T value) =>
        JsonSerializer.SerializeToNode(value, KaleidoJsonOptions.Options);

    private static string Camel(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

    private static CallToolResult Success(JsonNode body, IEnumerable<string>? forTheUser = null) =>
        new()
        {
            Content = Content(body, forTheUser),
        };

    private static CallToolResult Error(
        string code,
        string message,
        JsonNode? details = null,
        IEnumerable<string>? forTheUser = null)
    {
        var body = new JsonObject { ["code"] = code, ["message"] = message };

        if (details is not null)
        {
            body["details"] = details.DeepClone();
        }

        return new CallToolResult
        {
            IsError = true,
            Content = Content(body, forTheUser),
        };
    }

    // The JSON summary is for the agent. Text the service wrote for a person (business messages, its
    // own refusal reasons) also goes in blocks of its own marked for the user, so a client that
    // honours MCP's audience annotation can show it as written, without the model in between
    // (ADR 0007). A client that ignores annotations passes every block to the model, which the
    // instructions tell to relay messages verbatim.
    private static List<ContentBlock> Content(JsonNode body, IEnumerable<string>? forTheUser)
    {
        List<ContentBlock> content =
        [
            new TextContentBlock
            {
                Text = body.ToJsonString(KaleidoJsonOptions.Options),
                Annotations = new Annotations { Audience = [Role.Assistant] },
            },
        ];

        foreach (var text in forTheUser ?? [])
        {
            content.Add(new TextContentBlock
            {
                Text = text,
                Annotations = new Annotations { Audience = [Role.User], Priority = 1 },
            });
        }

        return content;
    }

    // A refusal reason the service itself wrote (e.g. MEMBER_PLAN_REQUIRED), as opposed to one of
    // Kaleido's own codes, which the agent explains instead.
    private static readonly string[] KaleidoCodePrefixes = ["qry_", "pro_", "httpclient_", "mcp_"];

    private static IEnumerable<string> ServiceReasons(IEnumerable<KaleidoError> errors) =>
        errors
            .Where(x =>
                x.Code is not (KaleidoErrorCodes.ArgumentError or KaleidoErrorCodes.FrameworkError) &&
                !KaleidoCodePrefixes.Any(prefix => x.Code.StartsWith(prefix, StringComparison.Ordinal)) &&
                !string.IsNullOrWhiteSpace(x.Message))
            .Select(x => x.Message);

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
        string ExchangeId,
        int Round,
        IReadOnlyList<string> Answered,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? Answers = null);

    private sealed record AnswersState(
        Guid ProcessId,
        string InformationRequestId,
        string ExchangeId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Answers);

    private sealed record SessionAnswers(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? Answers,
        string? Action);

    private sealed record PendingQuestions(
        ProcessRequiredStep Step,
        InformationRequest Request,
        CallToolResult? Error)
    {
        public static PendingQuestions Failed(CallToolResult error) =>
            new(new ProcessRequiredStep { Name = string.Empty }, new InformationRequest { InformationRequestId = string.Empty }, error);
    }

    // Debug: the Kaleido client already logged the service's non-success answer at Warning (one
    // signal per failure); this line only ties it to the tool.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP tool {Tool}: the service answered {StatusCode} ({Code}).")]
    private static partial void LogServiceAnswered(ILogger logger, string tool, int statusCode, string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP tool {Tool}: the service could not be reached.")]
    private static partial void LogServiceUnreachable(ILogger logger, Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: after a completed step of process {ProcessId}, its questions could not be put to the user or the answers could not be submitted.")]
    private static partial void LogAnswersNotTaken(ILogger logger, Exception exception, Guid processId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: the state of process {ProcessId} on {Target}, where a completed step handed it off, could not be read.")]
    private static partial void LogTargetStateUnread(ILogger logger, Exception exception, string target, Guid processId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido MCP tool {Tool} failed unexpectedly.")]
    private static partial void LogToolFailed(ILogger logger, Exception exception, string tool);
}
