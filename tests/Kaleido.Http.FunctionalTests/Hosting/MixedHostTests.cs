using System.Net;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Hosting;

// A host with its own endpoints next to Kaleido's: Kaleido's HTTP behavior (error answers, correlation
// headers) applies to Kaleido's endpoints only; the host's endpoints are the host's (#295).
public sealed class MixedHostTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;

    private HttpClient Client => _client ?? throw new InvalidOperationException("The host is not started.");

    public async ValueTask InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<FunctionalRecordData>();
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = "kaleido",
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(FunctionalRecordContext).Assembly],
                    })
                        .AddInMemoryStores()
                        .AddHttp();
                });
                webBuilder.Configure(app =>
                {
                    app.UseExceptionHandler(handler => handler.Run(context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        return context.Response.WriteAsync("host error page");
                    }));
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/host/ok", () => "ok");
                        endpoints.MapGet("/host/boom", IResult () => throw new InvalidOperationException("host failure"));
                        endpoints.MapKaleidoHttp();
                    });
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

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
    public async Task AThrowingHostEndpoint_IsAnsweredByTheHost_WithNoKaleidoHeaders()
    {
        using var response = await SendAsync("/host/boom", requestId: "req-1");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("host error page", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Fact]
    public async Task AHostEndpoint_GetsNoKaleidoHeaders()
    {
        using var response = await SendAsync("/host/ok", requestId: "req-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Fact]
    public async Task AKaleidoEndpoint_EchoesTheRequestId()
    {
        using var response = await SendAsync("/kaleido/registry", requestId: "req-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("req-1", Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
    }

    [Fact]
    public async Task AMalformedCorrelationHeader_OnAKaleidoEndpoint_IsKaleidos400()
    {
        using var response = await SendAsync("/kaleido/registry", processId: "not-a-guid");

        await AssertArgumentErrorAsync(response, KaleidoCorrelationHeaders.ProcessId);
    }

    [Fact]
    public async Task ANonGuidProcessId_InTheStateRoute_IsKaleidos400()
    {
        using var response = await SendAsync("/kaleido/processes/not-a-guid");

        await AssertArgumentErrorAsync(response, "not-a-guid");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string? requestId = null, string? processId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (requestId is not null)
        {
            request.Headers.Add(KaleidoCorrelationHeaders.RequestId, requestId);
        }

        if (processId is not null)
        {
            request.Headers.Add(KaleidoCorrelationHeaders.ProcessId, processId);
        }

        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertArgumentErrorAsync(HttpResponseMessage response, string named)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(KaleidoErrorCodes.ArgumentError, body, StringComparison.Ordinal);
        Assert.Contains(named, body, StringComparison.Ordinal);
    }
}
