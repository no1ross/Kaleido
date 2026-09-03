using Kaleido.Process.Attributes;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.UnitTests.Participant;

public sealed class ParticipantServiceCollectionExtensionsTests
{
    [Fact]
    public void AddParticipant_WhenNameIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddParticipant(options =>
                {
                    options.Version = "1.0.0";
                    options.DisplayName = "Test Participant";
                }));

        Assert.Equal(
            "Participant must specify a non-empty name.",
            exception.Message);
    }

    [Fact]
    public void AddParticipant_WhenVersionIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddParticipant(options =>
                {
                    options.Name = "test-participant";
                    options.DisplayName = "Test Participant";
                }));

        Assert.Equal(
            "Participant must specify a non-empty version.",
            exception.Message);
    }

    [Fact]
    public void AddParticipant_WhenDisplayNameIsMissing_Throws()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                builder.AddParticipant(options =>
                {
                    options.Name = "test-participant";
                    options.Version = "1.0.0";
                }));

        Assert.Equal(
            "Participant must specify a non-empty display name.",
            exception.Message);
    }

    [Fact]
    public void AddParticipant_RegistersParticipantRegistry()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido()
                .AddAssembly(typeof(TestStep).Assembly);

        builder.AddParticipant(options =>
        {
            options.Name = "test-participant";
            options.Description = "Test participant.";
            options.Version = "1.0.0";
            options.DisplayName = "Test Participant";
        });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IParticipantRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.Equal("test-participant", registration.Name);
        Assert.Equal("Test participant.", registration.Description);
        Assert.Equal("1.0.0", registration.Version);
        Assert.Equal("Test Participant", registration.DisplayName);

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
