using Kaleido.Exceptions;
using Kaleido.Process.Attributes;
using Kaleido.Process.Execution;
using Kaleido.Process;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Process.UnitTests.Processor;

public sealed class ProcessorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProcessor_RegistersProcessorRegistry()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
            {
                o.ServiceName = "test-processor";
                o.DisplayName = "Test Processor";
                o.Description = "Test processor.";
                o.Assemblies = new[] { typeof(TestStep).Assembly };
            });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IProcessRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.False(registration.IsEntryProcessor);

        var initialStep =
            Assert.Single(registration.InitialSteps);

        Assert.Equal("test-step", initialStep.Name);

        var step =
            Assert.Single(registration.Steps);

        Assert.Equal("test-step", step.Name);
        Assert.NotNull(step.Result);
        Assert.Single(step.Result!.OutputFields);
    }

    [Fact]
    public void AddProcessor_WithIsEntryProcessor_RegistrationReflectsIt()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
            {
                o.ServiceName = "test-processor";
                o.Assemblies = new[] { typeof(TestStep).Assembly };
                o.IsEntryProcessor = true;
            });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IProcessRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.True(registration.IsEntryProcessor);
    }

    [ProcessStep(
        Name = "test-step",
        Description = "Test step",
        Version = "1.0.0",
        DisplayName = "Test Step")]
    public sealed record TestStep;

    public sealed record TestResponse(
        string Value);

    public sealed class TestStepHandler
        : IProcessStepHandler<TestStep, TestResponse>
    {
        public Task<ProcessStepHandlerResult<TestResponse>> ExecuteAsync(
            TestStep step,
            ProcessStepContext context,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
