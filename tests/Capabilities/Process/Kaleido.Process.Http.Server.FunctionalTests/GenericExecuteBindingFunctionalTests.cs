using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Provider.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests.Binding;

public sealed class GenericExecuteBindingFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AValidRequest_UsesKaleidosCamelCaseContract_RegardlessOfTheHostsNaming()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await PostAsync(client, $$$"""{"steps":[{"stepName":"{{{nameof(BindingStep)}}}","request":{}}]}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.True(document.RootElement.TryGetProperty("processId", out _));
        Assert.False(document.RootElement.TryGetProperty("process_id", out _));
    }

    [Theory]
    [InlineData("""{"steps": [""")]
    [InlineData("""{"steps": "not a list"}""")]
    public async Task AMalformedBody_ReturnsKaleidosStructured400(string body)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await PostAsync(client, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(KaleidoErrorCodes.ArgumentError, document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "binding",
            ServiceVersion = "1",
            Assemblies = [typeof(GenericExecuteBindingFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(BindingStep) || type == typeof(BindingHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer()
            .AddInMemoryProviders();
        var app = builder.Build();
        app.MapProcessor();
        await app.StartAsync(Cancel);
        return app;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string json) =>
        client.PostAsync("/binding/1/processes/execute", new StringContent(json, Encoding.UTF8, "application/json"), Cancel);

    [ProcessStep(DisplayName = "Start", Description = "Starts a process with host-independent JSON")]
    public sealed record BindingStep : IProcessStep;

    public sealed class BindingHandler : IProcessStepHandler<BindingStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(BindingStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
