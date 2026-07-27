using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Kaleido.Queryable.AspNetCore.Tests;

public sealed class QueryableAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQueryableAspNetCore_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddQueryableAspNetCore());
    }

    [Fact]
    public void AddQueryableAspNetCore_ShouldThrow_WhenQueryableNotRegistered()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => builder.AddQueryableAspNetCore());

        Assert.Equal(
            "AddQueryable must be called before AddQueryableAspNetCore.",
            exception.Message);
    }

    [Fact]
    public void AddQueryableAspNetCore_ShouldReturn_SameBuilder()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton(
            Mock.Of<IQueryableService>());

        var builder =
            services.AddKaleido();

        var result =
            builder.AddQueryableAspNetCore();

        Assert.Same(
            builder,
            result);
    }

    [Fact]
    public void AddQueryableAspNetCore_ShouldRegister_ConfigurationDelegate()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton(
            Mock.Of<IQueryableService>());

        var builder =
            services.AddKaleido();

        builder.AddQueryableAspNetCore(options =>
        {
            options.RoutePrefix = "/custom";
            options.QueryRoute = "execute";
            options.MetadataRoute = "schema";
            options.QueriesRoute = "named-queries";
        });

        using var provider =
            services.BuildServiceProvider();

        var options =
            provider
                .GetRequiredService<IOptions<QueryableAspNetCoreOptions>>();

        Assert.Equal(
            "/custom",
            options.Value.RoutePrefix);

        Assert.Equal(
            "execute",
            options.Value.QueryRoute);

        Assert.Equal(
            "schema",
            options.Value.MetadataRoute);

        Assert.Equal(
            "named-queries",
            options.Value.QueriesRoute);
    }

    [Fact]
    public void AddQueryableAspNetCore_Should_Register_Routing_Services()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton(
            Mock.Of<IQueryableService>());

        var builder =
            services.AddKaleido();

        builder.AddQueryableAspNetCore();

        using var provider =
            services.BuildServiceProvider();

        var routing =
            provider.GetService<IConfigureOptions<RouteOptions>>();

        Assert.NotNull(routing);
    }
}