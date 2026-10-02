using System.Reflection;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    // ── AddProcessorClient — guard clauses ────────────────────────────────────────

    [Fact]
    public void AddProcessClient_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddProcessorClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenConfigureIsNull_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddProcessorClient(null!));
    }

    [Fact]
    public void AddProcessClient_WhenNameIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessorClient(o =>
            {
                o.Name = "";
                o.BaseUrl = "http://localhost";
            }));
    }

    [Fact]
    public void AddProcessClient_WhenBaseUrlIsNullOrWhiteSpace_Throws()
    {
        var builder = new FakeKaleidoBuilder(new ServiceCollection());

        Assert.Throws<ArgumentException>(
            () => builder.AddProcessorClient(o =>
            {
                o.Name = "name";
                o.BaseUrl = "";
            }));
    }

    // ── AddProcessorClient — factory registration ──────────────────────────────────

    [Fact]
    public void AddProcessClient_RegistersFactory()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        Assert.Contains(services,
            d => d.ServiceType == typeof(IKaleidoProcessorClientFactory));
    }

    [Fact]
    public void AddProcessClient_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        var result = builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        Assert.Same(builder, result);
    }

    // ── AddProcessorClient — RouteOptionsMap ───────────────────────────────────────

    [Fact]
    public void AddProcessClient_WithRoutePrefix_StoresOptionsInMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "Radiology";
            o.BaseUrl = "http://localhost";
            o.RoutePrefix = "radiology";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("Radiology", out var prefix));
        Assert.Equal("radiology", prefix);
    }

    [Fact]
    public void AddProcessClient_MultipleCalls_AccumulateInSingleMap()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder
            .AddProcessorClient(o => { o.Name = "ProcessorA"; o.BaseUrl = "http://a.localhost"; })
            .AddProcessorClient(o => { o.Name = "ProcessorB"; o.BaseUrl = "http://b.localhost"; o.RoutePrefix = "prefix"; });

        var maps = services
            .Where(d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap))
            .ToList();

        Assert.Single(maps); // only one singleton

        var map = (KaleidoProcessorClientRouteOptionsMap)maps[0].ImplementationInstance!;
        Assert.True(map.Options.ContainsKey("ProcessorA"));
        Assert.True(map.Options.ContainsKey("ProcessorB"));
    }

    [Fact]
    public void AddProcessClient_WithoutRoutePrefix_StoresEmptyPrefix()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(o =>
        {
            o.Name = "RemoteProcessor";
            o.BaseUrl = "http://localhost";
        });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var prefix));
        Assert.Equal("", prefix);
    }

    [Fact]
    public void AddProcessClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);

        builder.AddProcessorClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
                o.RoutePrefix = "kaleido";
            });

        var descriptor = services.First(
            d => d.ServiceType == typeof(KaleidoProcessorClientRouteOptionsMap));
        var map = (KaleidoProcessorClientRouteOptionsMap)descriptor.ImplementationInstance!;

        Assert.True(map.Options.TryGetValue("RemoteProcessor", out var stored));
        Assert.Equal("kaleido", stored);
    }

    [Fact]
    public void AddProcessClient_WithConfigureClient_InvokesCallbackWithHttpClientBuilder()
    {
        var services = new ServiceCollection();
        var builder = new FakeKaleidoBuilder(services);
        var callbackInvoked = false;

        builder.AddProcessorClient(
            o =>
            {
                o.Name = "RemoteProcessor";
                o.BaseUrl = "http://localhost";
            },
            http =>
            {
                Assert.NotNull(http);
                callbackInvoked = true;
            });

        Assert.True(callbackInvoked);
    }

    // ── AddQueryableClient — guard clauses ───────────────────────────────────────

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

    // ── AddQueryableClient — factory registration ────────────────────────────────

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

    // ── AddQueryableClient — RouteOptionsMap ─────────────────────────────────────

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

        Assert.True(map.Options.TryGetValue("Radiology", out var prefix));
        Assert.Equal("radiology", prefix);
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

        Assert.True(map.Options.TryGetValue("MemberService", out var prefix));
        Assert.Equal("", prefix);
    }

    [Fact]
    public void AddQueryableClient_WithConfigureClient_ConfigureIsInvokedFirst()
    {
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
        Assert.Equal("kaleido", stored);
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

    // ── Test doubles ─────────────────────────────────────────────────────────────

    private sealed class FakeKaleidoBuilder(IServiceCollection services) : IKaleidoBuilder
    {
        public IServiceCollection Services { get; } = services;
        public IReadOnlyCollection<Assembly> Assemblies { get; } = [typeof(FakeKaleidoBuilder).Assembly];
        public IConfiguration Configuration { get; } =
            new ConfigurationBuilder().Build();
        public KaleidoServiceOptions ServiceOptions { get; } =
            new() { ServiceName = "test" };
    }
}
