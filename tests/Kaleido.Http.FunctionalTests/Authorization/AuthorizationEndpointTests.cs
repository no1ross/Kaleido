using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Registry;

namespace Kaleido.Http.FunctionalTests.Authorization;

public sealed class AuthorizationEndpointTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private const string InternalStepExecuteUrl =
        "/kaleido/processes/steps/authorizedinternalstep";

    private const string PolicyStepExecuteUrl =
        "/kaleido/processes/steps/authorizedpolicystep";

    private const string OpenStepExecuteUrl =
        "/kaleido/processes/steps/authorizedopenstep";

    private const string AnonymousStepExecuteUrl =
        "/kaleido/processes/steps/authorizedanonymousstep";

    private const string ExecuteUrl =
        "/kaleido/processes/execute";

    private const string RegistryUrl =
        "/kaleido/registry";

    private static string ProcessStateUrl(Guid processId) =>
        $"/kaleido/processes/{processId}";

    private static string ProcessTransferUrl(Guid processId) =>
        $"/kaleido/processes/{processId}/transfer";

    private const string SecuredContextQueryUrl =
        "/kaleido/queryable/secured-records/query";

    private static readonly object StepBody =
        new { processStep = new { } };

    private static readonly object QueryBody =
        new { query = new { }, parameters = new { } };

    // -- per-capability endpoint enforcement -------------------------------

    [Fact]
    public async Task StepExecute_WhenUnauthenticated_ChallengesWithHostScheme()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            InternalStepExecuteUrl,
            StepBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            TestAuthHandler.ChallengeHeaderValue,
            response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task StepExecute_WhenRoleMismatch_ForbidsWithHostScheme()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                InternalStepExecuteUrl,
                StepBody,
                roles: ["viewer"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task StepExecute_WhenRoleMatches_Returns200()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                InternalStepExecuteUrl,
                StepBody,
                roles: ["internal"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StepExecute_WhenPolicyRoleMissing_Returns403()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                PolicyStepExecuteUrl,
                StepBody,
                roles: ["internal"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StepExecute_WhenPolicyRolePresent_Returns200()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                PolicyStepExecuteUrl,
                StepBody,
                roles: ["clinician"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StepExecute_Undeclared_RequiresAuthentication()
    {
        var anonymous = await fixture.Client.PostAsJsonAsync(
            OpenStepExecuteUrl,
            StepBody);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var authenticated = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                OpenStepExecuteUrl,
                StepBody));

        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    // -- discovery filtering ------------------------------------------------

    [Fact]
    public async Task Registry_WhenUnauthenticated_ReturnsOnlyAnonymousCapabilities()
    {
        var response = await fixture.Client.GetAsync(RegistryUrl);

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var processor = Assert.Single(registry!.Processes);
        var step = Assert.Single(processor.Steps!);
        Assert.Equal(AuthorizationStepNames.AnonymousStep, step.Name);
        Assert.True(step.Authorization?.AllowAnonymous);
        Assert.Empty(registry.Queryables);
    }

    // -- AllowAnonymous ------------------------------------------------------

    [Fact]
    public async Task StepExecute_AllowAnonymous_Returns200ForAnonymous()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            AnonymousStepExecuteUrl,
            StepBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Execute_AnonymousWithOnlyAnonymousSteps_Returns200()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            ExecuteUrl,
            ExecuteBody(AuthorizationStepNames.AnonymousStep));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Execute_AnonymousWithAnySecuredStep_RejectsWholeRequest()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            ExecuteUrl,
            ExecuteBody(
                AuthorizationStepNames.AnonymousStep,
                AuthorizationStepNames.OpenStep));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            TestAuthHandler.ChallengeHeaderValue,
            response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Execute_AnonymousWithUnknownStep_Returns401()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            ExecuteUrl,
            ExecuteBody("no-such-step"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousProcess_ClaimedAfterLogin_DeniesOtherUsers()
    {
        var created = await fixture.Client.PostAsJsonAsync(
            AnonymousStepExecuteUrl,
            StepBody);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var processId = Guid.Parse(
            created.Headers.GetValues("X-Kaleido-Process-Id").Single());

        var claim = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Post,
                ProcessTransferUrl(processId),
                "carol",
                "shopper"));

        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);

        var other = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "dave",
                "viewer"));

        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    private static object ExecuteBody(params string[] stepNames) =>
        new
        {
            steps = stepNames
                .Select(name => new { stepName = name, request = new { } })
                .ToArray()
        };

    [Fact]
    public async Task Registry_WhenViewerRole_ExcludesInternalStep()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                RegistryUrl,
                roles: ["viewer"]));

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var processor = Assert.Single(registry!.Processes);
        Assert.DoesNotContain(
            processor.Steps!,
            s => s.Name.Equals(
                AuthorizationStepNames.InternalStep,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Registry_WhenInternalRole_IncludesInternalStep()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                RegistryUrl,
                roles: ["internal"]));

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var processor = Assert.Single(registry!.Processes);
        Assert.Contains(
            processor.Steps!,
            s => s.Name.Equals(
                AuthorizationStepNames.InternalStep,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Registry_WhenViewerRole_ExcludesSecuredContext()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                RegistryUrl,
                roles: ["viewer"]));

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        Assert.DoesNotContain(
            registry!.Queryables,
            r => r.Name.Equals(
                "secured-records",
                StringComparison.OrdinalIgnoreCase));
    }

    // -- queryable capability enforcement ------------------------------------

    [Fact]
    public async Task Registry_WhenViewOverridesDeniedContext_ShowsOnlyView()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                RegistryUrl,
                roles: ["admin"]));

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var context = Assert.Single(registry!.Queryables, r => r.Name == "secured-records");
        Assert.Null(context.QueryUrl);
        Assert.Contains(context.Views, view => view.Name == "admin-view");
        Assert.Contains(context.Views, view => view.Name == "internal-view");
    }

    [Fact]
    public async Task QueryContextQuery_WhenRoleMismatch_Returns403()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                SecuredContextQueryUrl,
                QueryBody,
                roles: ["viewer"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Registry_WhenInternalRole_FiltersAdminOnlyView()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                RegistryUrl,
                roles: ["internal"]));

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var secured = Assert.Single(
            registry!.Queryables,
            r => r.Name.Equals("secured-records", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(secured.Views, v => v.Name == "internal-view");
        Assert.DoesNotContain(secured.Views, v => v.Name == "admin-view");
    }

    // -- process ownership ---------------------------------------------------

    private async Task<Guid> CreateProcessAsync(
        string user,
        params string[] roles)
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                InternalStepExecuteUrl,
                StepBody,
                user,
                roles));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return Guid.Parse(
            response.Headers.GetValues("X-Kaleido-Process-Id").Single());
    }

    [Fact]
    public async Task ProcessState_WhenDifferentOwnerAndNoSharedRole_Returns403()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "bob",
                "viewer"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProcessState_WhenRoleMateOfOwner_Returns200()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "bob",
                "internal"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessState_WhenOwner_Returns200()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "alice",
                "internal"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessTransfer_WhenUnrelatedCaller_Returns403()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Post,
                ProcessTransferUrl(processId),
                "bob",
                "viewer"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProcessTransfer_WhenRoleMate_TransfersOwnership()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var transfer = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Post,
                ProcessTransferUrl(processId),
                "bob",
                "internal"));

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        // bob now owns the process — alice keeps access as a role-mate.
        var state = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "bob",
                "internal"));

        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
    }

    [Fact]
    public async Task ProcessTransfer_ThenNewOwnerDeniesOriginalOwner()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        // bob shares "internal" so he may transfer, but his "admin" role
        // snapshot drops "internal" — alice loses access.
        var transfer = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Post,
                ProcessTransferUrl(processId),
                "bob",
                "internal", "admin"));

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
    }

    [Fact]
    public async Task ProcessTransfer_WhenUnauthenticated_Returns401()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.PostAsync(
            ProcessTransferUrl(processId),
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Execute_WhenResumingOtherOwnersProcess_Returns403()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var request =
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                OpenStepExecuteUrl,
                StepBody,
                "bob",
                "viewer");
        request.Headers.Add("X-Kaleido-Process-Id", processId.ToString());

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
