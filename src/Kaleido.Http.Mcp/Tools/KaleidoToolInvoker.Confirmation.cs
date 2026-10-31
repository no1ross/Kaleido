using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Processor;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

// The confirmation round (#439): with KaleidoMcpOptions.RequireConfirmation, a step declared
// RequiresConfirmation runs only after the user accepts a form naming the step and its inputs - in
// the session (elicitation) or as an MRTR round. What the agent says or adds to the arguments is
// never a confirmation. It raises the bar and is not proof of consent: the client relays the form.
// An information step is confirmed the same way, after its last answers and before they are
// submitted, on every path that submits them (#516).
internal sealed partial class KaleidoToolInvoker
{
    private const string ConfirmInputKey = "kaleido-confirm";
    private const string ConfirmItemId = "confirm";

    // Null when the step may run now; otherwise what the caller gets instead (the form, a refusal,
    // or "not run").
    private async Task<ToolOutcome?> ConfirmAsync(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        Guid? processId,
        KaleidoToolCall call,
        CancellationToken cancellationToken)
    {
        if (!NeedsConfirmation(tool.RequiresConfirmation))
        {
            return null;
        }

        switch (ElicitationMode(call))
        {
            case Elicitation.None:
                return Error(
                    McpErrorCodes.ConfirmationRequired,
                    $"{tool.Definition.Name} needs the user's confirmation before it runs, and this client cannot show the user a form. Nothing was run. Tell the user; they can run it from a client that shows forms.");

            case Elicitation.Mrtr:
                return new ToolOutcome
                {
                    InputRequired = new InputRequiredResult
                    {
                        InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                        {
                            [ConfirmInputKey] = InputRequest.ForElicitation(StepConfirmationForm(tool, arguments)),
                        },
                        RequestState = states.Protect(
                            McpStateKind.Confirmation,
                            new ConfirmationState(tool.Definition.Name, processId, JsonSerializer.Serialize(arguments, KaleidoJsonOptions.Options))),
                    },
                };

            default:
                var answer = await call.Server.ElicitAsync(StepConfirmationForm(tool, arguments), cancellationToken);

                if (Confirmed(answer))
                {
                    return null;
                }

                return NotConfirmed(tool, answer.Action);
        }
    }

    // The MRTR retry: run the step with the confirmed call's arguments only when the user accepted.
    private async Task<ToolOutcome> ContinueConfirmationAsync(
        string requestState,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        var state = states.Unprotect<ConfirmationState>(McpStateKind.Confirmation, requestState);

        if (state is { Answers: { } answers, ProcessId: { } processId })
        {
            return await ContinueConfirmedAnswersAsync(answers, processId, call, catalog, cancellationToken);
        }

        if (state is null || catalog.Find(state.Tool) is not { Kind: KaleidoToolKind.Step } tool)
        {
            return Error(McpErrorCodes.InvalidArguments, "The confirmation state is not readable; call the tool again.");
        }

        var answer = call.InputResponses?.TryGetValue(ConfirmInputKey, out var input) == true
            ? input.Deserialize(InputResponse.ElicitResultJsonTypeInfo)
            : null;

        if (!Confirmed(answer))
        {
            return NotConfirmed(tool, answer?.Action ?? "no answer");
        }

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(state.Arguments, KaleidoJsonOptions.Options) ?? [];

        return await RunStepAsync(
            tool,
            arguments,
            state.ProcessId,
            call with { InputResponses = null, RequestState = null },
            catalog,
            confirmed: true,
            cancellationToken);
    }

    // The host requires confirmation (KaleidoMcpOptions.RequireConfirmation) and the step declares it:
    // both are opt-in, and both are needed.
    private bool NeedsConfirmation(bool requiresConfirmation) =>
        options.RequireConfirmation && requiresConfirmation;

