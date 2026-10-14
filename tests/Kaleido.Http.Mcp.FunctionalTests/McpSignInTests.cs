using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Sign-in from MCP clients (#212): without a token the MCP endpoint answers 401 pointing at the
/// protected-resource metadata, which names the identity provider; with a token everything works as
/// before. Kaleido adds nothing for this - the host combines its own token validation with the MCP
/// SDK's challenge scheme and requires authorization on <c>MapKaleidoMcp()</c>.
/// </summary>
public sealed partial class McpSignInTests(OAuthSignInFixture fixture)
    : IClassFixture<OAuthSignInFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    // A minimal MCP initialize request, as a client sends it first.
    private static HttpRequestMessage Initialize()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return request;
    }

    [GeneratedRegex("resource_metadata=\"(?<url>[^\"]+)\"")]
    private static partial Regex ResourceMetadata();

    private async Task<string> ResourceMetadataUrlAsync(HttpClient http)
    {
        using var response = await http.SendAsync(Initialize(), Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);

        var match = ResourceMetadata().Match(challenge.Parameter ?? string.Empty);
        Assert.True(match.Success, $"No resource_metadata in '{challenge}'.");

        return match.Groups["url"].Value;
    }

    [Fact]
    public async Task WithoutAToken_TheEndpointAnswers401_PointingAtTheProtectedResourceMetadata()
    {
        using var http = fixture.CreateHttpClient();

        var url = await ResourceMetadataUrlAsync(http);

        Assert.Contains("/.well-known/oauth-protected-resource", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMetadata_NamesTheIdentityProviderAndScope_ForTheMcpEndpoint()
    {
        using var http = fixture.CreateHttpClient();
        var url = await ResourceMetadataUrlAsync(http);

        using var response = await http.GetAsync(new Uri(url).PathAndQuery, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));

        Assert.EndsWith("/mcp", metadata.RootElement.GetProperty("resource").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            [McpClinicFixture.AuthorizationServer],
            metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(
            [McpClinicFixture.Scope],
            metadata.RootElement.GetProperty("scopes_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task WithAToken_TheClientSeesItsToolsAsBefore()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);

        Assert.Contains(tools, x => x.Name == "clinic_patient");
    }
}
