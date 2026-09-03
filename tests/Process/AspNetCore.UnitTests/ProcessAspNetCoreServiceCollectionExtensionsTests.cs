using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProcessorAspNetCore_WhenBuilderIsNull_Throws()
    {
        IProcessorBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() =>
            builder!.AddProcessorAspNetCore());
    }

    [Fact]
    public void AddProcessorAspNetCore_WhenProcessorIsNotRegistered_Throws()
    {
        var builder =
            new TestProcessorBuilder(
                new ServiceCollection(),
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddProcessorAspNetCore());

        Assert.Equal(
            "AddProcessor must be called before AddProcessorAspNetCore.",
            exception.Message);
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
    public void AddProcessorAspNetCore_RegistersConfiguredRouteOptions()
    {
        var services =
            CreateServices();

        var builder =
            new TestProcessorBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddProcessorAspNetCore(options =>
        {
            options.RoutePrefix = "/custom/processes";
        });

        using var provider =
            services.BuildServiceProvider();

        var options =
            provider.GetRequiredService<ProcessRouteOptions>();

        Assert.Equal(
            "/custom/processes",
            options.RoutePrefix);
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

        return services;
    }

    private sealed class TestProcessorBuilder : IProcessorBuilder
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
