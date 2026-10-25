using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Http;
using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.IntegrationTests.Guide.Returns;

// The "returns" service the guide's ReturnOrderHandler hands the process to (docs/guide/processes.md,
// Across processors): a second Kaleido service on its own TestServer, with its own state.
public sealed class ReturnsHost : IAsyncDisposable
{
    private readonly IHost _host;

    private ReturnsHost(IHost host) => _host = host;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A handler for the orders service's "returns" client: its calls reach this server.</summary>
    public HttpMessageHandler CreateHandler() => _host.GetTestServer().CreateHandler();

    public static async Task<ReturnsHost> StartAsync() =>
        new(await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = "returns";
                            o.Assemblies = [typeof(ReturnsHost).Assembly];
                            o.TypeFilter = type => type.Namespace == typeof(ReturnsHost).Namespace;
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                        })
                        .AddInMemoryStores()
                        .AddHttp();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel));

    public async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, string url, Guid processId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Kaleido-Process-Id", processId.ToString());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _host.GetTestClient().SendAsync(request, Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        return (response.StatusCode, text.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(text));
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(Cancel);
        _host.Dispose();
    }
}

[ProcessStep(Version = "1.0.0", DisplayName = "Open return", Description = "Opens the return of an order handed over by the orders service.")]
public sealed record OpenReturnStep : IProcessStep;

[ProcessStep(Version = "1.0.0", DisplayName = "Receive return", Description = "Records that the returned goods arrived.")]
[DependsOn<OpenReturnStep>]
public sealed record ReceiveReturnStep : IProcessStep;

public sealed class OpenReturnHandler : IProcessStepHandler<OpenReturnStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(OpenReturnStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class ReceiveReturnHandler : IProcessStepHandler<ReceiveReturnStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ReceiveReturnStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
