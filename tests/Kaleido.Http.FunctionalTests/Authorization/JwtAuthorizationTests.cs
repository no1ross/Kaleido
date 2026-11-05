using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Registry;
using Kaleido.Testing.Identity;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// Capability authorization with real bearer tokens (#574): the roles a provider issues in
/// <c>roles</c> admit the same callers on a typed route, through generic execute and in discovery;
/// a token that isn't the platform's, or isn't signed by the trusted issuer, is not a caller.
/// </summary>
public sealed class JwtAuthorizationTests(JwtAuthorizationFixture fixture)
    : IClassFixture<JwtAuthorizationFixture>
{
    private const string InternalStepUrl = "kaleido/processes/steps/authorizedinternalstep";
    private const string ExecuteUrl = "kaleido/processes/execute";
    private const string RegistryUrl = "kaleido/registry";

    private static readonly object StepBody = new { processStep = new { } };

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AUserWithTheRole_RunsTheStep()
    {
        using var response = await SendAsync(HttpMethod.Post, InternalStepUrl, StepBody, TestTokenIssuer.User("bob", ["internal"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AUserWithoutTheRole_IsForbidden()
    {
        using var response = await SendAsync(HttpMethod.Post, InternalStepUrl, StepBody, TestTokenIssuer.User("alice", ["viewer"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_IsChallengedForABearerToken()
    {
        using var response = await SendAsync(HttpMethod.Post, InternalStepUrl, StepBody, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task ATokenForAnotherAudience_IsNotACaller()
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            InternalStepUrl,
            StepBody,
            TestTokenIssuer.User("bob", ["internal"], audience: "another-api"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AForgedToken_IsNotACaller()
    {
        using var response = await SendAsync(HttpMethod.Post, InternalStepUrl, StepBody, TestTokenIssuer.Forged("bob", ["internal"]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenericExecute_AdmitsTheRoleFromTheToken()
    {
        var body = ExecuteBody(AuthorizationStepNames.InternalStep);

        using var allowed = await SendAsync(HttpMethod.Post, ExecuteUrl, body, TestTokenIssuer.User("bob", ["internal"]));
        using var denied = await SendAsync(HttpMethod.Post, ExecuteUrl, body, TestTokenIssuer.User("alice", ["viewer"]));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task GenericExecute_AdmitsANamedPolicyOverTheToken()
    {
        var body = ExecuteBody(AuthorizationStepNames.PolicyStep);

        using var allowed = await SendAsync(HttpMethod.Post, ExecuteUrl, body, TestTokenIssuer.User("carol", ["clinician"]));
        using var denied = await SendAsync(HttpMethod.Post, ExecuteUrl, body, TestTokenIssuer.User("bob", ["internal"]));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Discovery_ShowsTheStepsTheTokensRolesAllow()
    {
        var withRole = await StepNamesAsync(TestTokenIssuer.User("bob", ["internal"]));
        var withoutRole = await StepNamesAsync(TestTokenIssuer.User("alice", ["viewer"]));

        Assert.Contains(AuthorizationStepNames.InternalStep, withRole);
        Assert.DoesNotContain(AuthorizationStepNames.InternalStep, withoutRole);
        Assert.Contains(AuthorizationStepNames.OpenStep, withoutRole);
    }

    // A client-credentials token has no user name; it is a caller all the same (#515), with its app roles.
    [Fact]
    public async Task AnAppOnlyToken_IsACallerWithItsAppRoles()
    {
        using var response = await SendAsync(HttpMethod.Post, InternalStepUrl, StepBody, TestTokenIssuer.App("intake", ["internal"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string[]> StepNamesAsync(string token)
    {
        using var response = await SendAsync(HttpMethod.Get, RegistryUrl, body: null, token);
        response.EnsureSuccessStatusCode();
        var registry = await response.Content.ReadAsync<KaleidoRegistry>();

        return [.. Assert.Single(registry!.Processes).Steps!.Select(step => step.Name)];
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, string? token)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options);
        }

        if (token is not null)
        {
            request.Headers.Authorization = TestJwtAuthentication.Bearer(token);
        }

        return await fixture.Client.SendAsync(request, Cancel);
    }

    private static object ExecuteBody(params string[] stepNames) =>
        new { steps = stepNames.Select(name => new { stepName = name, request = new { } }).ToArray() };
}
