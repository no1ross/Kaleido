using Kaleido.Exceptions;
using Kaleido.Process.Attributes;
using Kaleido.Process.Execution;
using Kaleido.Process.Registry;

namespace Kaleido.Process.UnitTests.Processor.Registry;

public sealed class ProcessStepRegistryTests
{
    [Fact]
    public void Constructor_WhenHandlerTypeMissing_ThrowsConfigurationException()
    {
        var exception =
            Assert.Throws<Kaleido.Exceptions.KaleidoConfigurationException>(() =>
                new ProcessStepRegistry(
                    new[] { typeof(StepA) },
                    new Dictionary<Type, Type>()));

        Assert.Contains(
            "No handler type registered for step",
            exception.Message);
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

        var ex = Assert.Throws<KaleidoFrameworkException>(() =>
            registry.GetRegistration("missing"));
        Assert.Equal(FrameworkErrorCodes.MissingRegistration, ex.Code);
    }

    [Fact]
    public void GetRegistration_ByType_WhenMissing_Throws()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        var ex = Assert.Throws<KaleidoFrameworkException>(() =>
            registry.GetRegistration(typeof(MissingStep)));
        Assert.Equal(FrameworkErrorCodes.MissingRegistration, ex.Code);
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

    [Fact]
    public void Registration_MapsRepeatableAttribute()
    {
        var registry =
            CreateRegistry(
                typeof(RepeatableStep));

        var registration =
            registry.GetRegistration(
                typeof(RepeatableStep));

        Assert.True(
            registration.Repeatable.Enabled);
    }

    [Fact]
    public void Registration_MapsNonRepeatableStep()
    {
        var registry =
            CreateRegistry(
                typeof(StepA));

        var registration =
            registry.GetRegistration(
                typeof(StepA));

        Assert.False(
            registration.Repeatable.Enabled);
    }

    private static ProcessStepRegistry CreateRegistry(
        params Type[] stepTypes)
    {
        var handlerTypes = new Dictionary<Type, Type>
        {
            { typeof(StepA), typeof(StepAHandler) },
            { typeof(StepB), typeof(StepBHandler) },
            { typeof(StepC), typeof(StepCHandler) },
            { typeof(StepD), typeof(StepDHandler) },
            { typeof(RepeatableStep), typeof(RepeatableStepHandler) },
            { typeof(StepAfter), typeof(StepAfterHandler) },
            { typeof(StepUntil), typeof(StepUntilHandler) },
            { typeof(StepMultiAvailability), typeof(StepMultiAvailabilityHandler) }
        };

        return new ProcessStepRegistry(
            stepTypes,
            handlerTypes);
    }

    [ProcessStep(Name = "step-a", Description = "step-a description", Version = "1.0")]
    private sealed class StepA;

    [ProcessStep(Name = "step-b", Description = "step-b description", Version = "1.0")]
    [DependsOnStep(typeof(StepA))]
    private sealed class StepB;

    [ProcessStep(Name = "step-c", Description = "step-c description", Version = "1.0")]
    private sealed class StepC;

    [ProcessStep(Name = "step-d", Description = "step-d description", Version = "1.0")]
    private sealed class StepD;

    [ProcessStep(Name = "step-after", Description = "step-after description", Version = "1.0")]
    [AvailableAfter(typeof(StepA))]
    private sealed class StepAfter;

    [ProcessStep(Name = "step-until", Description = "step-until description", Version = "1.0")]
    [AvailableUntil(typeof(StepA))]
    private sealed class StepUntil;

    [ProcessStep(Name = "repeatable-step", Description = "repeatable-step description", Version = "1.0")]
    [Repeatable]
    private sealed class RepeatableStep;

    [ProcessStep(Name = "step-multi", Description = "step-multi description", Version = "1.0")]
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

    private sealed class RepeatableStepHandler
    : BaseHandler<RepeatableStep, TestResponse>;

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