using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Kaleido.Http.FunctionalTests.Authorization;

namespace Kaleido.Http.FunctionalTests.Visibility;

// #524: under ZeroTrust a caller is never described a step or a query they may not use - not in the
// registry's relationships or lookups, not in a process's next steps. A clerk and an admin share a
// process; what each one is told differs only by what each one may do.
public sealed class CallerVisibilityTests : IAsyncLifetime
{
    private const string ServiceName = "kaleido";

    private IHost? _host;
    private HttpClient _client = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization();
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = ServiceName,
                        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
                        Assemblies = [typeof(CallerVisibilityTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(CallerVisibilityTests).Namespace,
                    })
                        .AddInMemoryStores()
                        .AddHttp();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }
    }

    [Fact]
    public async Task TheRegistry_NamesNoStepOrQueryTheCallerMayNotUse()
    {
        var clerk = await RegistryAsync("vis-clerk");
        var admin = await RegistryAsync("vis-admin");

        // The clerk sees their step, without the admin step it closes on and without the admin-only
        // lookup (the input stays, with the notice); nothing names what they may not use.
        var clerkStep = Step(clerk, nameof(VisClerkStep));
        Assert.Empty(clerkStep.GetProperty("availableUntil").EnumerateArray());
        Assert.Equal([nameof(VisStartStep)], clerkStep.GetProperty("dependencies").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
        var field = clerkStep.GetProperty("fields").EnumerateArray().Single(x => x.GetProperty("name").GetString() == nameof(VisClerkStep.PatientId));
        Assert.False(field.TryGetProperty("lookup", out var lookup) && lookup.ValueKind != JsonValueKind.Null);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, field.GetProperty("notice").GetProperty("code").GetString());
        Assert.DoesNotContain(nameof(VisAdminStep), clerk.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(VisPatientSource), clerk.GetRawText(), StringComparison.Ordinal);

        // The admin, who may use both, is described both.
        Assert.Equal([nameof(VisAdminStep)], Step(admin, nameof(VisClerkStep)).GetProperty("availableUntil").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
        Assert.Equal(nameof(VisPatientSource), Step(admin, nameof(VisClerkStep)).GetProperty("fields")[0].GetProperty("lookup").GetProperty("source").GetString());
    }

    [Fact]
    public async Task AProcessResult_ListsOnlyTheNextStepsTheCallerMayRun_AndSaysWhenTheyMayNotGoOn()
    {
        var (status, started) = await PostAsync(nameof(VisStartStep), "vis-clerk", processId: null);
        Assert.Equal(HttpStatusCode.OK, status);
        var processId = started.GetProperty("processId").GetGuid();

        // Started: the admin step is available but not to the clerk; they still have steps to run.
        Assert.Equal(
            [nameof(VisClerkStep), nameof(VisEscalateStep)],
            started.GetProperty("availableSteps").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order());
        Assert.False(started.TryGetProperty("notice", out var none) && none.ValueKind != JsonValueKind.Null);

        // Escalated: the process now requires the admin step - the clerk is told only that they may not go on.
        var (_, escalated) = await PostAsync(nameof(VisEscalateStep), "vis-clerk", processId);
        var state = await StateAsync(processId, "vis-clerk");

        foreach (var answer in new[] { escalated, state })
        {
            Assert.Equal(JsonValueKind.Null, answer.GetProperty("requiredStep").ValueKind);
            Assert.Empty(answer.GetProperty("availableSteps").EnumerateArray());
            Assert.Equal(ProcessorErrorCodes.NotPermitted, answer.GetProperty("notice").GetProperty("code").GetString());
        }

        Assert.DoesNotContain(nameof(VisAdminStep), escalated.GetRawText(), StringComparison.Ordinal);

        // The admin, who may run it, is told to.
        Assert.Equal(nameof(VisAdminStep), (await StateAsync(processId, "vis-admin")).GetProperty("requiredStep").GetProperty("name").GetString());
    }

    private static JsonElement Step(JsonElement registry, string name) =>
        registry.GetProperty("processes").EnumerateArray()
            .SelectMany(x => x.GetProperty("steps").EnumerateArray())
            .Single(x => x.GetProperty("name").GetString() == name);

    private async Task<JsonElement> RegistryAsync(string role)
    {
        using var request = AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, $"/{ServiceName}/registry", role, role);
        using var response = await _client.SendAsync(request, Cancel);
        return Parse(await response.Content.ReadAsStringAsync(Cancel));
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostAsync(string step, string role, Guid? processId)
    {
        using var request = AuthorizationAspNetCoreFixture.AuthenticatedJson($"/{ServiceName}/processes/steps/{step.ToLowerInvariant()}", new { processStep = new { } }, role, role);

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        using var response = await _client.SendAsync(request, Cancel);
        return (response.StatusCode, Parse(await response.Content.ReadAsStringAsync(Cancel)));
    }

    private async Task<JsonElement> StateAsync(Guid processId, string role)
    {
        using var request = AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, $"/{ServiceName}/processes/{processId}", role, role);
        using var response = await _client.SendAsync(request, Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.True(response.IsSuccessStatusCode, $"State read failed ({(int)response.StatusCode}): {body}");
        return Parse(body);
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

[ProcessStep(DisplayName = "Start", Description = "Opens the case.")]
[KaleidoAuthorization(Roles = "vis-clerk,vis-admin")]
public sealed record VisStartStep : IProcessStep;

[ProcessStep(DisplayName = "Clerk work", Description = "A clerk (or an admin) records the patient; closes once an admin acts.")]
[KaleidoAuthorization(Roles = "vis-clerk,vis-admin")]
[DependsOn<VisStartStep>]
[AvailableUntil<VisAdminStep>]
public sealed record VisClerkStep : IProcessStep
{
    [Lookup(Service = "kaleido", Source = nameof(VisPatientSource), Field = nameof(VisPatient.Id))]
    public Guid PatientId { get; init; }
}

[ProcessStep(DisplayName = "Escalate", Description = "A clerk escalates the case to an admin.")]
[KaleidoAuthorization(Roles = "vis-clerk")]
[DependsOn<VisStartStep>]
public sealed record VisEscalateStep : IProcessStep;

[ProcessStep(DisplayName = "Admin work", Description = "An admin decides.")]
[KaleidoAuthorization(Roles = "vis-admin")]
[DependsOn<VisStartStep>]
public sealed record VisAdminStep : IProcessStep;

public sealed class VisPatient : IQueryContext
{
    [Filterable(FilterOperator.Equals)]
    public Guid Id { get; init; }
}

[QuerySource(DisplayName = "Patients", Description = "Patients, for admins only.")]
[KaleidoAuthorization(Roles = "vis-admin")]
public sealed class VisPatientSource : IQuerySource<VisPatient>
{
    public IQueryable<VisPatient> CreateQuery(QueryExecutionContext executionContext) => Array.Empty<VisPatient>().AsQueryable();
}

public sealed class VisStartHandler : IProcessStepHandler<VisStartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(VisStartStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class VisClerkHandler : IProcessStepHandler<VisClerkStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(VisClerkStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class VisEscalateHandler : IProcessStepHandler<VisEscalateStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(VisEscalateStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success<VisAdminStep>());
}

public sealed class VisAdminHandler : IProcessStepHandler<VisAdminStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(VisAdminStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
