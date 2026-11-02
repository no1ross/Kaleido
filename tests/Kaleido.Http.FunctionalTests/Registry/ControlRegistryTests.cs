using System.Net;
using Kaleido.Http.FunctionalTests.Authorization;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Kaleido.Http.Registry;
using Kaleido.Registry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.FunctionalTests.Registry;

// #553: the control registry as a host - instances register, heartbeat and deregister over HTTP, each
// checked against who may register the service; only Kaleido hosts (the reader policy) read it, the
// capabilities and the statuses apart; leases, the rolling-deployment rule and completeness on the
// registry's clock.
public sealed class ControlRegistryTests : IAsyncLifetime
{
    private const string RadiologyClient = "radiology-client";
    private const string MemberClient = "member-client";
    private const string ReaderRole = "kaleido-reader";
    private static readonly AuthorizationMetadata RadiologyOnly = new(null, ["radiology"]);
    private static readonly TimeSpan PastTheSnapshot = TimeSpan.FromSeconds(10) + TimeSpan.FromTicks(1);

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private IHost host = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => host = await StartAsync([]);

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync(CancellationToken.None);
        host.Dispose();
    }

    private async Task<IHost> StartAsync(Dictionary<string, string?> extra)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Kaleido:Registry:Registrants:radiology:0"] = RadiologyClient,
            ["Kaleido:Registry:Registrants:member:0"] = MemberClient,
            ["Kaleido:Registry:ReaderPolicy"] = "registry-reader",
        };

        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(time);
                    services.AddRouting();
                    services.AddAuthentication(TestAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                    services.AddAuthorization(o => o.AddPolicy("registry-reader", p => p.RequireRole(ReaderRole)));
                    services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new()
                    {
                        ServiceName = "registry",
                        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
                        Assemblies = [typeof(ControlRegistryTests).Assembly],
                        TypeFilter = _ => false,
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistry()
                        .AddInMemoryRegistryStore();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoControlRegistry());
                });
            })
            .StartAsync(Cancel);
    }

    private static RegistryInstanceRegistration Radiology(string version = "2026-11", params string[] steps) =>
        new()
        {
            ServiceName = "radiology",
            ServiceVersion = version,
            InstanceId = Guid.NewGuid(),
            Address = "https://radiology",
            Processes =
            [
                new ProcessorRegistryResponse
                {
                    Name = "radiology",
                    ServiceName = "radiology",
                    Steps = [.. (steps.Length == 0 ? ["OrderScanStep"] : steps).Select(x => new ProcessStepResponse { Name = x, Authorization = RadiologyOnly })],
                },
            ],
        };

    private static RegistryInstanceRegistration Member() =>
        new()
        {
            ServiceName = "member",
            ServiceVersion = "1",
            InstanceId = Guid.NewGuid(),
            Queryables = [new QueryableSourceResponse { ServiceName = "member", Name = "MemberSource", QueryUrl = "/member/queryable/membersource/query", Authorization = new AuthorizationMetadata(null, ["clerk"]) }],
        };

    private HttpRequestMessage Request(HttpMethod method, string url, string? subject = null, string? roles = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (subject is not null || roles is not null)
        {
            request.Headers.Add(TestAuthHandler.UserHeader, subject ?? "someone");
            request.Headers.Add(TestAuthHandler.SubjectHeader, subject ?? "someone");
            request.Headers.Add(TestAuthHandler.RolesHeader, roles ?? string.Empty);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            return await host.GetTestClient().SendAsync(request, Cancel);
        }
    }

    private Task<HttpResponseMessage> RegisterAsync(RegistryInstanceRegistration registration, string? subject) =>
        SendAsync(Request(HttpMethod.Post, "/registry/instances", subject, body: registration));

    private async Task<AggregatedRegistryResponse> ReadAsync()
    {
        using var response = await SendAsync(Request(HttpMethod.Get, "/registry", "reader", ReaderRole));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options, Cancel))!;
    }

    private async Task<IReadOnlyCollection<RegistryServiceStatus>> StatusAsync()
    {
        using var response = await SendAsync(Request(HttpMethod.Get, "/registry/status", "reader", ReaderRole));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RegistryStatusResponse>(KaleidoJsonOptions.Options, Cancel))!.Services;
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel))!.Errors.Single().Code;

    [Fact]
    public async Task AnInstance_Registers_AndAReaderSeesItsVersionWithItsStatus()
    {
        var registration = Radiology();

        using var response = await RegisterAsync(registration, RadiologyClient);
        var lease = await response.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, Cancel);
        var registry = await ReadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(30), lease!.LeaseUntilUtc);
        var processor = Assert.Single(registry.Processes);
        Assert.Equal(("radiology", "2026-11"), (processor.ServiceName, processor.ServiceVersion));
        Assert.Equal(new RegistryServiceStatus { ServiceName = "radiology", ServiceVersion = "2026-11", IsCurrent = true, Address = "https://radiology", ReadyInstances = 1, Instances = 1, Available = true }, Assert.Single(await StatusAsync()));
    }

    // A service may register only as itself (the registrants map), and only with a version.
    [Fact]
    public async Task ARegistration_FromAnotherServicesCaller_Anonymously_OrWithoutAVersion_IsRefused()
    {
        using var otherCaller = await RegisterAsync(Radiology(), MemberClient);
        using var anonymous = await RegisterAsync(Radiology(), subject: null);
        using var unversioned = await RegisterAsync(Radiology() with { ServiceVersion = string.Empty }, RadiologyClient);

        Assert.Equal((HttpStatusCode.Forbidden, RegistryErrorCodes.RegistrantNotAllowed), (otherCaller.StatusCode, await CodeAsync(otherCaller)));
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
        Assert.Equal((HttpStatusCode.BadRequest, RegistryErrorCodes.InvalidRegistration), (unversioned.StatusCode, await CodeAsync(unversioned)));
        Assert.Empty(await StatusAsync());
    }

    // Heartbeats keep an instance ready; one that stops is not ready after its lease, and stays listed.
    [Fact]
    public async Task Heartbeats_KeepAnInstanceReady_AndOneThatStops_IsUnavailableAfterItsLease()
    {
        var registration = Radiology();
        (await RegisterAsync(registration, RadiologyClient)).Dispose();

        time.Advance(TimeSpan.FromSeconds(25));
        using (var renewed = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{registration.InstanceId}/heartbeat", RadiologyClient, body: new RegistryHeartbeat())))
        {
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        }

        time.Advance(TimeSpan.FromSeconds(25));
        Assert.True(Assert.Single(await StatusAsync()).Available);

        time.Advance(TimeSpan.FromSeconds(10));
        var status = Assert.Single(await StatusAsync());
        Assert.Equal((false, 0, 1), (status.Available, status.ReadyInstances, status.Instances));
        Assert.Single((await ReadAsync()).Processes);
    }

    [Fact]
    public async Task AHeartbeatOrDeregistration_OfAnUnknownInstance_AsksItToRegisterAgain()
    {
        using var heartbeat = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{Guid.NewGuid()}/heartbeat", RadiologyClient, body: new RegistryHeartbeat()));
        using var removal = await SendAsync(Request(HttpMethod.Delete, $"/registry/instances/{Guid.NewGuid()}", RadiologyClient));

        Assert.Equal((HttpStatusCode.NotFound, RegistryErrorCodes.InstanceUnknown), (heartbeat.StatusCode, await CodeAsync(heartbeat)));
        Assert.Equal(HttpStatusCode.NotFound, removal.StatusCode);
    }

    [Fact]
    public async Task ADeregisteredInstance_IsGone_AtOnce()
    {
        var registration = Radiology();
        (await RegisterAsync(registration, RadiologyClient)).Dispose();

        using var removal = await SendAsync(Request(HttpMethod.Delete, $"/registry/instances/{registration.InstanceId}", RadiologyClient));
        time.Advance(PastTheSnapshot);

        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        Assert.Equal(0, Assert.Single(await StatusAsync()).Instances);
    }

    // Only Kaleido hosts read the registry, and they get all of it, unfiltered - capabilities a reader's
    // own roles would not allow included; they filter for their callers. Anyone else is refused.
    [Fact]
    public async Task AReader_GetsTheWholeRegistry_AndNoOneElseReadsIt()
    {
        (await RegisterAsync(Radiology(), RadiologyClient)).Dispose();
        (await RegisterAsync(Member(), MemberClient)).Dispose();

        var registry = await ReadAsync();
        using var clerk = await SendAsync(Request(HttpMethod.Get, "/registry", "clerk", "clerk"));
        using var clerkStatus = await SendAsync(Request(HttpMethod.Get, "/registry/status", "clerk", "clerk"));
        using var anonymous = await SendAsync(Request(HttpMethod.Get, "/registry"));

        Assert.Single(registry.Processes);
        Assert.Single(registry.Queryables);
        Assert.Equal(2, (await StatusAsync()).Count);
        Assert.Equal((HttpStatusCode.Forbidden, RegistryErrorCodes.ReaderNotAllowed), (clerk.StatusCode, await CodeAsync(clerk)));
        Assert.Equal(HttpStatusCode.Forbidden, clerkStatus.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
    }

    // The statuses have their own ETag: unchanged statuses answer 304.
    [Fact]
    public async Task TheStatus_AnswersNotModified_WhileNothingChanged()
    {
        (await RegisterAsync(Radiology(), RadiologyClient)).Dispose();
        using var first = await SendAsync(Request(HttpMethod.Get, "/registry/status", "reader", ReaderRole));

        var check = Request(HttpMethod.Get, "/registry/status", "reader", ReaderRole);
        check.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var unchanged = await SendAsync(check);

        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
    }

    // The first version registered is current; a second one is listed, not current.
    [Fact]
    public async Task TheFirstVersion_IsCurrent_AndASecondOneIsListedNextToIt()
    {
        (await RegisterAsync(Radiology("1"), RadiologyClient)).Dispose();
        (await RegisterAsync(Radiology("2"), RadiologyClient)).Dispose();

        var registry = await ReadAsync();

        Assert.Equal([("1", true), ("2", false)], (await StatusAsync()).Select(x => (x.ServiceVersion, x.IsCurrent)));
        Assert.Equal(2, registry.Processes.Count);
    }

    // During a rolling deployment, a step only the new build has waits until every ready instance has it.
    [Fact]
    public async Task DuringARollingDeployment_AStepOnlyTheNewBuildHas_IsWithheld()
    {
        (await RegisterAsync(Radiology("1", "OrderScanStep"), RadiologyClient)).Dispose();
        (await RegisterAsync(Radiology("1", "OrderScanStep", "ReviewStep"), RadiologyClient)).Dispose();

        var registry = await ReadAsync();

        Assert.Equal(["OrderScanStep"], Assert.Single(registry.Processes).Steps!.Select(x => x.Name));
        Assert.Equal(2, Assert.Single(await StatusAsync()).ReadyInstances);
    }

    // An expected processor version that never registered makes the registry partial; ?strict says so.
    [Fact]
    public async Task AnExpectedVersionNotRegistered_MakesTheRegistryPartial()
    {
        await host.StopAsync(Cancel);
        host.Dispose();
        host = await StartAsync(new() { ["Kaleido:Registry:Expected:0:ServiceName"] = "radiology", ["Kaleido:Registry:Expected:0:Versions:0"] = "2026-11" });

        var registry = await ReadAsync();
        using var strict = await SendAsync(Request(HttpMethod.Get, "/registry?strict", "reader", ReaderRole));

        Assert.True(registry.IsPartial);
        Assert.Equal(new RegistryMissingService { ServiceName = "radiology", ServiceVersion = "2026-11", Reason = "expected" }, Assert.Single(registry.Missing));
        Assert.Equal(HttpStatusCode.BadGateway, strict.StatusCode);
    }
}
