using Kaleido;
using Kaleido.AspNetCore.Process;
using Kaleido.AspNetCore.Process.Services;
using Kaleido.Exceptions;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProcessorAspNetCore_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() =>
            builder!.AddProcessorAspNetCore());
    }

    [Fact]
    public void AddProcessorAspNetCore_ReturnsSameBuilder()
    {
        var services =
            CreateServices();

        var builder =
            new TestProcessorBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var result =
            builder.AddProcessorAspNetCore();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddProcessorAspNetCore_RegistersRoutingAndNoRouteOptions()
    {
        // RoutePrefix is no longer configurable — routes are derived from
        // KaleidoServiceOptions.ServiceName, which is set by AddKaleido().
        var services =
            CreateServices();

        var builder =
            new TestProcessorBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddProcessorAspNetCore();

        using var provider =
            services.BuildServiceProvider();

        // Routing infrastructure is registered.
        Assert.NotNull(provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    [Fact]
    public void AddProcessorAspNetCore_RegistersRoutingServices()
    {
        var services =
            CreateServices();

        var builder =
            new TestProcessorBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddProcessorAspNetCore();

        using var provider =
            services.BuildServiceProvider();

        Assert.NotNull(
            provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    [Fact]
    public void AddProcessorAspNetCore_RegistersExecutionAndStateServices()
    {
        var services =
            CreateServices();

        var builder =
            new TestProcessorBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddProcessorAspNetCore();

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(IProcessExecutionService) &&
                 x.ImplementationType == typeof(ProcessExecutionService));

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(IProcessStateService) &&
                 x.ImplementationType == typeof(ProcessStateService));
    }

    private static ServiceCollection CreateServices()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton<IProcessorRuntime, FakeProcessorRuntime>();
        services.AddSingleton<IProcessorRegistry, FakeProcessorRegistry>();

        return services;
    }

    private sealed class FakeProcessorRegistry : IProcessorRegistry
    {
        public IReadOnlyCollection<ProcessorRegistryItem> Registrations => Array.Empty<ProcessorRegistryItem>();
    }

    private sealed class TestProcessorBuilder : IKaleidoBuilder
    {
        public TestProcessorBuilder(
            IServiceCollection services,
            IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
        public IConfiguration Configuration { get; } =
            new ConfigurationBuilder().Build();
        public KaleidoServiceOptions ServiceOptions { get; } =
            new() { ServiceName = "test" };
    }

    private sealed class FakeProcessorRuntime : IProcessorRuntime
    {
        public Task<ProcessorProcessResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
