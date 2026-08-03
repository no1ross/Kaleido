using Kaleido.Process.Attributes;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.Tests.Participant.Registry;

public sealed class ProcessStepRegistryTests
{
    [Fact]
    public void Constructor_WhenServicesIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessStepRegistry(
                    null!,
                    [typeof(RootStep)]));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStepTypesIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessStepRegistry(
                    new ServiceCollection(),
                    null!));

        Assert.Equal("stepTypes", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenStepTypesContainDuplicates_RegistersEachStepOnlyOnce()
    {
        var registry =
            CreateRegistry(
                [typeof(RootStep), typeof(RootStep)],
                typeof(RootStepHandler));

        var registration =
            Assert.Single(registry.Registrations);

        Assert.Equal(typeof(RootStep), registration.StepType);
    }

    [Fact]
    public void Constructor_WhenStepHasRegisteredHandler_BuildsRegistration()
    {
        var registry =
            CreateRegistry(
                [typeof(RootStep)],
                typeof(RootStepHandler));

        var registration =
            Assert.Single(registry.Registrations);

        Assert.Equal(typeof(RootStep), registration.StepType);
        Assert.Equal(typeof(RootStepResult), registration.StepResultType);
        Assert.Equal(typeof(RootStepHandler), registration.HandlerType);

        Assert.Equal("root", registration.Metadata.Name);
        Assert.Equal("Root step.", registration.Metadata.Description);
        Assert.Equal("1.0", registration.Metadata.Version);
    }

    [Fact]
    public void Constructor_WhenStepIsMissingProcessStepAttribute_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(MissingAttributeStep)],
                    typeof(MissingAttributeStepHandler)));

        Assert.Contains(
            $"Process step '{nameof(MissingAttributeStep)}' is missing ProcessStepAttribute.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenNoHandlerIsRegisteredForStep_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(RootStep)]));

        Assert.Contains(
            $"No process step handler registered for step '{typeof(RootStep).FullName}'.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenMultipleHandlersAreRegisteredForSameStep_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(MultipleHandlerStep)],
                    typeof(MultipleHandlerStepHandlerA),
                    typeof(MultipleHandlerStepHandlerB)));

        Assert.Contains(
            $"Multiple process step handlers registered for step '{typeof(MultipleHandlerStep).FullName}'.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenDependencyIsNotRegistered_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(DependsOnUnregisteredStep)],
                    typeof(DependsOnUnregisteredStepHandler)));

        Assert.Contains(
            $"Process step '{typeof(DependsOnUnregisteredStep).FullName}' depends on",
            exception.Message);

        Assert.Contains(
            $"'{typeof(UnregisteredDependencyStep).FullName}', but that step is not registered.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenStepDependsOnItself_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(SelfDependentStep)],
                    typeof(SelfDependentStepHandler)));

        Assert.Contains(
            $"Process step '{typeof(SelfDependentStep).FullName}' cannot depend on itself.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenCircularDependencyExists_Throws()
    {
        var exception =
            Assert.Throws<InvalidOperationException>(() =>
                CreateRegistry(
                    [typeof(CircularStepA), typeof(CircularStepB)],
                    typeof(CircularStepAHandler),
                    typeof(CircularStepBHandler)));

        Assert.Contains(
            "Circular process step dependency detected:",
            exception.Message);
    }

    [Fact]
    public void Constructor_WhenMultipleStepsUseSameMetadataName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateRegistry(
                [typeof(DuplicateNameStepA), typeof(DuplicateNameStepB)],
                typeof(DuplicateNameStepAHandler),
                typeof(DuplicateNameStepBHandler)));
    }

    [Fact]
    public void Find_ByName_WhenNameExists_ReturnsRegistration()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.Find("root");

        Assert.NotNull(registration);
        Assert.Equal(typeof(RootStep), registration.StepType);
    }

    [Fact]
    public void Find_ByName_WhenNameUsesDifferentCasing_ReturnsRegistration()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.Find("ROOT");

        Assert.NotNull(registration);
        Assert.Equal(typeof(RootStep), registration.StepType);
    }

    [Fact]
    public void Find_ByName_WhenNameDoesNotExist_ReturnsNull()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.Find("missing");

        Assert.Null(registration);
    }

    [Fact]
    public void Find_ByName_WhenNameIsNull_Throws()
    {
        var registry =
            CreateRootRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.Find((string)null!));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Find_ByName_WhenNameIsWhiteSpace_Throws()
    {
        var registry =
            CreateRootRegistry();

        var exception =
            Assert.Throws<ArgumentException>(() =>
                registry.Find("   "));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Find_ByType_WhenTypeExists_ReturnsRegistration()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.Find(typeof(RootStep));

        Assert.NotNull(registration);
        Assert.Equal("root", registration.Metadata.Name);
    }

    [Fact]
    public void Find_ByType_WhenTypeDoesNotExist_ReturnsNull()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.Find(typeof(NotRegisteredStep));

        Assert.Null(registration);
    }

    [Fact]
    public void Find_ByType_WhenTypeIsNull_Throws()
    {
        var registry =
            CreateRootRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.Find((Type)null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void GetRegistration_ByName_WhenNameExists_ReturnsRegistration()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.GetRegistration("root");

        Assert.Equal(typeof(RootStep), registration.StepType);
    }

    [Fact]
    public void GetRegistration_ByName_WhenNameDoesNotExist_Throws()
    {
        var registry =
            CreateRootRegistry();

        var exception =
            Assert.Throws<KeyNotFoundException>(() =>
                registry.GetRegistration("missing"));

        Assert.Contains(
            "Process step 'missing' is not registered.",
            exception.Message);
    }

    [Fact]
    public void GetRegistration_ByType_WhenTypeExists_ReturnsRegistration()
    {
        var registry =
            CreateRootRegistry();

        var registration =
            registry.GetRegistration(typeof(RootStep));

        Assert.Equal("root", registration.Metadata.Name);
    }

    [Fact]
    public void GetRegistration_ByType_WhenTypeDoesNotExist_Throws()
    {
        var registry =
            CreateRootRegistry();

        var exception =
            Assert.Throws<KeyNotFoundException>(() =>
                registry.GetRegistration(typeof(NotRegisteredStep)));

        Assert.Contains(
            $"Process step type '{typeof(NotRegisteredStep).FullName}' is not registered.",
            exception.Message);
    }

    [Fact]
    public void HasDependencies_WhenStepHasDependencies_ReturnsTrue()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependencies(typeof(DependsOnRootStep));

        Assert.True(result);
    }

    [Fact]
    public void HasDependencies_WhenStepHasNoDependencies_ReturnsFalse()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependencies(typeof(RootStep));

        Assert.False(result);
    }

    [Fact]
    public void HasDependencies_WhenStepTypeIsUnknown_ReturnsFalse()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependencies(typeof(NotRegisteredStep));

        Assert.False(result);
    }

    [Fact]
    public void HasDependencies_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.HasDependencies(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void HasDependents_WhenStepHasDependents_ReturnsTrue()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependents(typeof(RootStep));

        Assert.True(result);
    }

    [Fact]
    public void HasDependents_WhenStepHasNoDependents_ReturnsFalse()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependents(typeof(DependsOnRootAndBranchStep));

        Assert.False(result);
    }

    [Fact]
    public void HasDependents_WhenStepTypeIsUnknown_ReturnsFalse()
    {
        var registry =
            CreateDependencyRegistry();

        var result =
            registry.HasDependents(typeof(NotRegisteredStep));

        Assert.False(result);
    }

    [Fact]
    public void HasDependents_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.HasDependents(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void GetDependencies_WhenStepHasDependencies_ReturnsDirectDependencies()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencies =
            registry.GetDependencies(typeof(DependsOnRootAndBranchStep));

        Assert.Equal(2, dependencies.Count);
        Assert.Contains(dependencies, x => x.StepType == typeof(RootStep));
        Assert.Contains(dependencies, x => x.StepType == typeof(BranchStep));
    }

    [Fact]
    public void GetDependencies_WhenStepHasNoDependencies_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencies =
            registry.GetDependencies(typeof(RootStep));

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependencies_WhenStepTypeIsUnknown_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencies =
            registry.GetDependencies(typeof(NotRegisteredStep));

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependencies_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.GetDependencies(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void GetDependents_WhenStepHasDependents_ReturnsDirectDependents()
    {
        var registry =
            CreateDependencyRegistry();

        var dependents =
            registry.GetDependents(typeof(RootStep));

        Assert.Equal(2, dependents.Count);
        Assert.Contains(dependents, x => x.StepType == typeof(DependsOnRootStep));
        Assert.Contains(dependents, x => x.StepType == typeof(DependsOnRootAndBranchStep));
    }

    [Fact]
    public void GetDependents_WhenStepHasNoDependents_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependents =
            registry.GetDependents(typeof(DependsOnRootAndBranchStep));

        Assert.Empty(dependents);
    }

    [Fact]
    public void GetDependents_WhenStepTypeIsUnknown_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependents =
            registry.GetDependents(typeof(NotRegisteredStep));

        Assert.Empty(dependents);
    }

    [Fact]
    public void GetDependents_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.GetDependents(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void GetDependencyChain_WhenStepHasTransitiveDependencies_ReturnsFullDependencyChain()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencyChain =
            registry.GetDependencyChain(typeof(DependsOnDependsOnRootStep));

        Assert.Equal(2, dependencyChain.Count);
        Assert.Contains(dependencyChain, x => x.StepType == typeof(DependsOnRootStep));
        Assert.Contains(dependencyChain, x => x.StepType == typeof(RootStep));
    }

    [Fact]
    public void GetDependencyChain_WhenStepHasNoDependencies_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencyChain =
            registry.GetDependencyChain(typeof(RootStep));

        Assert.Empty(dependencyChain);
    }

    [Fact]
    public void GetDependencyChain_WhenStepTypeIsUnknown_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependencyChain =
            registry.GetDependencyChain(typeof(NotRegisteredStep));

        Assert.Empty(dependencyChain);
    }

    [Fact]
    public void GetDependencyChain_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.GetDependencyChain(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void GetDependentChain_WhenStepHasTransitiveDependents_ReturnsFullDependentChain()
    {
        var registry =
            CreateDependencyRegistry();

        var dependentChain =
            registry.GetDependentChain(typeof(RootStep));

        Assert.Equal(3, dependentChain.Count);
        Assert.Contains(dependentChain, x => x.StepType == typeof(DependsOnRootStep));
        Assert.Contains(dependentChain, x => x.StepType == typeof(DependsOnDependsOnRootStep));
        Assert.Contains(dependentChain, x => x.StepType == typeof(DependsOnRootAndBranchStep));
    }

    [Fact]
    public void GetDependentChain_WhenStepHasNoDependents_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependentChain =
            registry.GetDependentChain(typeof(DependsOnRootAndBranchStep));

        Assert.Empty(dependentChain);
    }

    [Fact]
    public void GetDependentChain_WhenStepTypeIsUnknown_ReturnsEmptyCollection()
    {
        var registry =
            CreateDependencyRegistry();

        var dependentChain =
            registry.GetDependentChain(typeof(NotRegisteredStep));

        Assert.Empty(dependentChain);
    }

    [Fact]
    public void GetDependentChain_WhenStepTypeIsNull_Throws()
    {
        var registry =
            CreateDependencyRegistry();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                registry.GetDependentChain(null!));

        Assert.Equal("stepType", exception.ParamName);
    }

    [Fact]
    public void Graph_WhenRegistryIsCreated_IsAvailable()
    {
        var registry =
            CreateDependencyRegistry();

        Assert.NotNull(registry.Graph);
    }

    private static ProcessStepRegistry CreateRootRegistry()
    {
        return CreateRegistry(
            [typeof(RootStep)],
            typeof(RootStepHandler));
    }

    private static ProcessStepRegistry CreateDependencyRegistry()
    {
        return CreateRegistry(
            [
                typeof(RootStep),
                typeof(BranchStep),
                typeof(DependsOnRootStep),
                typeof(DependsOnDependsOnRootStep),
                typeof(DependsOnRootAndBranchStep)
            ],
            typeof(RootStepHandler),
            typeof(BranchStepHandler),
            typeof(DependsOnRootStepHandler),
            typeof(DependsOnDependsOnRootStepHandler),
            typeof(DependsOnRootAndBranchStepHandler));
    }

    private static ProcessStepRegistry CreateRegistry(
        IReadOnlyCollection<Type> stepTypes,
        params Type[] handlerTypes)
    {
        var services =
            CreateServices(handlerTypes);

        return new ProcessStepRegistry(
            services,
            stepTypes);
    }

    private static IServiceCollection CreateServices(
        params Type[] handlerTypes)
    {
        var services =
            new ServiceCollection();

        foreach (var handlerType in handlerTypes)
        {
            var handlerInterface =
                handlerType
                    .GetInterfaces()
                    .Single(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IProcessStepHandler<,>));

            services.AddTransient(
                handlerInterface,
                handlerType);
        }

        return services;
    }

    [ProcessStep("root", "Root step.", "1.0")]
    private sealed class RootStep;

    private sealed class RootStepResult;

    private abstract class RootStepHandler :
        TestHandler<RootStep, RootStepResult>;

    [ProcessStep("branch", "Branch step.", "1.0")]
    private sealed class BranchStep;

    private sealed class BranchStepResult;

    private abstract class BranchStepHandler :
        TestHandler<BranchStep, BranchStepResult>;

    [DependsOnStep(typeof(RootStep))]
    [ProcessStep("depends-on-root", "Depends on root step.", "1.0")]
    private sealed class DependsOnRootStep;

    private sealed class DependsOnRootStepResult;

    private abstract class DependsOnRootStepHandler :
        TestHandler<DependsOnRootStep, DependsOnRootStepResult>;

    [DependsOnStep(typeof(DependsOnRootStep))]
    [ProcessStep("depends-on-depends-on-root", "Depends on a step that depends on root.", "1.0")]
    private sealed class DependsOnDependsOnRootStep;

    private sealed class DependsOnDependsOnRootStepResult;

    private abstract class DependsOnDependsOnRootStepHandler :
        TestHandler<DependsOnDependsOnRootStep, DependsOnDependsOnRootStepResult>;

    [DependsOnStep(typeof(RootStep))]
    [DependsOnStep(typeof(BranchStep))]
    [ProcessStep("depends-on-root-and-branch", "Depends on root and branch.", "1.0")]
    private sealed class DependsOnRootAndBranchStep;

    private sealed class DependsOnRootAndBranchStepResult;

    private abstract class DependsOnRootAndBranchStepHandler :
        TestHandler<DependsOnRootAndBranchStep, DependsOnRootAndBranchStepResult>;

    private sealed class MissingAttributeStep;

    private sealed class MissingAttributeStepResult;

    private abstract class MissingAttributeStepHandler :
        TestHandler<MissingAttributeStep, MissingAttributeStepResult>;

    [ProcessStep("multiple-handler", "Step with multiple handlers.", "1.0")]
    private sealed class MultipleHandlerStep;

    private sealed class MultipleHandlerStepResult;

    private abstract class MultipleHandlerStepHandlerA :
        TestHandler<MultipleHandlerStep, MultipleHandlerStepResult>;

    private abstract class MultipleHandlerStepHandlerB :
        TestHandler<MultipleHandlerStep, MultipleHandlerStepResult>;

    [DependsOnStep(typeof(UnregisteredDependencyStep))]
    [ProcessStep("depends-on-unregistered", "Depends on an unregistered step.", "1.0")]
    private sealed class DependsOnUnregisteredStep;

    private sealed class DependsOnUnregisteredStepResult;

    private abstract class DependsOnUnregisteredStepHandler :
        TestHandler<DependsOnUnregisteredStep, DependsOnUnregisteredStepResult>;

    [ProcessStep("unregistered-dependency", "Unregistered dependency step.", "1.0")]
    private sealed class UnregisteredDependencyStep;

    [DependsOnStep(typeof(SelfDependentStep))]
    [ProcessStep("self-dependent", "Self dependent step.", "1.0")]
    private sealed class SelfDependentStep;

    private sealed class SelfDependentStepResult;

    private abstract class SelfDependentStepHandler :
        TestHandler<SelfDependentStep, SelfDependentStepResult>;

    [DependsOnStep(typeof(CircularStepB))]
    [ProcessStep("circular-a", "Circular step A.", "1.0")]
    private sealed class CircularStepA;

    private sealed class CircularStepAResult;

    private abstract class CircularStepAHandler :
        TestHandler<CircularStepA, CircularStepAResult>;

    [DependsOnStep(typeof(CircularStepA))]
    [ProcessStep("circular-b", "Circular step B.", "1.0")]
    private sealed class CircularStepB;

    private sealed class CircularStepBResult;

    private abstract class CircularStepBHandler :
        TestHandler<CircularStepB, CircularStepBResult>;

    [ProcessStep("duplicate-name", "Duplicate name A.", "1.0")]
    private sealed class DuplicateNameStepA;

    private sealed class DuplicateNameStepAResult;

    private abstract class DuplicateNameStepAHandler :
        TestHandler<DuplicateNameStepA, DuplicateNameStepAResult>;

    [ProcessStep("duplicate-name", "Duplicate name B.", "1.0")]
    private sealed class DuplicateNameStepB;

    private sealed class DuplicateNameStepBResult;

    private abstract class DuplicateNameStepBHandler :
        TestHandler<DuplicateNameStepB, DuplicateNameStepBResult>;

    [ProcessStep("not-registered", "Not registered step.", "1.0")]
    private sealed class NotRegisteredStep;


    private abstract class TestHandler<TProcessStep, TProcessStepResult> :
        IProcessStepHandler<TProcessStep, TProcessStepResult>
    {
        public Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(
            TProcessStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}