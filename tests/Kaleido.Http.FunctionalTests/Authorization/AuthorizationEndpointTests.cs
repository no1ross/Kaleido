using System.Net;
using Kaleido.Http.Process;
using Kaleido.Http.Queryable;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;

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

    private const string StepCatalogUrl =
        "/kaleido/processes/steps";

    private const string QueryableCatalogUrl =
        "/kaleido/queryable";

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
    public async Task StepCatalog_WhenUnauthenticated_ReturnsEmpty()
    {
        var response = await fixture.Client.GetAsync(StepCatalogUrl);

        var steps =
            await response.Content.ReadAsync<ProcessStepSummary[]>();

        Assert.Empty(steps!);
    }

    [Fact]
    public async Task StepCatalog_WhenViewerRole_ExcludesInternalStep()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                StepCatalogUrl,
                roles: ["viewer"]));

        var steps =
            await response.Content.ReadAsync<ProcessStepSummary[]>();

        Assert.DoesNotContain(
            steps!,
            s => s.Name.Equals(
                AuthorizationStepNames.InternalStep,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StepCatalog_WhenInternalRole_IncludesInternalStep()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                StepCatalogUrl,
                roles: ["internal"]));

        var steps =
            await response.Content.ReadAsync<ProcessStepSummary[]>();

        Assert.Contains(
            steps!,
            s => s.Name.Equals(
                AuthorizationStepNames.InternalStep,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryableCatalog_WhenViewerRole_ExcludesSecuredContext()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(
                HttpMethod.Get,
                QueryableCatalogUrl,
                roles: ["viewer"]));

        var records =
            await response.Content.ReadAsync<QueryableRecordSummary[]>();

        Assert.DoesNotContain(
            records!,
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
}
