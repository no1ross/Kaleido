namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// In a host with authentication, the correlation identity headers (request id, calling processor
/// and step) are honoured for an authenticated caller and ignored for an anonymous one: the
/// correlation filter runs inside Kaleido's endpoints, after the host's authentication (#295).
/// </summary>
public sealed class CorrelationIdentityTrustTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private const string RegistryUrl = "/kaleido/registry";

    private async Task<string?> EchoedRequestIdAsync(HttpRequestMessage request)
    {
        request.Headers.Add(KaleidoCorrelationHeaders.RequestId, "req-42");

        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var values)
            ? values.Single()
            : null;
    }

    [Fact]
    public async Task AnAuthenticatedCaller_KeepsItsRequestId()
    {
        using var request = AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, RegistryUrl);

        Assert.Equal("req-42", await EchoedRequestIdAsync(request));
    }

    [Fact]
    public async Task AnAnonymousCaller_GetsANewRequestId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RegistryUrl);

        var echoed = await EchoedRequestIdAsync(request);

        Assert.NotEqual("req-42", echoed);
        Assert.True(Guid.TryParse(echoed, out _));
    }
}
