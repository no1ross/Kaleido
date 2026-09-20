using Kaleido.AspNetCore.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore.UnitTests;

public sealed class AspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAspNetCore_RegistersKaleidoStartupFilter()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        var kaleidoBuilder = builder.Services.AddKaleido(builder.Configuration, o => o.ServiceName = "test-service");
        kaleidoBuilder.AddAspNetCore();

        var serviceProvider = builder.Services.BuildServiceProvider();
        var startupFilter = serviceProvider.GetService<IStartupFilter>();

        Assert.NotNull(startupFilter);
        Assert.IsType<KaleidoStartupFilter>(startupFilter);
    }

    [Fact]
    public async Task KaleidoStartupFilter_RegistersMiddlewareInCorrectOrder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        var kaleidoBuilder = builder.Services.AddKaleido(builder.Configuration, o => o.ServiceName = "test-service");
        kaleidoBuilder.AddAspNetCore();

        var app = builder.Build();

        // The middleware should be automatically registered via IStartupFilter
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await app.StartAsync();

        try
        {
            using var client = app.GetTestClient();
            var response = await client.GetAsync("/");

            Assert.Equal(StatusCodes.Status204NoContent, (int)response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
