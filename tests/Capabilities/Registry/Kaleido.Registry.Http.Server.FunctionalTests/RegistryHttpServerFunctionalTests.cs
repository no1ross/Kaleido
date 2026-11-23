using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Registry.Http.Server.FunctionalTests;

public sealed class RegistryHttpServerFunctionalTests
{
    [Fact]
    public async Task StandaloneAuthority_RegistersAndPublishesTypedCapabilitiesWithoutAClient()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var id = Guid.NewGuid();

        using var registration = await client.PostAsJsonAsync("/registry/instances", Registration(id), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var lease = await registration.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.IsType<RegistryLease>(lease).InstanceId);

        using var capabilities = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
        Assert.NotNull(capabilities.Headers.ETag);
        Assert.NotEqual("\"\"", capabilities.Headers.ETag.Tag);
        var published = await capabilities.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var item = Assert.Single(Assert.IsType<KaleidoRegistry>(published).Queryables);
        Assert.Equal("orders", item.Name);
        Assert.Equal("/catalog/1/queryable/orders/query", Assert.Single(item.Bindings.Http).Path);

        using var availability = await client.GetAsync("/registry/availability", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        var latest = await availability.Content.ReadFromJsonAsync<KaleidoRegistryAvailability>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(RegistryStatusNames.Available, Assert.Single(Assert.IsType<KaleidoRegistryAvailability>(latest).Services).Status);
        Assert.Equal(capabilities.Headers.ETag.Tag.Trim('"'), latest.CapabilitiesRevision);

        using var scope = app.Services.CreateScope();
        var health = await scope.ServiceProvider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Tags.Contains("ready"), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Contains("kaleido-registry-store", health.Entries.Keys);
        Assert.DoesNotContain("kaleido-registration", health.Entries.Keys);

        using var unrelated = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken);
        Assert.Equal("host", await unrelated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(unrelated.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("user")]
    [InlineData("wrong-app")]
    [InlineData("missing-version")]
    public async Task Registration_RequiresTheOwnServiceApplicationAndAVersion(string caller)
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with { ServiceVersion = caller == "missing-version" ? string.Empty : "1" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        var token = caller switch
        {
            "anonymous" => null,
            "user" => TestTokenIssuer.User("alice", ["viewer"], app: "catalog-app"),
            "wrong-app" => TestTokenIssuer.App("other-app", ["registrant", "Kaleido.Registrant.other"]),
            "missing-version" => TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]),
            _ => throw new ArgumentOutOfRangeException(nameof(caller)),
        };
        if (token is not null)
        {
            request.Headers.Authorization = TestJwtAuthentication.Bearer(token);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var invalidVersion = caller == "missing-version";
        Assert.Equal(invalidVersion ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(invalidVersion ? RegistryErrorCodes.InvalidRegistration : RegistryErrorCodes.RegistrantNotAllowed,
            Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);

        using var read = new HttpRequestMessage(HttpMethod.Get, "/registry/capabilities");
        read.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("reader", ["registry-reader"]));
        using var capabilities = await client.SendAsync(read, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
        var published = Assert.IsType<KaleidoRegistry>(await capabilities.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Empty(published.Queryables);
    }

    [Fact]
    public async Task StandaloneAuthority_RegistrationAndRemovalInvalidateTheCachedView()
    {
        await using var app = await StartAsync(registryOptions: new KaleidoRegistryOptions
        {
            Cache = new KaleidoRegistryCacheOptions { ViewTtl = TimeSpan.FromHours(1) },
        });
        using var client = app.GetTestClient();
        using var before = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var empty = await before.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Empty(Assert.IsType<KaleidoRegistry>(empty).Queryables);

        var id = Guid.NewGuid();
        using var registration = await client.PostAsJsonAsync("/registry/instances", Registration(id), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var after = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var published = await after.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("orders", Assert.Single(Assert.IsType<KaleidoRegistry>(published).Queryables).Name);

        using var beforeRemoval = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var status = await beforeRemoval.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.Single(Assert.Single(Assert.IsType<KaleidoRegistryStatus>(status).Services).Instances).InstanceId);
        using var removal = await client.DeleteAsync($"/registry/instances/{id:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        using var afterRemoval = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var noInstances = await afterRemoval.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Empty(Assert.IsType<KaleidoRegistryStatus>(noInstances).Services.SelectMany(x => x.Instances));
    }

    [Fact]
    public async Task FirstRegisteredServiceVersion_RemainsCurrentWhenAnotherVersionJoins()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var first = Registration(Guid.NewGuid());
        var second = Registration(Guid.NewGuid()) with
        {
            ServiceVersion = "2",
            BuildVersion = "2.0.0-preview.1",
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "orders",
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/2/queryable/orders/query" }],
                },
            }],
            Transports = [new RegistryTransportRoute { Transport = "http", GatewayRoute = "/catalog/2" }],
        };
        using var firstResponse = await client.PostAsJsonAsync("/registry/instances", first, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var secondResponse = await client.PostAsJsonAsync("/registry/instances", second, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        using var statusResponse = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var status = Assert.IsType<KaleidoRegistryStatus>(await statusResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal([("1", true), ("2", false)], status.Services.OrderBy(service => service.ServiceVersion, StringComparer.Ordinal).Select(service => (service.ServiceVersion, service.IsCurrent)));
        using var capabilitiesResponse = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var capabilities = Assert.IsType<KaleidoRegistry>(await capabilitiesResponse.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(["1", "2"], capabilities.Queryables.Select(item => item.ServiceVersion).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task RollingBuild_WaitsToPublishANewQueryableUntilEveryReadyInstanceHasIt()
    {
        await using var app = await StartAsync(registryOptions: new KaleidoRegistryOptions
        {
            Cache = new KaleidoRegistryCacheOptions { ViewTtl = TimeSpan.FromHours(1) },
        });
        using var client = app.GetTestClient();
        var old = Registration(Guid.NewGuid());
        var next = Registration(Guid.NewGuid()) with
        {
            BuildVersion = "1.0.0-preview.617",
            Queryables = [.. old.Queryables, new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "returns",
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/returns/query" }],
                },
            }],
        };
        using var firstRegistration = await client.PostAsJsonAsync("/registry/instances", old, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var secondRegistration = await client.PostAsJsonAsync("/registry/instances", next, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstRegistration.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondRegistration.StatusCode);
        using var during = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var first = Assert.IsType<KaleidoRegistry>(await during.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal("orders", Assert.Single(first.Queryables).Name);
        using var status = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var instances = Assert.IsType<KaleidoRegistryStatus>(await status.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(2, Assert.Single(instances.Services).ReadyInstances);

        using var removed = await client.DeleteAsync($"/registry/instances/{old.InstanceId:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var after = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var published = Assert.IsType<KaleidoRegistry>(await after.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(["orders", "returns"], published.Queryables.Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.NotEqual(during.Headers.ETag, after.Headers.ETag);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public async Task RegistryView_ExpiresAtTheConfiguredLifetime(long ticksFromExpiration, bool sameView)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        await using var app = await StartAsync(fakeTime: time, registryOptions: new KaleidoRegistryOptions
        {
            Cache = new KaleidoRegistryCacheOptions { ViewTtl = TimeSpan.FromSeconds(3) },
        });
        using var client = app.GetTestClient();
        using var registered = await client.PostAsJsonAsync("/registry/instances", Registration(Guid.NewGuid()), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        using var firstResponse = await client.GetAsync("/registry", TestContext.Current.CancellationToken);
        var first = Assert.IsType<KaleidoRegistry>(await firstResponse.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));

        time.Advance(TimeSpan.FromSeconds(3) + TimeSpan.FromTicks(ticksFromExpiration));
        using var secondResponse = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var second = Assert.IsType<KaleidoRegistryStatus>(await secondResponse.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(sameView ? first.GeneratedAt : time.GetUtcNow(), second.GeneratedAt);
        Assert.Equal("orders", Assert.Single(first.Queryables).Name);
    }

    [Fact]
    public async Task CapabilitiesAndAvailability_KeepTheirETagsWhenOnlyInstanceStatusChanges()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        await using var app = await StartAsync(fakeTime: time);
        using var client = app.GetTestClient();
        var first = Registration(Guid.NewGuid());
        var second = Registration(Guid.NewGuid());
        using var firstRegistration = await client.PostAsJsonAsync("/registry/instances", first, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstRegistration.StatusCode);
        using var capabilities = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        using var availability = await client.GetAsync("/registry/availability", TestContext.Current.CancellationToken);
        using var firstStatus = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.NotNull(capabilities.Headers.ETag);
        Assert.NotNull(availability.Headers.ETag);
        Assert.NotNull(firstStatus.Headers.ETag);

        using var secondRegistration = await client.PostAsJsonAsync("/registry/instances", second, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, secondRegistration.StatusCode);
        using var sameAvailability = new HttpRequestMessage(HttpMethod.Get, "/registry/availability");
        sameAvailability.Headers.IfNoneMatch.Add(availability.Headers.ETag);
        using var unchangedAvailability = await client.SendAsync(sameAvailability, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchangedAvailability.StatusCode);
        using var status = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.NotEqual(firstStatus.Headers.ETag, status.Headers.ETag);
        var twoReady = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await status.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(2, twoReady.ReadyInstances);

        using var firstUnhealthy = await client.PutAsJsonAsync($"/registry/instances/{first.InstanceId:D}/heartbeat", new RegistryHeartbeat { Healthy = false }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var secondUnhealthy = await client.PutAsJsonAsync($"/registry/instances/{second.InstanceId:D}/heartbeat", new RegistryHeartbeat { Healthy = false }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstUnhealthy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondUnhealthy.StatusCode);
        time.Advance(TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1));
        using var sameCapabilities = new HttpRequestMessage(HttpMethod.Get, "/registry/capabilities");
        sameCapabilities.Headers.IfNoneMatch.Add(capabilities.Headers.ETag);
        using var unchangedCapabilities = await client.SendAsync(sameCapabilities, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchangedCapabilities.StatusCode);
        using var noLongerAvailable = await client.GetAsync("/registry/availability", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, noLongerAvailable.StatusCode);
        var unavailable = Assert.Single(Assert.IsType<KaleidoRegistryAvailability>(await noLongerAvailable.Content.ReadFromJsonAsync<KaleidoRegistryAvailability>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, unavailable.Status);
        Assert.NotEqual(availability.Headers.ETag, noLongerAvailable.Headers.ETag);
    }

    [Fact]
    public async Task HeartbeatExtendsARegistryLease_AndTheInstanceTurnsUnavailableAfterItExpires()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));
        await using var app = await StartAsync(fakeTime: time);
        using var client = app.GetTestClient();
        var registration = Registration(Guid.NewGuid());
        using var first = await client.PostAsJsonAsync("/registry/instances", registration, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var initialLease = Assert.IsType<RegistryLease>(await first.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(30), initialLease.LeaseUntilUtc);

        time.Advance(TimeSpan.FromSeconds(25));
        using var heartbeat = await client.PutAsJsonAsync($"/registry/instances/{registration.InstanceId:D}/heartbeat", new RegistryHeartbeat(),
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        var renewed = Assert.IsType<RegistryLease>(await heartbeat.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(30), renewed.LeaseUntilUtc);

        time.Advance(TimeSpan.FromSeconds(25));
        using var beforeExpiry = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var ready = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await beforeExpiry.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Available, ready.Status);
        Assert.Equal(1, ready.ReadyInstances);

        time.Advance(TimeSpan.FromSeconds(10));
        using var afterExpiry = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        var unavailable = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(await afterExpiry.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, unavailable.Status);
        Assert.Equal(0, unavailable.ReadyInstances);
        Assert.Equal(RegistryInstanceStateNames.NotReady, Assert.Single(unavailable.Instances).State);
        using var capabilities = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var published = Assert.IsType<KaleidoRegistry>(await capabilities.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        Assert.Equal("orders", Assert.Single(published.Queryables).Name);
    }

    [Fact]
    public async Task StandaloneAuthority_HeartbeatsReportsStatusAndRemovesAnInstance()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var id = Guid.NewGuid();
        using var registered = await client.PostAsJsonAsync("/registry/instances", Registration(id), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var heartbeat = await client.PutAsJsonAsync($"/registry/instances/{id:D}/heartbeat", new RegistryHeartbeat { Healthy = false }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        var lease = await heartbeat.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.IsType<RegistryLease>(lease).InstanceId);

        using var status = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.NotNull(status.Headers.ETag);
        Assert.True(status.Headers.ETag.IsWeak);
        var document = await status.Content.ReadFromJsonAsync<KaleidoRegistryStatus>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var service = Assert.Single(Assert.IsType<KaleidoRegistryStatus>(document).Services);
        Assert.Equal(RegistryStatusNames.Unavailable, service.Status);
        Assert.Equal(0, service.ReadyInstances);
        Assert.Equal(RegistryInstanceStateNames.NotReady, Assert.Single(service.Instances).State);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/registry/status");
        conditional.Headers.IfNoneMatch.Add(status.Headers.ETag);
        using var unchanged = await client.SendAsync(conditional, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);

        using var removed = await client.DeleteAsync($"/registry/instances/{id:D}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var unknown = await client.PutAsJsonAsync($"/registry/instances/{id:D}/heartbeat", new RegistryHeartbeat(), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var error = await unknown.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(RegistryErrorCodes.InstanceUnknown, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
    }

    [Theory]
    [InlineData("/catalog/2/queryable/orders/query")]
    [InlineData("https://other.test/catalog/1/queryable/orders/query")]
    [InlineData("/catalog/1/../other")]
    public async Task StandaloneAuthority_RefusesUnsafeOrWrongVersionBindings(string path)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "orders",
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = path }],
                },
            }],
        };

        using var response = await client.PostAsJsonAsync("/registry/instances", registration, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(RegistryErrorCodes.InvalidRegistration, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        using var capabilities = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        var published = await capabilities.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Empty(Assert.IsType<KaleidoRegistry>(published).Queryables);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("method")]
    [InlineData("operation")]
    [InlineData("http")]
    [InlineData("bindings")]
    [InlineData("views")]
    [InlineData("queryable")]
    [InlineData("queryables")]
    [InlineData("process-bindings")]
    public async Task StandaloneAuthority_RejectsExplicitlyNullBindingStructures(string member)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var body = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(Registration(Guid.NewGuid()), KaleidoJsonOptions.Options));
        var queryable = Assert.IsType<JsonObject>(body["queryables"]?[0]);
        var bindings = Assert.IsType<JsonObject>(queryable["bindings"]);
        var operations = Assert.IsType<JsonArray>(bindings["http"]);
        switch (member)
        {
            case "path": Assert.IsType<JsonObject>(operations[0])["path"] = null; break;
            case "method": Assert.IsType<JsonObject>(operations[0])["method"] = null; break;
            case "operation": operations[0] = null; break;
            case "http": bindings["http"] = null; break;
            case "bindings": queryable["bindings"] = null; break;
            case "views": queryable["views"] = null; break;
            case "queryable": Assert.IsType<JsonArray>(body["queryables"])[0] = null; break;
            case "queryables": body["queryables"] = null; break;
            case "process-bindings":
                var process = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(new ProcessRegistryItem { ServiceName = "catalog", Name = "catalog" }, KaleidoJsonOptions.Options));
                process["bindings"] = null;
                body["processes"] = new JsonArray(process);
                break;
        }

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/registry/instances", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(RegistryErrorCodes.InvalidRegistration, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
    }

    [Fact]
    public async Task UnchangedCapabilityRead_UsesTheHttpEtagForConditionalRequests()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var registration = await client.PostAsJsonAsync("/registry/instances", Registration(Guid.NewGuid()), KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        using var first = await client.GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(first.Headers.ETag);
        Assert.True(first.Headers.ETag.IsWeak);
        Assert.NotEqual("\"\"", first.Headers.ETag.Tag);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/registry/capabilities");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag);
        using var unchanged = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);

        using var strong = new HttpRequestMessage(HttpMethod.Get, "/registry/capabilities");
        strong.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(first.Headers.ETag.Tag));
        using var matched = await client.SendAsync(strong, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, matched.StatusCode);
    }

    [Fact]
    public async Task StrictPartialRead_StillReturns502WhenTheEtagMatches()
    {
        await using var app = await StartAsync(registryOptions: new KaleidoRegistryOptions
        {
            Expected = [new KaleidoRegistryExpectedService { ServiceName = "catalog", Versions = ["1"] }],
        });
        using var client = app.GetTestClient();

        using var first = await client.GetAsync("/registry?strict", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.NotNull(first.Headers.ETag);
        var partial = await first.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.True(Assert.IsType<KaleidoRegistry>(partial).IsPartial);
        Assert.Single(partial.Missing);

        using var strict = new HttpRequestMessage(HttpMethod.Get, "/registry?strict");
        strict.Headers.IfNoneMatch.Add(first.Headers.ETag);
        using var stillPartial = await client.SendAsync(strict, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, stillPartial.StatusCode);

        using var ordinary = new HttpRequestMessage(HttpMethod.Get, "/registry");
        ordinary.Headers.IfNoneMatch.Add(first.Headers.ETag);
        using var unchanged = await client.SendAsync(ordinary, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
    }

    [Fact]
    public async Task FilteredRegistry_NeverReusesAnotherCallersEtag()
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables =
            [
                new QueryableRegistryItem { ServiceName = "catalog", Name = "public", Authorization = new AuthorizationMetadata(null, []) { AllowAnonymous = true } },
                new QueryableRegistryItem { ServiceName = "catalog", Name = "protected", Authorization = new AuthorizationMetadata(null, ["radiology"]) },
            ],
        };

        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var privileged = new HttpRequestMessage(HttpMethod.Get, "/registry");
        privileged.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["radiology"]));
        using var visible = await client.SendAsync(privileged, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        Assert.NotNull(visible.Headers.ETag);
        var full = await visible.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.IsType<KaleidoRegistry>(full).Queryables.Count);

        using var restricted = new HttpRequestMessage(HttpMethod.Get, "/registry");
        restricted.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("bob"));
        restricted.Headers.IfNoneMatch.Add(visible.Headers.ETag);
        using var limited = await client.SendAsync(restricted, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, limited.StatusCode);
        var filtered = await limited.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("public", Assert.Single(Assert.IsType<KaleidoRegistry>(filtered).Queryables).Name);
        Assert.NotEqual(visible.Headers.ETag, limited.Headers.ETag);
    }

    [Fact]
    public async Task FilteredRegistry_HidesUnauthorizedRelationshipsAndLookupsWithoutDroppingTheInput()
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var clerkAndAdmin = new AuthorizationMetadata(null, ["vis-clerk", "vis-admin"]);
        var adminOnly = new AuthorizationMetadata(null, ["vis-admin"]);
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "PatientSource",
                Authorization = adminOnly,
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/patientsource/query" }],
                },
            }],
            Processes = [new ProcessRegistryItem
            {
                ServiceName = "catalog",
                Name = "catalog",
                Steps =
                [
                    new ProcessStepResponse { Name = "StartStep", Authorization = clerkAndAdmin },
                    new ProcessStepResponse { Name = "AdminStep", Authorization = adminOnly },
                    new ProcessStepResponse
                    {
                        Name = "ClerkStep",
                        Authorization = clerkAndAdmin,
                        Dependencies = [new ProcessStepSummary { Name = "StartStep", Authorization = clerkAndAdmin }],
                        AvailableUntil = [new ProcessStepSummary { Name = "AdminStep", Authorization = adminOnly }],
                        Fields = [new ProcessFieldMetadata
                        {
                            Name = "PatientId",
                            DataType = new DataTypeMetadata("string", "uuid"),
                            Lookup = new ProcessFieldLookup { Service = "catalog", Source = "PatientSource", Field = "Id", Group = "patient" },
                        }],
                    },
                ],
            }],
        };
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        async Task<KaleidoRegistry> ForRoleAsync(string role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/registry");
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", [role]));
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.IsType<KaleidoRegistry>(await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        }

        var clerk = await ForRoleAsync("vis-clerk");
        var admin = await ForRoleAsync("vis-admin");
        var clerkSteps = Assert.Single(clerk.Processes).Steps!;
        var clerkStep = Assert.Single(clerkSteps, step => step.Name == "ClerkStep");
        Assert.Equal("StartStep", Assert.Single(clerkStep.Dependencies).Name);
        Assert.Empty(clerkStep.AvailableUntil);
        var input = Assert.Single(clerkStep.Fields);
        Assert.Null(input.Lookup);
        Assert.Equal("pro_not_permitted", input.Notice?.Code);
        Assert.DoesNotContain(clerkSteps, step => step.Name == "AdminStep");
        Assert.Empty(clerk.Queryables);

        var adminStep = Assert.Single(Assert.Single(admin.Processes).Steps!, step => step.Name == "ClerkStep");
        Assert.Equal("AdminStep", Assert.Single(adminStep.AvailableUntil).Name);
        Assert.Equal("PatientSource", Assert.Single(adminStep.Fields).Lookup?.Source);
        Assert.Equal("PatientSource", Assert.Single(admin.Queryables).Name);
    }

    [Fact]
    public async Task AnonymousRegistryRead_AdvertisesOnlyAnonymousSteps_NotAuthenticatedSources()
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with
        {
            Processes = [new ProcessRegistryItem
            {
                ServiceName = "catalog",
                Name = "catalog",
                Steps =
                [
                    new ProcessStepResponse { Name = "PublicStep", Authorization = new AuthorizationMetadata(null, []) { AllowAnonymous = true } },
                    new ProcessStepResponse { Name = "SecuredStep", Authorization = new AuthorizationMetadata(null, ["reviewer"]) },
                    new ProcessStepResponse { Name = "AuthenticatedStep" },
                ],
            }],
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "securedsource",
                Authorization = new AuthorizationMetadata(null, ["internal"]),
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/securedsource/query" }],
                },
            }],
        };
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var response = await client.GetAsync("/registry", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var visible = Assert.IsType<KaleidoRegistry>(await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        var step = Assert.Single(Assert.Single(visible.Processes).Steps!);
        Assert.Equal("PublicStep", step.Name);
        Assert.True(step.Authorization?.AllowAnonymous);
        Assert.Empty(visible.Queryables);
    }

    [Fact]
    public async Task ViewAuthorization_RetainsAVisibleViewWithoutExposingItsDeniedSourceBinding()
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "catalog",
                Name = "securedsource",
                Authorization = new AuthorizationMetadata(null, ["internal"]),
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/securedsource/query" }],
                },
                Views =
                [
                    new QueryableRegistryViewItem
                    {
                        Name = "adminview",
                        Authorization = new AuthorizationMetadata(null, ["admin"]),
                        Bindings = new RegistryTransportBindings
                        {
                            Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/securedsource/adminview/query" }],
                        },
                    },
                    new QueryableRegistryViewItem
                    {
                        Name = "internalview",
                        Authorization = new AuthorizationMetadata(null, ["internal", "admin"]),
                        Bindings = new RegistryTransportBindings
                        {
                            Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/securedsource/internalview/query" }],
                        },
                    },
                ],
            }],
        };
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        async Task<KaleidoRegistry> ViewForAsync(string[] roles)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/registry");
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", roles));
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.IsType<KaleidoRegistry>(await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
        }

        var viewer = await ViewForAsync(["viewer"]);
        var admin = await ViewForAsync(["admin"]);
        var internalCaller = await ViewForAsync(["internal"]);
        Assert.Empty(viewer.Queryables);
        var adminSource = Assert.Single(admin.Queryables);
        Assert.Empty(adminSource.Bindings.Http);
        Assert.Equal(["adminview", "internalview"], adminSource.Views.Select(view => view.Name).Order(StringComparer.Ordinal));
        var internalSource = Assert.Single(internalCaller.Queryables);
        Assert.Equal("/catalog/1/queryable/securedsource/query", Assert.Single(internalSource.Bindings.Http).Path);
        Assert.Equal("internalview", Assert.Single(internalSource.Views).Name);
    }

    [Fact]
    public async Task BearerRolesAndHostAuthorizationHandler_FilterProcessDiscoveryPerCaller()
    {
        await using var app = await StartAsync(enforceAuthorization: true, superuserHandler: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables = [],
            Processes = [new ProcessRegistryItem
            {
                ServiceName = "catalog",
                Name = "catalog",
                Steps =
                [
                    new ProcessStepResponse { Name = "ReviewOrderStep", Authorization = new AuthorizationMetadata(null, ["reviewer"]) },
                    new ProcessStepResponse { Name = "ClinicianStep", Authorization = new AuthorizationMetadata("clinician-only", []) },
                    new ProcessStepResponse { Name = "OpenStep" },
                ],
            }],
        };
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        async Task<(string[] Steps, string? Revision)> StepsForAsync(string user, string[] roles, bool appOnly = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/registry");
            request.Headers.Authorization = TestJwtAuthentication.Bearer(appOnly ? TestTokenIssuer.App(user, roles) : TestTokenIssuer.User(user, roles));
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var visible = Assert.IsType<KaleidoRegistry>(await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken));
            return ([.. Assert.Single(visible.Processes).Steps!.Select(step => step.Name).Order(StringComparer.Ordinal)], visible.Revision);
        }

        var reviewer = await StepsForAsync("alice", ["reviewer"]);
        var viewer = await StepsForAsync("alice", ["viewer"]);
        var wrongCase = await StepsForAsync("alice", ["REVIEWER"]);
        var appOnly = await StepsForAsync("intake", ["reviewer"], appOnly: true);
        var clinician = await StepsForAsync("carol", ["clinician"]);
        var superuser = await StepsForAsync("root", ["superuser"]);
        Assert.Equal(["OpenStep", "ReviewOrderStep"], reviewer.Steps);
        Assert.Equal(["OpenStep"], viewer.Steps);
        Assert.Equal(viewer.Steps, wrongCase.Steps);
        Assert.Equal(viewer.Revision, wrongCase.Revision);
        Assert.Equal(reviewer.Steps, appOnly.Steps);
        Assert.NotEqual(reviewer.Revision, viewer.Revision);
        Assert.Equal(["ClinicianStep", "OpenStep"], clinician.Steps);
        Assert.Equal(["ClinicianStep", "OpenStep", "ReviewOrderStep"], superuser.Steps);
    }

    [Fact]
    public async Task ZeroTrustRegistry_HidesUndeclaredStepsEvenFromACallerWithAllDeclaredRoles()
    {
        await using var app = await StartAsync(enforceAuthorization: true, zeroTrust: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        var registration = Registration(Guid.NewGuid()) with
        {
            Queryables = [],
            Processes = [new ProcessRegistryItem
            {
                ServiceName = "catalog",
                Name = "catalog",
                Steps =
                [
                    new ProcessStepResponse { Name = "DeclaredStep", Authorization = new AuthorizationMetadata(null, ["internal"]) },
                    new ProcessStepResponse { Name = "UndeclaredStep" },
                ],
            }],
        };
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(registration, options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/registry");
        request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["internal", "clinician", "admin"]));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var visible = await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("DeclaredStep", Assert.Single(Assert.Single(Assert.IsType<KaleidoRegistry>(visible).Processes).Steps!).Name);
    }

    [Fact]
    public async Task IdenticalFilteredViews_HaveDistinctValidatorsForDifferentAuthenticatedCallers()
    {
        await using var app = await StartAsync(enforceAuthorization: true);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        using var publish = new HttpRequestMessage(HttpMethod.Post, "/registry/instances")
        {
            Content = JsonContent.Create(Registration(Guid.NewGuid()), options: KaleidoJsonOptions.Options),
        };
        publish.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("catalog-app", ["registrant", "Kaleido.Registrant.catalog"]));
        using var registered = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var bobRequest = new HttpRequestMessage(HttpMethod.Get, "/registry");
        bobRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("bob"));
        using var bob = await client.SendAsync(bobRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, bob.StatusCode);
        Assert.NotNull(bob.Headers.ETag);
        var bobView = await bob.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("orders", Assert.Single(Assert.IsType<KaleidoRegistry>(bobView).Queryables).Name);

        using var carolRequest = new HttpRequestMessage(HttpMethod.Get, "/registry");
        carolRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("carol"));
        carolRequest.Headers.IfNoneMatch.Add(bob.Headers.ETag);
        using var carol = await client.SendAsync(carolRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, carol.StatusCode);
        var carolView = await carol.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("orders", Assert.Single(Assert.IsType<KaleidoRegistry>(carolView).Queryables).Name);
        Assert.NotEqual(bob.Headers.ETag, carol.Headers.ETag);

        using var sameCaller = new HttpRequestMessage(HttpMethod.Get, "/registry");
        sameCaller.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("bob"));
        sameCaller.Headers.IfNoneMatch.Add(bob.Headers.ETag);
        using var unchanged = await client.SendAsync(sameCaller, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
    }

    private static RegistryInstanceRegistration Registration(Guid instanceId) => new()
    {
        ServiceName = "catalog",
        ServiceVersion = "1",
        BuildVersion = "1.0.0-preview.616",
        InstanceId = instanceId,
        Queryables = [new QueryableRegistryItem
        {
            ServiceName = "catalog",
            Name = "orders",
            Bindings = new RegistryTransportBindings
            {
                Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/catalog/1/queryable/orders/query" }],
            },
        }],
        Transports = [new RegistryTransportRoute { Transport = "http", GatewayRoute = "/catalog/1" }],
    };

    private static async Task<WebApplication> StartAsync(bool enforceAuthorization = false, KaleidoRegistryOptions? registryOptions = null, bool zeroTrust = false,
        bool superuserHandler = false, FakeTimeProvider? fakeTime = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (fakeTime is not null)
        {
            builder.Services.AddSingleton<TimeProvider>(fakeTime);
            builder.Services.Configure<MemoryCacheOptions>(options => options.Clock = new FakeRegistryClock(fakeTime));
        }
        if (enforceAuthorization)
        {
            builder.Services.AddTestJwtBearer();
            builder.Services.AddAuthorization(policies =>
            {
                policies.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
                policies.AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
                if (superuserHandler)
                {
                    policies.AddPolicy("clinician-only", policy => policy.RequireRole("clinician"));
                }
            });
            if (superuserHandler)
            {
                builder.Services.AddSingleton<IAuthorizationHandler, SuperuserRoleHandler>();
            }
        }

        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryHttpServerFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = zeroTrust ? KaleidoAuthorizationMode.ZeroTrust : enforceAuthorization ? KaleidoAuthorizationMode.Authenticated : KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry(enforceAuthorization
                ? (registryOptions ?? new KaleidoRegistryOptions()) with { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" }
                : registryOptions)
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();

        var app = builder.Build();
        if (enforceAuthorization)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapKaleidoControlRegistry();
        app.MapGet("/ordinary", () => Results.Text("host"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class FakeRegistryClock(FakeTimeProvider time) : ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }

    private sealed class SuperuserRoleHandler : AuthorizationHandler<RolesAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolesAuthorizationRequirement requirement)
        {
            if (context.User.IsInRole("superuser") && requirement.AllowedRoles.Any(role => role is "reviewer" or "clinician"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
