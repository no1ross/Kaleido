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
using Kaleido.Provider.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

    // What only service applications' app-only tokens carry, here: the host's RegistrantPolicy requires it.
    private const string ServiceRole = "kaleido-service";
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

    private async Task<IHost> StartAsync(Dictionary<string, string?> extra, string? database = null)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Kaleido:Registry:Registrants:radiology:0"] = RadiologyClient,
            ["Kaleido:Registry:Registrants:member:0"] = MemberClient,
            ["Kaleido:Registry:ReaderPolicy"] = "registry-reader",
            ["Kaleido:Registry:RegistrantPolicy"] = "service-application",
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
                    services.AddAuthorization(o =>
                    {
                        o.AddPolicy("registry-reader", p => p.RequireRole(ReaderRole));
                        o.AddPolicy("service-application", p => p.RequireRole(ServiceRole));
                    });
                    var kaleido = services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new()
                    {
                        ServiceName = "registry",
                        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
                        Assemblies = [typeof(ControlRegistryTests).Assembly],
                        TypeFilter = _ => false,
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistry();

                    if (database is null)
                    {
                        kaleido.AddInMemoryRegistryStore();
                    }
                    else
                    {
                        kaleido.AddSqliteRegistryStore(database);
                    }
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
            // The services' own clients call with their app-only credential.
            roles ??= subject is RadiologyClient or MemberClient ? ServiceRole : null;
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

    private Task<HttpResponseMessage> RegisterAsync(RegistryInstanceRegistration registration, string? subject, string? roles = null) =>
        SendAsync(Request(HttpMethod.Post, "/registry/instances", subject, roles, registration));

    private async Task<KaleidoRegistry> ReadAsync()
    {
        using var response = await SendAsync(Request(HttpMethod.Get, "/registry", "reader", ReaderRole));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel))!;
    }

    private async Task<IReadOnlyCollection<RegistryServiceStatus>> StatusAsync()
    {
        using var response = await SendAsync(Request(HttpMethod.Get, "/registry/status", "reader", ReaderRole));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, Cancel))!.Services;
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
        Assert.Equal(("radiology", "2026-11", RegistryCapabilityStatus.Available), (processor.ServiceName, processor.ServiceVersion, processor.Status));
        var status = Assert.Single(await StatusAsync());
        Assert.Equal(("radiology", "2026-11", true, "https://radiology", RegistryCapabilityStatus.Available, 1), (status.ServiceName, status.ServiceVersion, status.IsCurrent, status.Address, status.Status, status.ReadyInstances));
        Assert.Equal((registration.InstanceId, RegistryInstanceState.Ready), (Assert.Single(status.Instances).InstanceId, Assert.Single(status.Instances).State));
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
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single(await StatusAsync()).Status);

        time.Advance(TimeSpan.FromSeconds(10));
        var status = Assert.Single(await StatusAsync());
        Assert.Equal((RegistryCapabilityStatus.Unavailable, 0, RegistryInstanceState.NotReady), (status.Status, status.ReadyInstances, Assert.Single(status.Instances).State));
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single((await ReadAsync()).Processes).Status);
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
        Assert.Empty(Assert.Single(await StatusAsync()).Instances);
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

    // After every registry node was down, a new node serves the registry from the database at once:
    // what was registered is all there, each instance not heard from since is Unknown and the registry is
    // Degraded, until the instance's next heartbeat (it never needs to register again).
    [Fact]
    public async Task AfterEveryNodeWasDown_TheRegistryServesWhatItStored_UnknownUntilTheInstancesReport()
    {
        var file = Path.Combine(Path.GetTempPath(), $"kaleido-registry-{Guid.NewGuid():N}.db");
        var database = $"Data Source={file};Pooling=False";
        await using (var context = new SqliteKaleidoRegistryDbContext(new DbContextOptionsBuilder<SqliteKaleidoRegistryDbContext>().UseSqlite(database).Options))
        {
            await context.Database.EnsureCreatedAsync(Cancel);
        }

        try
        {
            var registration = Radiology();
            await host.StopAsync(Cancel);
            host.Dispose();
            host = await StartAsync([], database);
            (await RegisterAsync(registration, RadiologyClient)).Dispose();
            await host.StopAsync(Cancel);
            host.Dispose();

            time.Advance(TimeSpan.FromMinutes(5));
            host = await StartAsync([], database);
            await TouchWatchAsync();

            var registry = await ReadAsync();
            var status = Assert.Single(await StatusAsync());
            Assert.Equal(("radiology", RegistryCapabilityStatus.Unknown), (Assert.Single(registry.Processes).ServiceName, Assert.Single(registry.Processes).Status));
            Assert.Equal((RegistryCapabilityStatus.Unknown, 1, RegistryInstanceState.Unknown), (status.Status, status.UnknownInstances, Assert.Single(status.Instances).State));
            Assert.Equal(HealthStatus.Degraded, await CompletenessAsync());

            time.Advance(TimeSpan.FromSeconds(5));
            using (var heartbeat = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{registration.InstanceId}/heartbeat", RadiologyClient, body: new RegistryHeartbeat())))
            {
                Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
            }

            time.Advance(PastTheSnapshot);
            Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single((await ReadAsync()).Processes).Status);
            Assert.Equal(HealthStatus.Healthy, await CompletenessAsync());
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            SqliteConnection.ClearAllPools();
            File.Delete(file);
            host = await StartAsync([]);
        }
    }

    // The node's housekeeping touches the watch at startup, in the background; the test touches it at the
    // same moment, so the reads after it see the watch resumed (the same statement: idempotent).
    private async Task TouchWatchAsync()
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>().TouchWatchAsync(time.GetUtcNow(), TimeSpan.FromSeconds(30), Cancel);
    }

    private async Task<HealthStatus> CompletenessAsync() =>
        (await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(x => x.Name == "kaleido-registry-completeness", Cancel)).Status;

    // The capabilities alone, for hosts keeping their copy fresh: an instance failing changes the statuses,
    // not the capabilities, so their ETag still answers 304. Only readers get them.
    [Fact]
    public async Task TheCapabilities_AreUnchanged_WhenOnlyAStatusChanges()
    {
        var registration = Radiology();
        (await RegisterAsync(registration, RadiologyClient)).Dispose();
        using var first = await SendAsync(Request(HttpMethod.Get, "/registry/capabilities", "reader", ReaderRole));
        var capabilities = await first.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel);

        using (var failing = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{registration.InstanceId}/heartbeat", RadiologyClient, body: new RegistryHeartbeat { Healthy = false })))
        {
            Assert.Equal(HttpStatusCode.OK, failing.StatusCode);
        }

        time.Advance(PastTheSnapshot);
        var check = Request(HttpMethod.Get, "/registry/capabilities", "reader", ReaderRole);
        check.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var unchanged = await SendAsync(check);
        using var clerk = await SendAsync(Request(HttpMethod.Get, "/registry/capabilities", "clerk", "clerk"));

        Assert.Equal(RegistryCapabilityStatus.NotReported, Assert.Single(capabilities!.Processes).Status);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single((await ReadAsync()).Processes).Status);
        Assert.Equal(HttpStatusCode.Forbidden, clerk.StatusCode);
    }

    // A user signed in through a service's own client id is not that service: without the service
    // application's credential (RegistrantPolicy) it registers, renews and removes nothing.
    [Fact]
    public async Task AUserBehindAServicesClientId_IsRefused()
    {
        var registration = Radiology();
        (await RegisterAsync(registration, RadiologyClient)).Dispose();

        using var register = await SendAsync(Request(HttpMethod.Post, "/registry/instances", RadiologyClient, roles: "clerk", body: Radiology()));
        using var heartbeat = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{registration.InstanceId}/heartbeat", RadiologyClient, roles: "clerk", body: new RegistryHeartbeat()));
        using var removal = await SendAsync(Request(HttpMethod.Delete, $"/registry/instances/{registration.InstanceId}", RadiologyClient, roles: "clerk"));

        Assert.Equal((HttpStatusCode.Forbidden, RegistryErrorCodes.RegistrantNotAllowed), (register.StatusCode, await CodeAsync(register)));
        Assert.Equal(HttpStatusCode.Forbidden, heartbeat.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removal.StatusCode);
        Assert.Single(Assert.Single(await StatusAsync()).Instances);
    }

    // At 150+ shared services, the registry needs no list: each service's app registration carries the app
    // role naming it, assigned in the IdP; a role naming another service grants nothing.
    [Fact]
    public async Task AServiceRegistersByTheAppRoleNamingIt_WithoutTheMap()
    {
        var codeSets = Radiology() with
        {
            ServiceName = "code-sets",
            Processes = [],
            Queryables = [new QueryableSourceResponse { ServiceName = "code-sets", Name = "CodeSetSource", QueryUrl = "/code-sets/queryable/codesetsource/query" }],
        };

        using var registered = await RegisterAsync(codeSets, "code-sets-app", $"{ServiceRole},Kaleido.Registrant.code-sets");
        using var otherService = await RegisterAsync(codeSets, "provider-search-app", $"{ServiceRole},Kaleido.Registrant.provider-search");
        using (var heartbeat = await SendAsync(Request(HttpMethod.Put, $"/registry/instances/{codeSets.InstanceId}/heartbeat", "code-sets-app", $"{ServiceRole},Kaleido.Registrant.code-sets", new RegistryHeartbeat())))
        {
            Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal((HttpStatusCode.Forbidden, RegistryErrorCodes.RegistrantNotAllowed), (otherService.StatusCode, await CodeAsync(otherService)));
        Assert.Equal("CodeSetSource", Assert.Single((await ReadAsync()).Queryables).Name);
    }
}