    // Null when the answers may be submitted now; otherwise what the caller gets instead (#516): the
    // same decision and the same form as for any step, before anything is submitted.
    private async Task<ToolOutcome?> ConfirmAnswersAsync(
        ProcessRequiredStep step,
        Guid processId,
        ConfirmedAnswers answers,
        KaleidoToolCall call,
        CancellationToken cancellationToken)
    {
        if (!NeedsConfirmation(step.RequiresConfirmation))
        {
            return null;
        }

        var form = AnswersConfirmationForm(step, answers.Items);

        switch (ElicitationMode(call))
        {
            case Elicitation.None:
                return Error(
                    McpErrorCodes.ConfirmationRequired,
                    $"{StepTitle(step)} needs the user's confirmation before the answers are submitted, and this client cannot show the user a form. Nothing was submitted; the process is still waiting for the answers. Tell the user; they can answer from a client that shows forms.");

            case Elicitation.Mrtr:
                return new ToolOutcome
                {
                    InputRequired = new InputRequiredResult
                    {
                        InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                        {
                            [ConfirmInputKey] = InputRequest.ForElicitation(form),
                        },
                        RequestState = states.Protect(
                            McpStateKind.Confirmation,
                            new ConfirmationState(ToolCatalogBuilder.AnswerInformationRequestTool, processId, "{}", answers)),
                    },
                };

            default:
                var answer = await call.Server.ElicitAsync(form, cancellationToken);

                if (Confirmed(answer))
                {
                    return null;
                }

                return AnswersNotConfirmed(step, answer.Action);
        }
    }

