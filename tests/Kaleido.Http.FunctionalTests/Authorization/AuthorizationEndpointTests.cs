using System.Net;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry;
using Kaleido.Processor.AspNetCore.FunctionalTests.Infrastructure;

namespace Kaleido.AspNetCore.FunctionalTests.Authorization;

public sealed class AuthorizationEndpointTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private const string InternalStepExecuteUrl =
        "/kaleido/processes/steps/auth-internal";

    private const string InternalStepMetadataUrl =
        "/kaleido/processes/steps/auth-internal/metadata";

    private const string PolicyStepExecuteUrl =
        "/kaleido/processes/steps/auth-policy";

    private const string OpenStepExecuteUrl =
        "/kaleido/processes/steps/auth-open";

    private const string RegistryUrl =
        "/kaleido/registry";

    private static string ProcessStateUrl(Guid processId) =>
        $"/kaleido/processes/{processId}";

    private static string ProcessTransferUrl(Guid processId) =>
        $"/kaleido/processes/{processId}/transfer";

    private const string SecuredContextMetadataUrl =
        "/kaleido/queryable/secured-records/metadata";

    private const string SecuredContextQueryUrl =
        "/kaleido/queryable/secured-records/query";

    private static readonly object StepBody =
        new { processStep = new { } };

    private static readonly object QueryBody =
        new { query = new { }, parameters = new { } };

    // -- per-capability endpoint enforcement -------------------------------

    [Fact]
    public async Task StepExecute_WhenUnauthenticated_Returns401WithKaleidoError()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            InternalStepExecuteUrl,
            StepBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body =
            await response.Content.ReadAsync<KaleidoErrorResponse>();

        Assert.Equal(KaleidoErrorCodes.Unauthorized, Assert.Single(body!.Errors).Code);
    }

    [Fact]
    public async Task StepExecute_WhenRoleMismatch_Returns403WithKaleidoError()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson(
                InternalStepExecuteUrl,
                StepBody,
                roles: ["viewer"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body =
            await response.Content.ReadAsync<KaleidoErrorResponse>();

        Assert.Equal(KaleidoErrorCodes.Forbidden, Assert.Single(body!.Errors).Code);
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

    [Fact]
    public async Task StepMetadata_WhenUnauthenticated_Returns401()
    {
        var response =
            await fixture.Client.GetAsync(InternalStepMetadataUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- discovery filtering ------------------------------------------------

    [Fact]
    public async Task Registry_WhenUnauthenticated_ReturnsNoCapabilities()
    {
        var response = await fixture.Client.GetAsync(RegistryUrl);

        var registry =
            await response.Content.ReadAsync<AggregatedRegistryResponse>();

        Assert.All(registry!.Processes, p => Assert.Empty(p.Steps!));
        Assert.Empty(registry.Queryables);
    }

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
    public async Task QueryContextMetadata_WhenUnauthenticated_Returns401()
    {
        var response =
            await fixture.Client.GetAsync(SecuredContextMetadataUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
    public async Task QueryContextMetadata_FiltersAdminOnlyView()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                SecuredContextMetadataUrl,
                roles: ["internal"]));

        var metadata =
            await response.Content.ReadAsync<QueryableRecordResponse>();

        Assert.Contains(
            metadata!.Views,
            v => v.Name == "internal-view");

        Assert.DoesNotContain(
            metadata.Views,
            v => v.Name == "admin-view");
    }

    [Fact]
    public async Task QueryContextMetadata_WhenContextRoleMissing_Returns403()
    {
        // "admin" lacks the context's "internal" role — the context
        // metadata endpoint itself is denied even though admin-view
        // would be visible to it.
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                SecuredContextMetadataUrl,
                roles: ["admin"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
