using System.Text.Json;

namespace Kaleido.Process.Http;

/// <summary>
/// The HTTP body of a per-step execute endpoint: <c>{"processStep":{...}}</c>. The route names
/// the step; its input is converted to a declared CLR step before the Process runtime receives it.
/// </summary>
/// <typeparam name="TProcessStep">The declared step, a local mirror, or <see cref="JsonElement"/> at the HTTP boundary.</typeparam>
[ExcludeFromCodeCoverage]
public sealed record ExecuteStepRequest<TProcessStep>
{
    /// <summary>The step input supplied by the caller.</summary>
    public required TProcessStep ProcessStep { get; init; }
}