    // In a session already asking the questions: null when the answers may be submitted, otherwise
    // the note for the step's summary. A form that cannot be shown is a note too, never the step failing.
    private async Task<string?> ConfirmAnswersInSessionAsync(
        KaleidoToolCall call,
        ProcessRequiredStep step,
        InformationResponseItem[] items,
        Guid processId,
        CancellationToken cancellationToken)
    {
        if (!NeedsConfirmation(step.RequiresConfirmation))
        {
            return null;
        }

        ElicitResult answer;

        try
        {
            answer = await call.Server.ElicitAsync(AnswersConfirmationForm(step, items), cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogAnswersNotTaken(logger, exception, processId);
            return $"The user's answers to {StepTitle(step)} could not be confirmed with them. Ask the user the questions; the process is waiting for them.";
        }

        return Confirmed(answer)
            ? null
            : $"The user did not confirm submitting the answers to {StepTitle(step)} ({answer.Action}). Nothing was submitted; the process is still waiting for them.";
    }

    // The MRTR retry of an information step's confirmation: submit the confirmed answers only when the
    // user accepted, and only to the questions they answered (the same request and exchange).
    private async Task<ToolOutcome> ContinueConfirmedAnswersAsync(
        ConfirmedAnswers answers,
        Guid processId,
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        var answer = call.InputResponses?.TryGetValue(ConfirmInputKey, out var input) == true
            ? input.Deserialize(InputResponse.ElicitResultJsonTypeInfo)
            : null;

        if (!Confirmed(answer))
        {
            return await StateAsync(
                answers.Service,
                processId,
                catalog,
                $"The user did not confirm submitting the answers ({answer?.Action ?? "no answer"}). Nothing was submitted; the process is still waiting for them.",
                cancellationToken);
        }

        var pending = await PendingAsync(answers.Service, processId, cancellationToken);

        if (pending.Error is { } error)
        {
            return error;
        }

        if (!string.Equals(pending.Request.InformationRequestId, answers.InformationRequestId, StringComparison.Ordinal) ||
            !string.Equals(pending.Request.ExchangeId, answers.ExchangeId, StringComparison.Ordinal))
        {
            return await StateAsync(answers.Service, processId, catalog, "The process is now asking different questions; the confirmed answers were not submitted.", cancellationToken);
        }

        var response = await SubmitAsync(answers.Service, pending.Step, pending.Request, answers.ExchangeId, answers.Items, processId, cancellationToken);

        return await FollowUpAsync(
            answers.Service,
            response,
            catalog,
            call with { InputResponses = null, RequestState = null },
            new JsonArray([.. answers.Answered.Select(x => (JsonNode?)x)]),
            answers.Round,
            cancellationToken);
    }

    private static CallToolResult AnswersNotConfirmed(ProcessRequiredStep step, string action) =>
        Success(new JsonObject
        {
            ["step"] = step.Name,
            ["outcome"] = "notRun",
            ["note"] = $"The user did not confirm submitting the answers to {StepTitle(step)} ({action}). Nothing was submitted; the process is still waiting for them.",
        });

    private static string StepTitle(ProcessStepContract step) => step.DisplayName ?? step.Name;

    // Accepted, with the confirmation checked: nothing else counts (decline, cancel, unchecked, missing).
    private static bool Confirmed(ElicitResult? answer) =>
        answer is { IsAccepted: true, Content: { } content } &&
        content.TryGetValue(ConfirmItemId, out var value) &&
        value.ValueKind == JsonValueKind.True;

    private static CallToolResult NotConfirmed(KaleidoTool tool, string action) =>
        Success(new JsonObject
        {
            ["step"] = tool.CapabilityName,
            ["outcome"] = "notRun",
            ["note"] = $"The user did not confirm {Title(tool)} ({action}). Nothing was run.",
        });

    // What the user knows the step as: its display name, or the tool name when it has none.
    private static string Title(KaleidoTool tool) => tool.Definition.Title ?? tool.Definition.Name;

    // The step and the inputs it will run with.
    private static ElicitRequestParams StepConfirmationForm(KaleidoTool tool, IReadOnlyDictionary<string, JsonElement> arguments) =>
        ConfirmationForm(
            Title(tool),
            tool.Definition.Annotations?.DestructiveHint == true,
            "Inputs",
            arguments
                .Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}: {x.Value.GetRawText()}"));

    // The information step and the answers that will be submitted.
    private static ElicitRequestParams AnswersConfirmationForm(ProcessRequiredStep step, IEnumerable<InformationResponseItem> items) =>
        ConfirmationForm(
            $"submit the answers to {StepTitle(step)}",
            step.Destructive,
            "Answers",
            items.Select(x => $"{x.ItemId}: {string.Join(" / ", x.Answers.Select(a => a.Value))}"));

    // One checkbox, unchecked: the message names what runs, what the implementer declared about it,
    // and what it runs with.
    private static ElicitRequestParams ConfirmationForm(string title, bool destructive, string inputsLabel, IEnumerable<string> lines)
    {
        var message = new StringBuilder()
            .Append("Confirm: ").Append(title).Append('.');

        if (destructive)
        {
            message.Append(" It cannot be undone.");
        }

        var inputs = lines.ToArray();

        if (inputs.Length > 0)
        {
            message.AppendLine().Append(inputsLabel).Append(": ").AppendJoin(", ", inputs);
        }

        return new ElicitRequestParams
        {
            Message = message.ToString(),
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>(StringComparer.Ordinal)
                {
                    [ConfirmItemId] = new ElicitRequestParams.BooleanSchema
                    {
                        Title = $"Run {title} now",
                        Default = false,
                    },
                },
                Required = [ConfirmItemId],
            },
        };
    }

    // Protected (#516): it carries exactly the call the user confirmed, so the client can neither change
    // what runs nor replay it; the step's service still checks the values it receives, as for any call.
    // For an information step it carries the answers instead of arguments.
    private sealed record ConfirmationState(string Tool, Guid? ProcessId, string Arguments, ConfirmedAnswers? Answers = null);

    // An information step's answers waiting for the user's confirmation: where they are submitted, the
    // exchange they answer, and how the follow-up goes on (the rounds and requests answered so far).
    private sealed record ConfirmedAnswers(
        string Service,
        string InformationRequestId,
        string ExchangeId,
        InformationResponseItem[] Items,
        int Round,
        string[] Answered);
}
