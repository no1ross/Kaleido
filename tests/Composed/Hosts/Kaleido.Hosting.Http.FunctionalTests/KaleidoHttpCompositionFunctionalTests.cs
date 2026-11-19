using System.Net;
using System.Net.Http.Json;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Hosting.Http.FunctionalTests;

public sealed class KaleidoHttpCompositionFunctionalTests
{
    private const string ProcessUrl = "/clinic/1/processes/steps/revieworderstep";
    private const string QueryUrl = "/clinic/1/queryable/catalogsource/query";

    [Fact]
    public async Task ComposedService_MapsBothCapabilitiesWithoutAnAuthorityEndpoint_AndKeepsHostRoutesIsolated()
    {
        await using var app = await StartServiceAsync(KaleidoAuthorizationMode.Authenticated);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reader", "reviewer", "host-allowed"]));

        using var query = await client.PostAsJsonAsync(QueryUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        var records = await query.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("one", records.GetProperty("results")[0].GetProperty("name").GetString());
        using var step = await client.PostAsJsonAsync(ProcessUrl,
            new ExecuteStepRequest<Scenarios.ReviewOrderStep> { ProcessStep = new Scenarios.ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, step.StatusCode);
        var result = await step.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(Assert.IsType<ProcessResult>(result).Steps).ExecutionStatus);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/registry", TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("outside", await client.GetStringAsync("/outside", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(QueryUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitSelection_MapsOnlyTheChosenCapability(bool processOnly)
    {
        await using var app = await StartServiceAsync(KaleidoAuthorizationMode.Authenticated,
            mapProcess: processOnly, mapQueryable: !processOnly);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reader", "reviewer", "host-allowed"]));
        using var query = await client.PostAsJsonAsync(QueryUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var process = await client.PostAsJsonAsync(ProcessUrl,
            new ExecuteStepRequest<Scenarios.ReviewOrderStep> { ProcessStep = new Scenarios.ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(processOnly ? HttpStatusCode.NotFound : HttpStatusCode.OK, query.StatusCode);
        Assert.Equal(processOnly ? HttpStatusCode.OK : HttpStatusCode.NotFound, process.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedMode_MapsAnUndeclaredStepForAnAuthenticatedCaller()
    {
        await using var app = await StartServiceAsync(KaleidoAuthorizationMode.Authenticated, mapQueryable: false);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["host-allowed"]));

        using var response = await client.PostAsJsonAsync("/clinic/1/processes/steps/undeclaredstep",
            new ExecuteStepRequest<Scenarios.UndeclaredStep> { ProcessStep = new Scenarios.UndeclaredStep() },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Conventions_AddedBeforeOrAfterCapabilityMapping_ApplyToBothNotTheHost()
    {
        await using var app = await StartServiceAsync(KaleidoAuthorizationMode.Authenticated, conventionsBeforeMapping: true);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reader", "reviewer"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(QueryUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(ProcessUrl,
            new ExecuteStepRequest<Scenarios.ReviewOrderStep> { ProcessStep = new Scenarios.ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("outside", await client.GetStringAsync("/outside", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ZeroTrust_OnlyExplicitlyAuthorizedSourcesAndStepsAreMapped()
    {
        await using var app = await StartServiceAsync(KaleidoAuthorizationMode.ZeroTrust);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ["reader", "reviewer", "viewer", "host-allowed"]));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/clinic/1/queryable/undeclaredsource/query", new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/clinic/1/queryable/undeclaredsource/undeclaredview/query", new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/clinic/1/processes/steps/undeclaredstep", new { processStep = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/clinic/1/queryable/undeclaredsource/visibleview/query", new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(QueryUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(ProcessUrl,
            new ExecuteStepRequest<Scenarios.ReviewOrderStep> { ProcessStep = new Scenarios.ReviewOrderStep { Code = "A-1" } },
            KaleidoJsonOptions.Options, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task ReaderOnlyHost_MapsNoCapabilityOrRegistryEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoHttpCompositionFunctionalTests).Assembly],
            TypeFilter = _ => false,
        }).AddHosting().AddHttp();
        var app = builder.Build();
        app.MapKaleido();
        app.MapGet("/outside", () => Results.Text("outside"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        await using (app)
        {
            using var client = app.GetTestClient();
            Assert.Equal("outside", await client.GetStringAsync("/outside", TestContext.Current.CancellationToken));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/registry", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/reader/1/processes/execute", TestContext.Current.CancellationToken)).StatusCode);
        }
    }

    [Fact]
    public async Task AuthorityHost_MapsOnlyTheCentralRegistryWithoutARegistryParticipant()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("registry-reader", policy => policy.RequireRole("reader"));
            options.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
        });
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoHttpCompositionFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { ReaderPolicy = "registry-reader", RegistrantPolicy = "registry-registrant" })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleido().MapCentralRegistry();
        app.MapGet("/outside", () => Results.Text("outside"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("reader-app", ["reader"]));
        using var response = await client.GetAsync("/registry/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(ProcessUrl, TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal("outside", await client.GetStringAsync("/outside", TestContext.Current.CancellationToken));
    }

    private static async Task<WebApplication> StartServiceAsync(KaleidoAuthorizationMode mode, bool conventionsBeforeMapping = false,
        bool mapProcess = true, bool mapQueryable = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddAuthorization(options => options.AddPolicy("host-policy", policy => policy.RequireRole("host-allowed")));
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "clinic",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoHttpCompositionFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(Scenarios.ReviewOrderStep).Namespace,
        })
            .AddHosting(serverOptions: new() { AuthorizationMode = mode })
            .AddInMemoryProviders()
            .AddHttp()
            .AddProcessHttpServer();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        var kaleido = app.MapKaleido();
        if (conventionsBeforeMapping)
        {
            kaleido.RequireAuthorization("host-policy");
        }

        if (mapQueryable)
        {
            Assert.Same(kaleido, kaleido.MapQueryable());
        }

        if (mapProcess)
        {
            Assert.Same(kaleido, kaleido.MapProcess());
        }

        if (!conventionsBeforeMapping)
        {
            kaleido.RequireAuthorization("host-policy");
        }

        app.MapGet("/outside", () => Results.Text("outside"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
