using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Server.Registry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.IntegrationTests.Guide;

/// <summary>
/// The guide's orders service (every example of docs/guide/examples) on a TestServer, called over
/// HTTP as the guide shows. A caller signs in with <c>Authorization: Test name|role,role</c>.
/// </summary>
public sealed class GuideHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly TestControlRegistry _registry;
    private readonly bool _ownsRegistry;

    private GuideHost(IHost host, TestControlRegistry registry, bool ownsRegistry)
    {
        _host = host;
        _registry = registry;
        _ownsRegistry = ownsRegistry;
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public IServiceProvider Services => _host.Services;

    public static async Task<GuideHost> StartAsync(
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        Action<IKaleidoBuilder>? configure = null,
        Action<IServiceCollection>? services = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        TestControlRegistry? registry = null)
    {
        var central = registry ?? await TestControlRegistry.StartAsync();
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddAuthentication(TestAuthentication.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
                    s.AddAuthorization();
                    s.AddSingleton<OrderBook>();
                    services?.Invoke(s);

                    var configuration = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>()) { ["Kaleido:BaseUrl"] = "http://registry.test/" };
                    var kaleido = s.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(), new()
                    {
                        ServiceName = "orders",
                        ServiceVersion = "1",
                        Assemblies = [typeof(OrderSource).Assembly],
                        AuthorizationMode = mode,
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddControlRegistryClient();
                    s.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(central.CreateHandler);
                    configure?.Invoke(kaleido);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapKaleidoHttp();
                        endpoints.MapGet("/test/in-process-registry", async (IKaleidoRegistryService registry, CancellationToken ct) =>
                            Results.Json(await registry.GetForCallerAsync(refresh: false, ct), KaleidoJsonOptions.Options));
                    });
                });
            })
            .StartAsync(Cancel);

        await central.WaitForRegistrationAsync(host);
        return new GuideHost(host, central, ownsRegistry: registry is null);
    }

    public async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpMethod method, string url, object? body = null, Guid? processId = null, string? caller = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (processId is { } id)
        {
            request.Headers.Add("X-Kaleido-Process-Id", id.ToString());
        }

        if (caller is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Test {caller}");
        }

        using var response = await _host.GetTestClient().SendAsync(request, Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        return (response.StatusCode, text.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(text));
    }

    /// <summary>Executes a step; fails the test unless the call succeeds (2xx).</summary>
    public async Task<JsonElement> StepAsync(string step, object input, Guid? processId = null, string? caller = null)
    {
        var (status, body) = await SendAsync(HttpMethod.Post, $"/orders/processes/steps/{step}", new { processStep = input }, processId, caller);
        Assert.True((int)status < 300, $"{step}: {(int)status} {body}");
        return body;
    }

    /// <summary>Runs a query; fails the test unless the call succeeds (2xx).</summary>
    public async Task<JsonElement> QueryAsync(string path, object body, string? caller = null)
    {
        var (status, result) = await SendAsync(HttpMethod.Post, $"/orders/queryable/{path}/query", body, caller: caller);
        Assert.True((int)status < 300, $"{path}: {(int)status} {result}");
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(Cancel);
        _host.Dispose();
        if (_ownsRegistry)
        {
            await _registry.DisposeAsync();
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();

            if (!header.StartsWith("Test ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Test ".Length..].Split('|');
            var claims = new List<Claim> { new(ClaimTypes.Name, parts[0]) };
            claims.AddRange((parts.Length > 1 ? parts[1] : string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim(ClaimTypes.Role, role)));

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
        }
    }
}
