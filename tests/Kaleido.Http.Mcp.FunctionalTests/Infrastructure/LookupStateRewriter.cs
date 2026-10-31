using System.Text;
using System.Text.Json.Nodes;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// Sits in the SDK client's HTTP pipeline: sees, and may replace, the state the client sends back on
/// each MRTR retry - the guided-lookup state by default (#452), or another kind by its prefix (#516).
/// <c>rewrite</c> gets the retry's index and the state.
/// </summary>
public sealed class LookupStateRewriter(Func<int, string, string> rewrite, string prefix = LookupStateRewriter.StatePrefix) : DelegatingHandler
{
    public const string StatePrefix = "kaleido-mcp-lookup:";

    public const string ConfirmationPrefix = "kaleido-mcp-confirm:";

    public LookupStateRewriter(Func<string, string> rewrite, string prefix = StatePrefix)
        : this((_, state) => rewrite(state), prefix)
    {
    }

    /// <summary>A rewriter that sends every state back unchanged.</summary>
    public static LookupStateRewriter Unchanged() => new((_, state) => state);

    /// <summary>The states the client sent back, as the server issued them.</summary>
    public List<string> Seen { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null &&
            JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken)) is JsonObject message &&
            message["params"]?["requestState"]?.GetValue<string>() is { } state &&
            state.StartsWith(prefix, StringComparison.Ordinal))
        {
            message["params"]!["requestState"] = rewrite(Seen.Count, state);
            Seen.Add(state);
            request.Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
