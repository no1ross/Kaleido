using Kaleido.Exceptions;
using Kaleido.Processor;
using Kaleido.Processor.Registry;
using Kaleido.Registry;

using Kaleido.UnitTests;

namespace Kaleido.Processor.Registry.UnitTests;

public sealed class ProcessorStepRegistryTests
    : SutFixture
{
    [Fact]
    public void Constructor_WhenHandlerTypeMissing_ThrowsConfigurationException()
    {
        var exception =
            Assert.Throws<Kaleido.Exceptions.KaleidoConfigurationException>(() =>
                CreateSut(
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
            CreateSut(
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
            CreateSut(
                typeof(StepA));

        var registration =
            registry.Find(
                nameof(StepA));

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
            CreateSut(
                typeof(StepA));

        var registration =
            registry.Find(
                typeof(StepA));

        Assert.NotNull(
            registration);

        Assert.Equal(
            nameof(StepA),
            registration.Metadata.Name);
    }

    [Fact]
    public void GetRegistration_ByName_WhenMissing_Throws()
    {
        var registry =
            CreateSut(
                typeof(StepA));

        var ex = Assert.Throws<KaleidoFrameworkException>(() =>
            registry.GetRegistration("missing"));
        Assert.Equal(FrameworkErrorCodes.MissingRegistration, ex.Code);
    }

    [Fact]
    public void GetRegistration_ByType_WhenMissing_Throws()
    {
        var registry =
            CreateSut(
                typeof(StepA));

        var ex = Assert.Throws<KaleidoFrameworkException>(() =>
            registry.GetRegistration(typeof(MissingStep)));
        Assert.Equal(FrameworkErrorCodes.MissingRegistration, ex.Code);
    }

    [Fact]
    public void Registration_MapsProcessStepMetadata()
    {
        var registry =
            CreateSut(
                typeof(StepA));

        var registration =
            registry.GetRegistration(
                typeof(StepA));

        Assert.Equal(
            nameof(StepA),
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
            CreateSut(
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
            CreateSut(
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
            CreateSut(
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
            CreateSut(
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
            CreateSut(
                typeof(RepeatableStep));

        var registration =
            registry.GetRegistration(
                typeof(RepeatableStep));

        Assert.True(
            registration.Repeatable.Enabled);
    }

    [Fact]
    public void Registration_MapsKaleidoAuthorization()
    {
        var registry =
            CreateSut(
                typeof(SecuredStep));

        var registration =
            registry.GetRegistration(
                typeof(SecuredStep));

        Assert.Equal(
            "step-policy",
            registration.Metadata.Authorization?.Policy);

        Assert.Equal(
            "internal",
            Assert.Single(registration.Metadata.Authorization!.Roles));
    }

    [Fact]
    public void Registration_WithoutKaleidoAuthorization_HasUnspecifiedAuthorization()
    {
        var registry =
            CreateSut(
                typeof(StepA));

        var registration =
            registry.GetRegistration(
                typeof(StepA));

        Assert.Same(
            AuthorizationMetadata.Unspecified,
            registration.Metadata.Authorization);
    }

    [Fact]
    public void Registration_WithoutAttribute_InheritsServiceAuthorization()
    {
        var rule = new AuthorizationMetadata(null, ["radiology"]);
        var registry = CreateSut(rule, typeof(StepA));

        Assert.Same(rule, registry.GetRegistration(typeof(StepA)).Metadata.Authorization);
    }

    [Fact]
    public void Registration_Attribute_ReplacesServiceAuthorization()
    {
        var rule = new AuthorizationMetadata(null, ["radiology"]);
        var registry = CreateSut(rule, typeof(SecuredStep));

        Assert.Equal("step-policy", registry.GetRegistration(typeof(SecuredStep)).Metadata.Authorization.Policy);
        Assert.DoesNotContain("radiology", registry.GetRegistration(typeof(SecuredStep)).Metadata.Authorization.Roles);
    }

    [Fact]
    public void ToRegistryItem_CarriesAuthorization()
    {
        var registry =
            CreateSut(
                typeof(SecuredStep));

        var registration =
            registry.GetRegistration(
                typeof(SecuredStep));

        var item =
            registration.ToRegistryItem(
                new Mock<ITypeDescriber>().Object,
                new Mock<IConstraintMapper>().Object);

        Assert.Equal(
            "step-policy",
            item.Authorization?.Policy);

        Assert.Equal(
            "step-policy",
            registration.ToSummary().Authorization?.Policy);
    }

    [Fact]
    public void Registration_MapsNonRepeatableStep()
    {
        var registry =
            CreateSut(
                typeof(StepA));

        var registration =
            registry.GetRegistration(
                typeof(StepA));

        Assert.False(
            registration.Repeatable.Enabled);
    }

    private static ProcessorStepRegistry CreateSut(
        IEnumerable<Type> stepTypes,
        IReadOnlyDictionary<Type, Type> handlerTypes) =>
        new(stepTypes, handlerTypes);

    private static ProcessorStepRegistry CreateSut(
        params Type[] stepTypes) =>
        CreateSut(AuthorizationMetadata.Unspecified, stepTypes);

    private static ProcessorStepRegistry CreateSut(
        AuthorizationMetadata defaultAuthorization,
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
            { typeof(StepMultiAvailability), typeof(StepMultiAvailabilityHandler) },
            { typeof(SecuredStep), typeof(SecuredStepHandler) }
        };

        return new ProcessorStepRegistry(
            stepTypes,
            handlerTypes,
            defaultAuthorization);
    }

    [ProcessStep(DisplayName = nameof(StepA), Description = "step-a description", Version = "1.0")]
    private sealed class StepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepB), Description = "step-b description", Version = "1.0")]
    [DependsOn<StepA>]
    private sealed class StepB : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepC), Description = "step-c description", Version = "1.0")]
    private sealed class StepC : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepD), Description = "step-d description", Version = "1.0")]
    private sealed class StepD : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepAfter), Description = "step-after description", Version = "1.0")]
    [AvailableAfter<StepA>]
    private sealed class StepAfter : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepUntil), Description = "step-until description", Version = "1.0")]
    [AvailableUntil<StepA>]
    private sealed class StepUntil : IProcessStep;

    [ProcessStep(DisplayName = nameof(RepeatableStep), Description = "repeatable-step description", Version = "1.0")]
    [Repeatable]
    private sealed class RepeatableStep : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepMultiAvailability), Description = "step-multi description", Version = "1.0")]
    [AvailableAfter<StepA>]
    [AvailableAfter<StepB>]
    [AvailableUntil<StepC>]
    [AvailableUntil<StepD>]
    private sealed class StepMultiAvailability : IProcessStep;

    [ProcessStep(DisplayName = nameof(SecuredStep), Description = "secured-step description", Version = "1.0")]
    [KaleidoAuthorization(Policy = "step-policy", Roles = "internal")]
    private sealed class SecuredStep : IProcessStep;

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

    private sealed class SecuredStepHandler
        : BaseHandler<SecuredStep, TestResponse>;

    private abstract class BaseHandler<TStep, TResponse>
        : IProcessStepHandler<TStep, TResponse>
        where TStep : class, IProcessStep
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