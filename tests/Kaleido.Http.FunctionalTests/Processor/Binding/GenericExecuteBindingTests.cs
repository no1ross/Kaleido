using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Binding;

// #521: the generic execute endpoint binds its body with Kaleido's own JSON rules, like the per-step
// routes. A host whose HttpJsonOptions use another naming (snake_case here) neither rewrites the
// endpoint's request contract nor answers a malformed body with its own page or a bare 400.
public sealed class GenericExecuteBindingTests : IAsyncLifetime
{
    private const string ServiceName = "binding";

    private IHost? _host;
    private HttpClient _client = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = ServiceName,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(GenericExecuteBindingTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(GenericExecuteBindingTests).Namespace,
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
            .StartAsync(Cancel);

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
    public async Task AValidRequest_BindsByKaleidosContract_WhateverTheHostsNaming()
    {
        using var response = await PostAsync($$$"""{"steps":[{"stepName":"{{{nameof(BindingStartStep)}}}","request":{}}]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.True(document.RootElement.TryGetProperty("processId", out _), document.RootElement.ToString());
    }

    [Theory]
    [InlineData("""{"steps": [""")]
    [InlineData("""{"steps": "not a list"}""")]
    public async Task AMalformedBody_GetsKaleidos400(string body)
    {
        using var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(KaleidoErrorCodes.ArgumentError, document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    private Task<HttpResponseMessage> PostAsync(string json) =>
        _client.PostAsync($"/{ServiceName}/processes/execute", new StringContent(json, Encoding.UTF8, "application/json"), Cancel);
}

[ProcessStep(DisplayName = "Start", Description = "Starts the case.")]
public sealed record BindingStartStep : IProcessStep;

public sealed class BindingStartHandler : IProcessStepHandler<BindingStartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BindingStartStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
