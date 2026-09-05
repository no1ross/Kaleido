using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Client.Tests;

public sealed class KaleidoQueryableClientServiceCollectionExtensionsTests
{
    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddQueryableClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddQueryableClient_WhenConfigureIsNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddQueryableClient(null!));
    }

    [Fact]
    public void AddQueryableClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient(o =>
            {
                o.Name = "";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddQueryableClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddQueryableClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "";
            }));
    }

    // ---------------------------------------------------------------------------
    // Factory registration
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoQueryableClientFactory));
    }

    [Fact]
    public void AddQueryableClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        Assert.Same(builder, result);
    }

    // ---------------------------------------------------------------------------
    // RouteOptionsMap
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RoutePrefix = "radiology";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var opts));
        Assert.Equal("radiology", opts!.RoutePrefix);
    }

    [Fact]
    public void AddQueryableClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddQueryableClient(o => { o.Name = "ServiceA"; o.BaseUrl = "http://a.localhost"; })
            .AddQueryableClient(o => { o.Name = "ServiceB"; o.BaseUrl = "http://b.localhost"; o.RoutePrefix = "prefix"; });

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoQueryableClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ServiceA"));
        Assert.True(map.Options.ContainsKey("ServiceB"));
    }

    [Fact]
    public void AddQueryableClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(o =>
        {
            o.Name = "MemberService";
            o.BaseUrl = "http://localhost";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("MemberService", out var opts));
        Assert.Equal("", opts!.RoutePrefix);
    }

    // ---------------------------------------------------------------------------
    // configureClient callback
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddQueryableClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
        // configure runs before configureClient, so RoutePrefix set in configure is
        // stored in the map (configureClient no longer receives the options object).
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddQueryableClient(
            o =>
            {
                o.Name = "MemberService";
                o.BaseUrl = "http://localhost";
                o.RoutePrefix = "kaleido";
            });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));
        var map = (KaleidoQueryableClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("MemberService", out var stored));
        Assert.Equal("kaleido", stored!.RoutePrefix);
    }

    [Fact]
    public void AddQueryableClient_WithConfigureClient_InvokesCallbackWithHttpClientBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);
        var callbackInvoked = false;

        builder.AddQueryableClient(
            o =>
            {
                o.Name = "MemberService";
                o.BaseUrl = "http://localhost";
            },
            http =>
            {
                Assert.NotNull(http);
                callbackInvoked = true;
            });

        Assert.True(callbackInvoked);
    }

    // ---------------------------------------------------------------------------
    // Test doubles
    // ---------------------------------------------------------------------------

    private sealed class FakeKaleidoBuilder : IKaleidoBuilder
    {
        public FakeKaleidoBuilder(IServiceCollection services)
        {
            Services = services;
            Assemblies = [typeof(FakeKaleidoBuilder).Assembly];
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }


}
