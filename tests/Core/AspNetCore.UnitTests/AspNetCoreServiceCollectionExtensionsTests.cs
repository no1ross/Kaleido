using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore.UnitTests;

public sealed class AspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public async Task UseKaleidoExceptionHandling_WhenAddedOnce_RegistersMiddlewarePipeline()
    {
        var app = CreateApplication();

        app.UseKaleidoExceptionHandling();
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

    [Fact]
    public async Task UseKaleidoExceptionHandling_WhenAddedTwice_DoesNotBreakPipeline()
    {
        var app = CreateApplication();

        app.UseKaleidoExceptionHandling();
        app.UseKaleidoExceptionHandling();
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

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        return builder.Build();
    }
}
