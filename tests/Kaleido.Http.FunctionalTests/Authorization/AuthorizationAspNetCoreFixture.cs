using Kaleido.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.AspNetCore.FunctionalTests.Authorization;

/// <summary>
/// TestServer host with header-driven test authentication
/// (<see cref="TestAuthHandler"/>) and <see cref="KaleidoHttpOptions"/>.<c>RequireAuthorization</c>
/// enabled — undeclared capabilities require an authenticated caller,
/// capabilities declaring <c>[KaleidoAuthorization]</c> require the
/// declared role/policy.
/// </summary>
public sealed class AuthorizationAspNetCoreFixture
    : IAsyncLifetime
{
    private IHost? _host;

    public HttpClient Client { get; private set; } = null!;

    internal static HttpRequestMessage AuthenticatedRequest(
        HttpMethod method,
        string url,
        string user = "alice",
        params string[] roles)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserHeader, user);

        if (roles.Length > 0)
        {
            request.Headers.Add(
                TestAuthHandler.RolesHeader,
                string.Join(",", roles));
        }

        return request;
    }

    internal static HttpRequestMessage AuthenticatedJson(
        string url,
        object body,
        string user = "alice",
        params string[] roles)
    {
        var request = AuthenticatedRequest(HttpMethod.Post, url, user, roles);
        request.Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options);
        return request;
    }

    public async ValueTask InitializeAsync()
    {
        _host =
            await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddRouting();

                        services.AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                TestAuthHandler.SchemeName,
                                _ => { });

                        services.AddAuthorization(options =>
                            options.AddPolicy(
                                "clinician-only",
                                p => p.RequireRole("clinician")));

                        var serverConfig = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["Kaleido:Clients:self:BaseUrl"] = "http://localhost/",
                            })
                            .Build();

                        services.AddKaleido(serverConfig, o =>
                            {
                                o.ServiceName = "kaleido";
                                o.DisplayName = "Auth Test Processor";
                                o.Assemblies = new[] { typeof(AuthorizationAspNetCoreFixture).Assembly };
                            })
                            .AddHttp(o =>
                            {
                                o.RequireAuthorization = true;
                                o.RequireProcessOwnership = true;
                            });
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp();
                        });
                    });
                })
                .StartAsync();

        Client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
