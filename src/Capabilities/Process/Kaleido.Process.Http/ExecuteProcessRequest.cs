using System.Text.Json;

namespace Kaleido.Process.Http;

/// <summary>
/// The HTTP body of a multi-step execute request: step names and their JSON inputs. The HTTP
/// adapter authorizes and reads every step into its declared CLR type before calling Process.
/// </summary>
/// <remarks>
/// An existing process is continued by its id in <c>X-Kaleido-Process-Id</c>, not in this body.
/// The transport rejects an unknown step, duplicate name or invalid JSON input before execution.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ExecuteProcessRequest
{
    /// <summary>The submitted steps and inputs, or an empty collection.</summary>
    public IReadOnlyCollection<ProcessStepRequest> Steps { get; init; } = [];
}

/// <summary>One named step and its raw JSON input in an HTTP multi-step execute request.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepRequest
{
    /// <summary>The published step name, matched without case.</summary>
    public required string StepName { get; init; }

    /// <summary>The step input read into its declared CLR type by the HTTP adapter.</summary>
    public required JsonElement Request { get; init; }
}
