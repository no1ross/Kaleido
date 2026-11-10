using Kaleido.Exceptions;
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

    [Fact]
    public void InitialRegistrations_ExcludeInformationSteps()
    {
        var registry =
            CreateSut(
                typeof(StepA),
                typeof(InformationStep));

        Assert.Equal(
            [nameof(StepA)],
            registry.InitialRegistrations.Select(x => x.Metadata.Name));
    }

    // -- startup validation of relationships --------------------------------

    [Fact]
    public void Constructor_WhenARelationshipNamesAStepThatIsNotRegistered_ThrowsInvalidRegistration()
    {
        // StepB depends on StepA, which this processor does not register.
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(typeof(StepB)));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
        Assert.Contains(nameof(StepA), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenAStepDependsOnItself_ThrowsInvalidRegistration()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(typeof(SelfDependentStep)));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
    }

    [Fact]
    public void Constructor_WhenDependenciesAreCircular_ThrowsInvalidRegistration()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(typeof(CycleStepA), typeof(CycleStepB)));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
    }

    // AvailableAfter needs every named step completed, like DependsOn: a cycle through it - alone or
    // mixed with DependsOn - can never become available, so it fails at startup too (#522).
    [Theory]
    [InlineData(typeof(AfterCycleStepA), typeof(AfterCycleStepB))]
    [InlineData(typeof(MixedCycleStepA), typeof(MixedCycleStepB))]
    public void Constructor_WhenAvailableAfterClosesACycle_ThrowsInvalidRegistrationNamingTheCycle(Type first, Type second)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(first, second));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
        Assert.Contains(first.Name, exception.Message);
        Assert.Contains(second.Name, exception.Message);
    }

    // AvailableUntil only closes a step, so two steps closing each other are a valid choice.
    [Fact]
    public void Constructor_WhenStepsCloseEachOtherWithAvailableUntil_Registers()
    {
        var sut = CreateSut(typeof(UntilLoopStepA), typeof(UntilLoopStepB));

        Assert.Equal(2, sut.Registrations.Count);
    }

    [Fact]
    public void Registration_ByDefault_DeclaresAProcessOnlyStep()
    {
        var metadata = CreateSut(typeof(StepA)).GetRegistration(typeof(StepA)).Metadata;

        Assert.False(metadata.Destructive);
        Assert.False(metadata.External);
        Assert.False(metadata.RequiresConfirmation);
    }

    [Fact]
    public void Registration_MapsTheDeclaredBehavior_IntoTheMetadata()
    {
        var registration = CreateSut(typeof(DeclaredStep)).GetRegistration(typeof(DeclaredStep));

        Assert.True(registration.Metadata.Destructive);
        Assert.True(registration.Metadata.External);
        Assert.True(registration.Metadata.RequiresConfirmation);
    }

    [Theory]
    [InlineData(typeof(SelfAvailableAfterStep), "AvailableAfter")]
    [InlineData(typeof(SelfAvailableUntilStep), "AvailableUntil")]
    public void Constructor_WhenAStepReferencesItselfInAnAvailabilityRule_ThrowsInvalidRegistration(Type stepType, string rule)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(stepType));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
        Assert.Contains($"cannot reference itself in {rule}", exception.Message);
    }

    [Fact]
    public void Constructor_ASharedDependency_IsNotACycle()
    {
        var registry = CreateSut(typeof(StepA), typeof(StepB), typeof(DiamondStep));

        Assert.Equal(
            [typeof(StepA), typeof(StepB)],
            registry.GetRegistration(typeof(DiamondStep)).Dependencies.Select(x => x.StepType).OrderBy(x => x.Name));
    }

    [Fact]
    public void GetRegistration_ByName_WhenRegistered_ReturnsItCaseInsensitively()
    {
        var registry = CreateSut([typeof(StepA)], new Dictionary<Type, Type> { [typeof(StepA)] = typeof(StepAHandler) });

        Assert.Equal(typeof(StepA), registry.GetRegistration(nameof(StepA).ToUpperInvariant()).StepType);
        Assert.Equal(AuthorizationMetadata.Unspecified, registry.GetRegistration(typeof(StepA)).Metadata.Authorization);
    }

    [Fact]
    public void Registration_ResultExtractor_ReturnsTheHandlerResult_AndRejectsANullResult()
    {
        var getResult = CreateSut(typeof(StepA)).GetRegistration(typeof(StepA)).GetResultFromTask!;
        var handlerResult = new ProcessStepHandlerResult<TestResponse> { Succeeded = true };

        Assert.Same(handlerResult, getResult(Task.FromResult(handlerResult)));

        var exception =
            Assert.Throws<KaleidoFrameworkException>(() =>
                getResult(Task.FromResult<ProcessStepHandlerResult<TestResponse>>(null!)));

        Assert.Equal(FrameworkErrorCodes.InvalidHandlerResult, exception.Code);
    }

    [ProcessStep(DisplayName = nameof(SelfAvailableAfterStep), Description = "self available-after description")]
    [AvailableAfter<SelfAvailableAfterStep>]
    private sealed class SelfAvailableAfterStep : IProcessStep;

    private sealed class SelfAvailableAfterStepHandler : BaseHandler<SelfAvailableAfterStep, TestResponse>;

    [ProcessStep(DisplayName = nameof(SelfAvailableUntilStep), Description = "self available-until description")]
    [AvailableUntil<SelfAvailableUntilStep>]
    private sealed class SelfAvailableUntilStep : IProcessStep;

    private sealed class SelfAvailableUntilStepHandler : BaseHandler<SelfAvailableUntilStep, TestResponse>;

    [ProcessStep(DisplayName = nameof(DiamondStep), Description = "diamond description")]
    [DependsOn<StepA>]
    [DependsOn<StepB>]
    private sealed class DiamondStep : IProcessStep;

    private sealed class DiamondStepHandler : BaseHandler<DiamondStep, TestResponse>;

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
            { typeof(SecuredStep), typeof(SecuredStepHandler) },
            { typeof(InformationStep), typeof(InformationStepHandler) },
            { typeof(SelfDependentStep), typeof(SelfDependentStepHandler) },
            { typeof(CycleStepA), typeof(CycleStepAHandler) },
            { typeof(CycleStepB), typeof(CycleStepBHandler) },
            { typeof(AfterCycleStepA), typeof(AfterCycleStepAHandler) },
            { typeof(AfterCycleStepB), typeof(AfterCycleStepBHandler) },
            { typeof(MixedCycleStepA), typeof(MixedCycleStepAHandler) },
            { typeof(MixedCycleStepB), typeof(MixedCycleStepBHandler) },
            { typeof(UntilLoopStepA), typeof(UntilLoopStepAHandler) },
            { typeof(UntilLoopStepB), typeof(UntilLoopStepBHandler) },
            { typeof(DeclaredStep), typeof(DeclaredStepHandler) },
            { typeof(SelfAvailableAfterStep), typeof(SelfAvailableAfterStepHandler) },
            { typeof(SelfAvailableUntilStep), typeof(SelfAvailableUntilStepHandler) },
            { typeof(DiamondStep), typeof(DiamondStepHandler) }
        };

        return new ProcessorStepRegistry(
            stepTypes,
            handlerTypes,
            defaultAuthorization);
    }

    [ProcessStep(DisplayName = nameof(StepA), Description = "step-a description")]
    private sealed class StepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepB), Description = "step-b description")]
    [DependsOn<StepA>]
    private sealed class StepB : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepC), Description = "step-c description")]
    private sealed class StepC : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepD), Description = "step-d description")]
    private sealed class StepD : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepAfter), Description = "step-after description")]
    [AvailableAfter<StepA>]
    private sealed class StepAfter : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepUntil), Description = "step-until description")]
    [AvailableUntil<StepA>]
    private sealed class StepUntil : IProcessStep;

    [ProcessStep(DisplayName = nameof(RepeatableStep), Description = "repeatable-step description")]
    [Repeatable]
    private sealed class RepeatableStep : IProcessStep;

    [ProcessStep(DisplayName = nameof(StepMultiAvailability), Description = "step-multi description")]
    [AvailableAfter<StepA>]
    [AvailableAfter<StepB>]
    [AvailableUntil<StepC>]
    [AvailableUntil<StepD>]
    private sealed class StepMultiAvailability : IProcessStep;

    [ProcessStep(DisplayName = nameof(SelfDependentStep), Description = "self-dependent description")]
    [DependsOn<SelfDependentStep>]
    private sealed class SelfDependentStep : IProcessStep;

    [ProcessStep(DisplayName = nameof(CycleStepA), Description = "cycle-a description")]
    [DependsOn<CycleStepB>]
    private sealed class CycleStepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(CycleStepB), Description = "cycle-b description")]
    [DependsOn<CycleStepA>]
    private sealed class CycleStepB : IProcessStep;

    [ProcessStep(DisplayName = nameof(AfterCycleStepA), Description = "after-cycle-a description")]
    [AvailableAfter<AfterCycleStepB>]
    private sealed class AfterCycleStepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(AfterCycleStepB), Description = "after-cycle-b description")]
    [AvailableAfter<AfterCycleStepA>]
    private sealed class AfterCycleStepB : IProcessStep;

    [ProcessStep(DisplayName = nameof(MixedCycleStepA), Description = "mixed-cycle-a description")]
    [DependsOn<MixedCycleStepB>]
    private sealed class MixedCycleStepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(MixedCycleStepB), Description = "mixed-cycle-b description")]
    [AvailableAfter<MixedCycleStepA>]
    private sealed class MixedCycleStepB : IProcessStep;

    [ProcessStep(DisplayName = nameof(UntilLoopStepA), Description = "until-loop-a description")]
    [AvailableUntil<UntilLoopStepB>]
    private sealed class UntilLoopStepA : IProcessStep;

    [ProcessStep(DisplayName = nameof(UntilLoopStepB), Description = "until-loop-b description")]
    [AvailableUntil<UntilLoopStepA>]
    private sealed class UntilLoopStepB : IProcessStep;

    private sealed class SelfDependentStepHandler : BaseHandler<SelfDependentStep, TestResponse>;

    private sealed class CycleStepAHandler : BaseHandler<CycleStepA, TestResponse>;

    private sealed class CycleStepBHandler : BaseHandler<CycleStepB, TestResponse>;

    private sealed class AfterCycleStepAHandler : BaseHandler<AfterCycleStepA, TestResponse>;

    private sealed class AfterCycleStepBHandler : BaseHandler<AfterCycleStepB, TestResponse>;

    private sealed class MixedCycleStepAHandler : BaseHandler<MixedCycleStepA, TestResponse>;

    private sealed class MixedCycleStepBHandler : BaseHandler<MixedCycleStepB, TestResponse>;

    private sealed class UntilLoopStepAHandler : BaseHandler<UntilLoopStepA, TestResponse>;

    private sealed class UntilLoopStepBHandler : BaseHandler<UntilLoopStepB, TestResponse>;

    [ProcessStep(DisplayName = nameof(SecuredStep), Description = "secured-step description")]
    [KaleidoAuthorization(Policy = "step-policy", Roles = "internal")]
    private sealed class SecuredStep : IProcessStep;

    [ProcessStep(DisplayName = nameof(InformationStep), Description = "information-step description")]
    private sealed class InformationStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }
        public string ExchangeId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }

    private sealed class InformationStepHandler
        : BaseHandler<InformationStep, TestResponse>;

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

    private sealed class TestPhiAttribute() : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute(new("Test", "PHI"));

    [ProcessStep(DisplayName = nameof(DeclaredStep), Description = "charges the member's card", Destructive = true, External = true, RequiresConfirmation = true)]
    private sealed class DeclaredStep : IProcessStep
    {
        [TestPhi]
        public string MemberName { get; init; } = string.Empty;

        public decimal Amount { get; init; }
    }

    private sealed record DeclaredResponse
    {
        [TestPhi]
        public string MemberName { get; init; } = string.Empty;
    }

    private sealed class DeclaredStepHandler
        : BaseHandler<DeclaredStep, DeclaredResponse>;
}
