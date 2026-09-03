using Kaleido.Process.Attributes;
using Kaleido.Process.Execution;
using Kaleido.Process;
using Kaleido.Process.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.UnitTests.Processor;

public sealed class ProcessorServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProcessor_WhenNameIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddProcessor(options =>
                {
                    options.Version = "1.0.0";
                    options.DisplayName = "Test Processor";
                }));

        Assert.Equal(
            "Processor must specify a non-empty name.",
            exception.Message);
    }

    [Fact]
    public void AddProcessor_WhenVersionIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddProcessor(options =>
                {
                    options.Name = "test-processor";
                    options.DisplayName = "Test Processor";
                }));

        Assert.Equal(
            "Processor must specify a non-empty version.",
            exception.Message);
    }

    [Fact]
    public void AddProcessor_WhenDisplayNameIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddProcessor(options =>
                {
                    options.Name = "test-processor";
                    options.Version = "1.0.0";
                }));

        Assert.Equal(
            "Processor must specify a non-empty display name.",
            exception.Message);
    }

    [Fact]
    public void AddProcessor_RegistersProcessorRegistry()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        builder.AddProcessor(options =>
        {
            options.Name = "test-processor";
            options.Description = "Test processor.";
            options.Version = "1.0.0";
            options.DisplayName = "Test Processor";
        });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IProcessorRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.Equal("test-processor", registration.Name);
        Assert.Equal("Test processor.", registration.Description);
        Assert.Equal("1.0.0", registration.Version);
        Assert.Equal("Test Processor", registration.DisplayName);

        var initialStep =
            Assert.Single(registration.InitialSteps);

        Assert.Equal("test-step", initialStep.Name);

        var step =
            Assert.Single(registration.Steps);

        Assert.Equal("test-step", step.Name);
        Assert.NotNull(step.Result);
        Assert.Single(step.Result!.OutputFields);
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
