using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Tests;

public sealed class ProcessAspNetCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddParticipantAspNetCore_WhenBuilderIsNull_Throws()
    {
        IParticipantBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() =>
            builder!.AddParticipantAspNetCore());
    }

    [Fact]
    public void AddParticipantAspNetCore_WhenParticipantIsNotRegistered_Throws()
    {
        var builder =
            new TestParticipantBuilder(
                new ServiceCollection(),
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddParticipantAspNetCore());

        Assert.Equal(
            "AddParticipant must be called before AddParticipantAspNetCore.",
            exception.Message);
    }

    [Fact]
    public void AddParticipantAspNetCore_ReturnsSameBuilder()
    {
        var services =
            CreateServices();

        var builder =
            new TestParticipantBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        var result =
            builder.AddParticipantAspNetCore();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddParticipantAspNetCore_RegistersConfiguredRouteOptions()
    {
        var services =
            CreateServices();

        var builder =
            new TestParticipantBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddParticipantAspNetCore(options =>
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
    public void AddParticipantAspNetCore_RegistersRoutingServices()
    {
        var services =
            CreateServices();

        var builder =
            new TestParticipantBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddParticipantAspNetCore();

        using var provider =
            services.BuildServiceProvider();

        Assert.NotNull(
            provider.GetService<IConfigureOptions<RouteOptions>>());
    }

    [Fact]
    public void AddParticipantAspNetCore_RegistersExecutionAndStateServices()
    {
        var services =
            CreateServices();

        var builder =
            new TestParticipantBuilder(
                services,
                [typeof(ProcessAspNetCoreServiceCollectionExtensionsTests).Assembly]);

        builder.AddParticipantAspNetCore();

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

        services.AddSingleton<IParticipantRuntime, FakeParticipantRuntime>();

        return services;
    }

    private sealed class TestParticipantBuilder : IParticipantBuilder
    {
        public TestParticipantBuilder(
            IServiceCollection services,
            IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }

        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }

    private sealed class FakeParticipantRuntime : IParticipantRuntime
    {
        public Task<ParticipantProcessResult> ExecuteAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
