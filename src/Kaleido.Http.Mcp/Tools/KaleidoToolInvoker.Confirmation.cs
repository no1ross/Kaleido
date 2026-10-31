using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

// The confirmation round (#439): with KaleidoMcpOptions.RequireConfirmation, a step declared
// RequiresConfirmation runs only after the user accepts a form naming the step and its inputs - in
// the session (elicitation) or as an MRTR round. What the agent says or adds to the arguments is
// never a confirmation. It raises the bar and is not proof of consent: the client relays the form.
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
        if (!options.RequireConfirmation || !tool.RequiresConfirmation)
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
                            [ConfirmInputKey] = InputRequest.ForElicitation(ConfirmationForm(tool, arguments)),
                        },
                        RequestState = states.Protect(
                            McpStateKind.Confirmation,
                            new ConfirmationState(tool.Definition.Name, processId, JsonSerializer.Serialize(arguments, KaleidoJsonOptions.Options))),
                    },
                };

            default:
                var answer = await call.Server.ElicitAsync(ConfirmationForm(tool, arguments), cancellationToken);

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
        if (states.Unprotect<ConfirmationState>(McpStateKind.Confirmation, requestState) is not { } state ||
            catalog.Find(state.Tool) is not { Kind: KaleidoToolKind.Step } tool)
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

    // One checkbox, unchecked: the message names the step, what the implementer declared about it,
    // and the inputs it will run with.
    private static ElicitRequestParams ConfirmationForm(KaleidoTool tool, IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var title = Title(tool);
        var message = new StringBuilder()
            .Append("Confirm: ").Append(title).Append('.');

        if (tool.Definition.Annotations?.DestructiveHint == true)
        {
            message.Append(" It cannot be undone.");
        }

        var inputs = arguments
            .Where(x => !string.Equals(x.Key, "processId", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}: {x.Value.GetRawText()}")
            .ToArray();

        if (inputs.Length > 0)
        {
            message.AppendLine().Append("Inputs: ").AppendJoin(", ", inputs);
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
    private sealed record ConfirmationState(string Tool, Guid? ProcessId, string Arguments);
}
