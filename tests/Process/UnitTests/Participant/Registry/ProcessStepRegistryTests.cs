using Kaleido.Process.Attributes;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant.Registry;

public sealed class ProcessStepRegistryTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessStepRegistry(
                    null!,
                    [typeof(StepA)]));

        Assert.Equal(
            "services",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStepTypesIsNull_Throws()
    {
        var services =
            CreateServices();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessStepRegistry(
                    services,
                    null!));

        Assert.Equal(
            "stepTypes",
            exception.ParamName);
    }

    [Fact]
    public void Registrations_ReturnsAllRegistrations()
    {
        var registry =
            CreateRegistry(
                typeof(StepA),
                typeof(StepB),
                typeof(StepC));

        Assert.Equal(
            3,
            registry.Registrations.Count);
    }

    [Fact]
    public void Find_ByName_ReturnsRegistration()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        var registration =
            registry.Find(
                "step-a");

        Assert.NotNull(
            registration);

        Assert.Equal(
            typeof(StepA),
            registration.StepType);
    }

    [Fact]
    public void Find_ByType_ReturnsRegistration()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        var registration =
            registry.Find(
                typeof(StepA));

        Assert.NotNull(
            registration);

        Assert.Equal(
            "step-a",
            registration.Metadata.Name);
    }

    [Fact]
    public void GetRegistration_ByName_WhenMissing_Throws()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        Assert.Throws<KeyNotFoundException>(() =>
            registry.GetRegistration(
                "missing"));
    }

    [Fact]
    public void GetRegistration_ByType_WhenMissing_Throws()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        Assert.Throws<KeyNotFoundException>(() =>
            registry.GetRegistration(
                typeof(MissingStep)));
    }

    [Fact]
    public void Registration_MapsProcessStepMetadata()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        var registration =
            registry.GetRegistration(
                typeof(StepA));

        Assert.Equal(
            "step-a",
            registration.Metadata.Name);

        Assert.Equal(
            "step-a description",
            registration.Metadata.Description);

        Assert.Equal(
            "1.0",
            registration.Metadata.Version);
    }

    [Fact]
    public void Registration_MapsDependencies()
    {
        var registry =
            CreateRegistry(
                typeof(StepA),
                typeof(StepB));

        var registration =
            registry.GetRegistration(
                typeof(StepB));

        var dependency =
            Assert.Single(
                registration.Dependencies);

        Assert.Equal(
            typeof(StepA),
            dependency.StepType);
    }

    [Fact]
    public void Registration_MapsAvailableAfter()
    {
        var registry =
            CreateRegistry(
                typeof(StepA),
                typeof(StepAfter));

        var registration =
            registry.GetRegistration(
                typeof(StepAfter));

        var availableAfter =
            Assert.Single(
                registration.AvailableAfter);

        Assert.Equal(
            typeof(StepA),
            availableAfter.StepType);
    }

    [Fact]
    public void Registration_MapsAvailableUntil()
    {
        var registry =
            CreateRegistry(
                typeof(StepA),
                typeof(StepUntil));

        var registration =
            registry.GetRegistration(
                typeof(StepUntil));

        var availableUntil =
            Assert.Single(
                registration.AvailableUntil);

        Assert.Equal(
            typeof(StepA),
            availableUntil.StepType);
    }

    [Fact]
    public void Registration_MapsMultipleAvailabilityRules()
    {
        var registry =
            CreateRegistry(
                typeof(StepA),
                typeof(StepB),
                typeof(StepC),
                typeof(StepD),
                typeof(StepMultiAvailability));

        var registration =
            registry.GetRegistration(
                typeof(StepMultiAvailability));

        Assert.Equal(
            2,
            registration.AvailableAfter.Count);

        Assert.Equal(
            2,
            registration.AvailableUntil.Count);

        Assert.Contains(
            registration.AvailableAfter,
            x => x.StepType == typeof(StepA));

        Assert.Contains(
            registration.AvailableAfter,
            x => x.StepType == typeof(StepB));

        Assert.Contains(
            registration.AvailableUntil,
            x => x.StepType == typeof(StepC));

        Assert.Contains(
            registration.AvailableUntil,
            x => x.StepType == typeof(StepD));
    }

    private static ProcessStepRegistry CreateRegistry(
        params Type[] stepTypes)
    {
        return new ProcessStepRegistry(
            CreateServices(),
            stepTypes);
    }

    private static IServiceCollection CreateServices()
    {
        var services =
            new ServiceCollection();

        services.AddTransient<
            IProcessStepHandler<StepA, TestResponse>,
            StepAHandler>();

        services.AddTransient<
            IProcessStepHandler<StepB, TestResponse>,
            StepBHandler>();

        services.AddTransient<
            IProcessStepHandler<StepC, TestResponse>,
            StepCHandler>();

        services.AddTransient<
            IProcessStepHandler<StepD, TestResponse>,
            StepDHandler>();

        services.AddTransient<
            IProcessStepHandler<StepAfter, TestResponse>,
            StepAfterHandler>();

        services.AddTransient<
            IProcessStepHandler<StepUntil, TestResponse>,
            StepUntilHandler>();

        services.AddTransient<
            IProcessStepHandler<StepMultiAvailability, TestResponse>,
            StepMultiAvailabilityHandler>();

        return services;
    }

    [ProcessStep("step-a", "step-a description", "1.0")]
    private sealed class StepA;

    [ProcessStep("step-b", "step-b description", "1.0")]
    [DependsOnStep(typeof(StepA))]
    private sealed class StepB;

    [ProcessStep("step-c", "step-c description", "1.0")]
    private sealed class StepC;

    [ProcessStep("step-d", "step-d description", "1.0")]
    private sealed class StepD;

    [ProcessStep("step-after", "step-after description", "1.0")]
    [AvailableAfter(typeof(StepA))]
    private sealed class StepAfter;

    [ProcessStep("step-until", "step-until description", "1.0")]
    [AvailableUntil(typeof(StepA))]
    private sealed class StepUntil;

    [ProcessStep("step-multi", "step-multi description", "1.0")]
    [AvailableAfter(typeof(StepA))]
    [AvailableAfter(typeof(StepB))]
    [AvailableUntil(typeof(StepC))]
    [AvailableUntil(typeof(StepD))]
    private sealed class StepMultiAvailability;

    private sealed class MissingStep;

    private sealed record TestResponse;

    private sealed class StepAHandler
        : BaseHandler<StepA, TestResponse>;

    private sealed class StepBHandler
        : BaseHandler<StepB, TestResponse>;

    private sealed class StepCHandler
        : BaseHandler<StepC, TestResponse>;

    private sealed class StepDHandler
        : BaseHandler<StepD, TestResponse>;

    private sealed class StepAfterHandler
        : BaseHandler<StepAfter, TestResponse>;

    private sealed class StepUntilHandler
        : BaseHandler<StepUntil, TestResponse>;

    private sealed class StepMultiAvailabilityHandler
        : BaseHandler<StepMultiAvailability, TestResponse>;

    private abstract class BaseHandler<TStep, TResponse>
        : IProcessStepHandler<TStep, TResponse>
        where TStep : class
    {
        public Task<ProcessStepHandlerResult<TResponse>> ExecuteAsync(
            TStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}