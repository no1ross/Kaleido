using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Registry;
using Kaleido.Observability;

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

    private const string SecuredContextQueryUrl =
        "/kaleido/queryable/securedrecordcontextsource/query";

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
                "SecuredRecordContextSource",
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

        var context = Assert.Single(registry!.Queryables, r => r.Name == "SecuredRecordContextSource");
        Assert.Null(context.QueryUrl);
        Assert.Contains(context.Views, view => view.Name == "SecuredAdminView");
        Assert.Contains(context.Views, view => view.Name == "SecuredInternalView");
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
            r => r.Name.Equals("SecuredRecordContextSource", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(secured.Views, v => v.Name == "SecuredInternalView");
        Assert.DoesNotContain(secured.Views, v => v.Name == "SecuredAdminView");
    }

    // -- no case-level guardrails (ADR 0010): only the step's roles/policies --

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
    public async Task ProcessState_ForAnotherUserWithNoneOfTheCreatorsRoles_Returns200()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                ProcessStateUrl(processId),
                "bob",
                "viewer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task Execute_WhenAnotherUserResumes_AndTheStepAllowsThem_Succeeds()
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Kaleido only tags (#193): who created and last changed the process, and who started and last
    // ran each step - exactly as the principal provides it.
    [Fact]
    public async Task ProcessState_TagsWhoCreatedAndUpdatedTheProcess_AndWhoRanEachStep()
    {
        var create = AuthorizationAspNetCoreFixture.AuthenticatedJson(InternalStepExecuteUrl, StepBody, "alice", "internal");
        create.Headers.Add(TestAuthHandler.SubjectHeader, "sub-alice");
        var created = await fixture.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var processId = Guid.Parse(created.Headers.GetValues("X-Kaleido-Process-Id").Single());

        var resume = AuthorizationAspNetCoreFixture.AuthenticatedJson(OpenStepExecuteUrl, StepBody, "bob", "viewer");
        resume.Headers.Add("X-Kaleido-Process-Id", processId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(resume)).StatusCode);

        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, ProcessStateUrl(processId), "carol"));
        var state = await response.Content.ReadFromJsonAsync<Kaleido.Http.Processor.ProcessStateResponse>(KaleidoJsonOptions.Options);

        var alice = new KaleidoCaller { Name = "alice", Id = "sub-alice" };
        var bob = new KaleidoCaller { Name = "bob" };
        Assert.Equal(alice, state!.CreatedBy);
        Assert.Equal(bob, state.UpdatedBy);

        var internalStep = state.Steps.Single(x => x.StepName == AuthorizationStepNames.InternalStep);
        Assert.Equal(alice, internalStep.StartedBy);
        Assert.Equal(alice, internalStep.LastUpdatedBy);
        Assert.NotNull(internalStep.FirstExecuted);

        var openStep = state.Steps.Single(x => x.StepName == AuthorizationStepNames.OpenStep);
        Assert.Equal(bob, openStep.StartedBy);
        Assert.Equal(bob, openStep.LastUpdatedBy);

        var neverRan = state.Steps.Single(x => x.StepName == AuthorizationStepNames.PolicyStep);
        Assert.Null(neverRan.StartedBy);
        Assert.Null(neverRan.FirstExecuted);
    }

    [Fact]
    public async Task Execute_WhenAnotherUserResumes_ButTheStepRefusesThem_Returns403()
    {
        var processId = await CreateProcessAsync("alice", "internal");

        var request =
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                InternalStepExecuteUrl,
                StepBody,
                "bob",
                "viewer");
        request.Headers.Add("X-Kaleido-Process-Id", processId.ToString());

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
