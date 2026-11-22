using System.Net;
using System.Text;
using Kaleido.Eventing;
using Kaleido.Http.Server;
using Kaleido.Queryable.Http.Server.FunctionalTests.Contract;
using Kaleido.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.Server.FunctionalTests.HostExceptions;

public sealed class HostExceptionBoundaryFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidationError_UsesKaleidos400UnderEitherHostExceptionHandler(bool developerPage)
    {
        await using var app = await StartAsync(developerPage);
        using var client = app.GetTestClient();
        using var content = new StringContent("""{"query":{"page":{"size":50,"offset":0}}}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/catalog/1/queryable/unpagedcontractsource/query", content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("qry_paging_not_supported", body, StringComparison.Ordinal);
        Assert.DoesNotContain("host error page", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedParameters_WithADeveloperExceptionPage_KeepKaleidosErrorAndJsonPath()
    {
        await using var app = await StartAsync(developerPage: true);
        using var client = app.GetTestClient();
        using var content = new StringContent("""{"parameters":{"category":42}}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/catalog/1/queryable/contractsource/gridview/query", content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("argument_error", body, StringComparison.Ordinal);
        Assert.Contains("$.parameters.category", body, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> StartAsync(bool developerPage)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(store => store.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddSingleton<ContractRecords>();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(HostExceptionBoundaryFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(ContractSource) || type == typeof(UnpagedContractSource) || type == typeof(GridView),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp();
        var app = builder.Build();
        if (developerPage)
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler(handler => handler.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return context.Response.WriteAsync("host error page");
            }));
        }

        app.MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }
}
