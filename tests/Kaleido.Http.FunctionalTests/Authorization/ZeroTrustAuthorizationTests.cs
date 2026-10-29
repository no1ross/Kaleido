using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Registry;

namespace Kaleido.Http.FunctionalTests.Authorization;

// #503: ZeroTrust on a real host. A capability without an explicit rule (AuthorizedOpenStep) exists
// in the code but is exposed nowhere: not in the registry, not mapped, denied in generic execute -
// even for an authenticated caller with every role. Declared capabilities work as in Authenticated.
// Catches ZeroTrust treated as Authenticated (an undeclared capability published or run).
public sealed class ZeroTrustAuthorizationTests(
    ZeroTrustAuthorizationFixture fixture)
    : IClassFixture<ZeroTrustAuthorizationFixture>
{
    private static readonly string[] EveryRole = ["internal", "clinician", "admin"];

    private static readonly object StepBody = new { processStep = new { } };

    private static object ExecuteBody(string stepName) =>
        new { steps = new[] { new { stepName, request = new { } } } };

    [Fact]
    public async Task TheRegistry_OmitsACapabilityWithoutARule_AndKeepsTheDeclaredOnes()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, "/kaleido/registry", roles: EveryRole));

        var registry = await response.Content.ReadAsync<AggregatedRegistryResponse>();

        var steps = Assert.Single(registry!.Processes).Steps!.Select(x => x.Name).Order(StringComparer.Ordinal);
        Assert.Equal(
            [AuthorizationStepNames.AnonymousStep, AuthorizationStepNames.InternalStep, AuthorizationStepNames.PolicyStep],
            steps);
    }

    [Fact]
    public async Task ACapabilityWithoutARule_IsNotMapped()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson("/kaleido/processes/steps/authorizedopenstep", StepBody, roles: EveryRole));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ACapabilityWithoutARule_IsDeniedInGenericExecute()
    {
        var response = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson("/kaleido/processes/execute", ExecuteBody(AuthorizationStepNames.OpenStep), roles: EveryRole));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ADeclaredCapability_RunsForTheDeclaredRole_AndOnlyForIt()
    {
        var allowed = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson("/kaleido/processes/steps/authorizedinternalstep", StepBody, roles: ["internal"]));
        var refused = await fixture.Client.SendAsync(
            AuthorizationAspNetCoreFixture.AuthenticatedJson("/kaleido/processes/steps/authorizedinternalstep", StepBody, roles: ["viewer"]));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
