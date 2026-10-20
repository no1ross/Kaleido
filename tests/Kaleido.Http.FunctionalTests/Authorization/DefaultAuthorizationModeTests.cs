using Kaleido.Exceptions;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Authorization;

// #288: a host that never chooses an authorization mode gets Authenticated (ADR 0021). Without
// authentication it does not start; with authentication its capabilities refuse anonymous callers.
public sealed class DefaultAuthorizationModeTests
{
    [Fact]
    public async Task AHostThatChoosesNoMode_AndHasNoAuthentication_DoesNotStart_AndSaysWhatToChoose()
    {
        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => StartAsync(withAuthentication: false));

        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, exception.Code);
        Assert.Contains("AuthorizationMode = None", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHostThatChoosesNoMode_RefusesAnonymousCallers_AndAdmitsAuthenticatedOnes()
    {
        using var host = await StartAsync(withAuthentication: true);
        var client = host.GetTestClient();

        using var anonymous = await client.SendAsync(StartStep(user: null), TestContext.Current.CancellationToken);
        using var admitted = await client.SendAsync(StartStep(user: "alice"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    // The Behavior models' initial step, which declares no authorization of its own.
    private static HttpRequestMessage StartStep(string? user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/defaultmode/processes/steps/behaviorstartstep")
        {
            Content = new StringContent("""{"processStep":{}}""", System.Text.Encoding.UTF8, "application/json"),
        };

        if (user is not null)
        {
            request.Headers.Add(TestAuthHandler.UserHeader, user);
        }

        return request;
    }

    private static async Task<IHost> StartAsync(bool withAuthentication) =>
        await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();

                    if (withAuthentication)
                    {
                        services.AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                        services.AddAuthorization();
                    }

                    // No AuthorizationMode: the default applies.
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = "defaultmode";
                            o.Assemblies = [typeof(DefaultAuthorizationModeTests).Assembly];
                            o.TypeFilter = type => type.Namespace == "Kaleido.Http.FunctionalTests.Processor.Behavior";
                        })
                        .AddHttp();
                });
                web.Configure(app =>
                {
                    app.UseRouting();

                    if (withAuthentication)
                    {
                        app.UseAuthentication();
                        app.UseAuthorization();
                    }

                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);
}
