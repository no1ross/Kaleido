using Kaleido.Exceptions;
using Kaleido.Processor;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class ProcessorServiceCollectionExtensionsTests
    : SutFixture
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
            provider.GetRequiredService<IProcessorRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.False(registration.IsEntryProcessor);

        var initialStep =
            Assert.Single(registration.InitialSteps);

        Assert.Equal(nameof(TestStep), initialStep.Name);

        var step =
            Assert.Single(registration.Steps);

        Assert.Equal(nameof(TestStep), step.Name);
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
            provider.GetRequiredService<IProcessorRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.True(registration.IsEntryProcessor);
    }

    [Fact]
    public void AddProcessor_WhenProcessStepAttributeOnTypeWithoutInterface_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.AttributeOnlyStep", implementsStep: false, withAttribute: true));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("AttributeOnlyStep", ex.Message);
    }

    [Fact]
    public void AddProcessor_WhenProcessStepTypeMissingAttribute_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.InterfaceOnlyStep", implementsStep: true, withAttribute: false));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains("InterfaceOnlyStep", ex.Message);
    }

    [Theory]
    [InlineData("", "Display", "Description", nameof(ProcessStepAttribute.Version))]
    [InlineData("1.0", " ", "Description", nameof(ProcessStepAttribute.DisplayName))]
    [InlineData("1.0", "Display", "", nameof(ProcessStepAttribute.Description))]
    public void AddProcessor_WhenRequiredMetadataEmpty_Throws(
        string version,
        string displayName,
        string description,
        string expectedProperty)
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(
                    module,
                    "Steps.EmptyMetadataStep",
                    implementsStep: true,
                    withAttribute: true,
                    version,
                    displayName,
                    description));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains(expectedProperty, ex.Message);
    }

    [Fact]
    public void AddProcessor_WhenTwoStepsShareTypeName_Throws()
    {
        var assembly =
            BuildAssembly(module =>
            {
                DefineType(module, "First.SharedStep", implementsStep: true, withAttribute: true);
                DefineType(module, "Second.SharedStep", implementsStep: true, withAttribute: true);
            });

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.DuplicateStep, ex.Code);
        Assert.Contains("SharedStep", ex.Message);
    }

    [Fact]
    public void AddProcessor_WhenInvalidTypeExcludedByTypeFilter_DoesNotThrow()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Excluded.AttributeOnlyStep", implementsStep: false, withAttribute: true));

        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "test-processor";
            o.Assemblies = [assembly];
            o.TypeFilter = type => type.Namespace != "Excluded";
        });
    }

    private static void AddKaleido(
        System.Reflection.Assembly assembly)
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "test-processor";
            o.Assemblies = [assembly];
        });
    }

    private static System.Reflection.Assembly BuildAssembly(
        Action<System.Reflection.Emit.ModuleBuilder> define)
    {
        var assemblyBuilder =
            System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                new System.Reflection.AssemblyName($"DynamicSteps_{Guid.NewGuid():N}"),
                System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect);

        define(assemblyBuilder.DefineDynamicModule("Steps"));

        return assemblyBuilder;
    }

    private static void DefineType(
        System.Reflection.Emit.ModuleBuilder module,
        string fullName,
        bool implementsStep,
        bool withAttribute,
        string version = "1.0",
        string displayName = "Dynamic step",
        string description = "Dynamically defined step.")
    {
        var typeBuilder =
            module.DefineType(
                fullName,
                System.Reflection.TypeAttributes.Public |
                System.Reflection.TypeAttributes.Class |
                System.Reflection.TypeAttributes.Sealed);

        if (implementsStep)
        {
            typeBuilder.AddInterfaceImplementation(typeof(IProcessStep));
        }

        if (withAttribute)
        {
            var attributeType = typeof(ProcessStepAttribute);

            typeBuilder.SetCustomAttribute(
                new System.Reflection.Emit.CustomAttributeBuilder(
                    attributeType.GetConstructor(Type.EmptyTypes)
                        ?? throw new InvalidOperationException("ProcessStepAttribute constructor not found."),
                    [],
                    [
                        attributeType.GetProperty(nameof(ProcessStepAttribute.Version))!,
                        attributeType.GetProperty(nameof(ProcessStepAttribute.DisplayName))!,
                        attributeType.GetProperty(nameof(ProcessStepAttribute.Description))!
                    ],
                    [version, displayName, description]));
        }

        typeBuilder.CreateType();
    }

    [ProcessStep(
        Description = "Test step",
        Version = "1.0.0",
        DisplayName = "Test Step")]
    public sealed record TestStep : IProcessStep;

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
