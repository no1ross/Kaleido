using System.Net;
using System.Text;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Queryable.Execution;

// A host's own exception handling (WebApplication adds the developer exception page in Development;
// production hosts add UseExceptionHandler) sits in the host's pipeline, in front of every endpoint.
// Kaleido answers its errors inside its own endpoints, so they are still Kaleido's answers (#210).
public sealed class HostExceptionHandlerTests
{
    [Fact]
    public async Task ValidationError_WithTheDeveloperExceptionPage_Returns400WithTheKaleidoError() =>
        await AssertPagingNotSupportedAsync(app => app.UseDeveloperExceptionPage());

    [Fact]
    public async Task ValidationError_WithAHostExceptionHandler_Returns400WithTheKaleidoError() =>
        await AssertPagingNotSupportedAsync(app => app.UseExceptionHandler(handler =>
            handler.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync("host error page");
            })));

    // A body that does not fit its type fails before endpoint filters in minimal-API binding; Kaleido
    // reads bodies itself so the developer exception page never answers (and never logs an Error).
    [Fact]
    public async Task MalformedParameters_WithTheDeveloperExceptionPage_Returns400WithTheKaleidoError()
    {
        using var host = await StartAsync(app => app.UseDeveloperExceptionPage());

        using var content = new StringContent("""{"parameters":{"category":42}}""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync(
            "/kaleido/queryable/functionalrecordcontextsource/functionalrecordgridview/query",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("argument_error", body, StringComparison.Ordinal);
        Assert.Contains("$.parameters.category", body, StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IHost> StartAsync(Action<IApplicationBuilder> hostExceptionHandling) =>
        await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<FunctionalRecordData>();
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = "kaleido";
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            o.Assemblies = [typeof(FunctionalRecordContext).Assembly];
                        })
                        .AddHttp();
                });
                webBuilder.Configure(app =>
                {
                    hostExceptionHandling(app);
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

    private static async Task AssertPagingNotSupportedAsync(Action<IApplicationBuilder> hostExceptionHandling)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<FunctionalRecordData>();
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = "kaleido";
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            o.Assemblies = [typeof(FunctionalRecordContext).Assembly];
                        })
                        .AddHttp();
                });
                webBuilder.Configure(app =>
                {
                    hostExceptionHandling(app);
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

        using var content = new StringContent("""{"query":{"page":{"size":50,"offset":0}}}""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync(
            "/kaleido/queryable/functionalunpagedrecordsource/query",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("qry_paging_not_supported", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
