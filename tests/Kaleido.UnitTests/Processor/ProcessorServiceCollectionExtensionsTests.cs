using Kaleido.Exceptions;
using Kaleido.Processor.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Processor.UnitTests;

public sealed class ProcessorServiceCollectionExtensionsTests
    : SutFixture
{
    [Fact]
    public void AddProcessor_RegistersProcessorRegistry()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            DisplayName = "Test Processor",
            Description = "Test processor.",
            Assemblies = new[] { typeof(TestStep).Assembly },
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
    public void AddProcessor_RegistersTheStepRegistryOnce_FromTheScannedSteps()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = new[] { typeof(TestStep).Assembly } });

        var descriptor =
            Assert.Single(services, x => x.ServiceType == typeof(IProcessorStepRegistry));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IProcessorStepRegistry>().Find(typeof(TestStep)));
    }

    [Fact]
    public void AddProcessor_WithIsEntryProcessor_RegistrationReflectsIt()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            Assemblies = new[] { typeof(TestStep).Assembly },
            IsEntryProcessor = true,
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
    [InlineData(" ", "Description", nameof(ProcessStepAttribute.DisplayName))]
    [InlineData("Display", "", nameof(ProcessStepAttribute.Description))]
    public void AddProcessor_WhenRequiredMetadataEmpty_Throws(
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

        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            Assemblies = [assembly],
            TypeFilter = type => type.Namespace != "Excluded",
        });
    }

    [Fact]
    public void AddProcessor_WhenInformationStepDeclaresOtherProperties_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineInformationStep(module, "Steps.MixedInformationStep", extraProperty: "MemberId"));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("MemberId", ex.Message);
    }

    [Fact]
    public void AddProcessor_WhenNoAssemblyIsRegistered_Throws()
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([]);

        var ex =
            Assert.Throws<KaleidoConfigurationException>(builder.Object.AddProcessor);

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, ex.Code);
    }

    [Fact]
    public void AddProcessor_WhenBuilderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IKaleidoBuilder)null!).AddProcessor());
    }

    [Fact]
    public void AddProcessor_WhenAStepHasNoHandler_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.UnhandledStep", implementsStep: true, withAttribute: true));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingHandler, ex.Code);
        Assert.Contains("Steps.UnhandledStep", ex.Message);
    }

    [Fact]
    public void AddProcessor_WhenAStepHasTwoHandlers_Throws()
    {
        var assembly =
            BuildAssembly(module =>
            {
                var step = DefineType(module, "Steps.DoublyHandledStep", implementsStep: true, withAttribute: true);
                DefineHandler(module, "Handlers.FirstHandler", step);
                DefineHandler(module, "Handlers.SecondHandler", step);
            });

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                AddKaleido(assembly));

        Assert.Equal(ProcessorErrorCodes.InvalidHandler, ex.Code);
        Assert.Contains("Handlers.FirstHandler", ex.Message);
        Assert.Contains("Handlers.SecondHandler", ex.Message);
    }

    // Emits a concrete IProcessStepHandler<TStep> whose ExecuteAsync throws NotImplementedException.
    private static void DefineHandler(
        System.Reflection.Emit.ModuleBuilder module,
        string fullName,
        Type stepType)
    {
        var handlerInterface = typeof(IProcessStepHandler<>).MakeGenericType(stepType);
        var typeBuilder =
            module.DefineType(
                fullName,
                System.Reflection.TypeAttributes.Public |
                System.Reflection.TypeAttributes.Class |
                System.Reflection.TypeAttributes.Sealed);

        typeBuilder.AddInterfaceImplementation(handlerInterface);
        typeBuilder.DefineDefaultConstructor(System.Reflection.MethodAttributes.Public);

        var execute =
            typeBuilder.DefineMethod(
                nameof(IProcessStepHandler<IProcessStep>.ExecuteAsync),
                System.Reflection.MethodAttributes.Public |
                System.Reflection.MethodAttributes.Virtual |
                System.Reflection.MethodAttributes.Final |
                System.Reflection.MethodAttributes.NewSlot |
                System.Reflection.MethodAttributes.HideBySig,
                typeof(Task<ProcessStepHandlerResult>),
                [stepType, typeof(ProcessStepContext), typeof(CancellationToken)]);
        var il = execute.GetILGenerator();
        il.Emit(
            System.Reflection.Emit.OpCodes.Newobj,
            typeof(NotImplementedException).GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException("NotImplementedException constructor not found."));
        il.Emit(System.Reflection.Emit.OpCodes.Throw);

        typeBuilder.DefineMethodOverride(
            execute,
            handlerInterface.GetMethod(nameof(IProcessStepHandler<IProcessStep>.ExecuteAsync))
                ?? throw new InvalidOperationException("ExecuteAsync not found."));
        typeBuilder.CreateType();
    }

    [Fact]
    public void AddProcessor_IgnoresAbstractAndInterfaceStepTypes()
    {
        var assembly =
            BuildAssembly(module =>
            {
                module.DefineType(
                        "Steps.AbstractStep",
                        System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class | System.Reflection.TypeAttributes.Abstract,
                        parent: null,
                        interfaces: [typeof(IProcessStep)])
                    .CreateType();
                DefineHandlerInterface(module, "Steps.IStepContract", typeof(IProcessStep));
            });

        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = [assembly] });

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessorStepRegistry));
    }

    private static void DefineHandlerInterface(
        System.Reflection.Emit.ModuleBuilder module,
        string fullName,
        Type handlerInterface)
    {
        var typeBuilder =
            module.DefineType(
                fullName,
                System.Reflection.TypeAttributes.Public |
                System.Reflection.TypeAttributes.Interface |
                System.Reflection.TypeAttributes.Abstract);

        typeBuilder.AddInterfaceImplementation(handlerInterface);
        typeBuilder.CreateType();
    }

    private static void AddKaleido(
        System.Reflection.Assembly assembly)
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = [assembly] });
    }

    private static System.Reflection.Emit.AssemblyBuilder BuildAssembly(
        Action<System.Reflection.Emit.ModuleBuilder> define)
    {
        var assemblyBuilder =
            System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                new System.Reflection.AssemblyName($"DynamicSteps_{Guid.NewGuid():N}"),
                System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect);

        define(assemblyBuilder.DefineDynamicModule("Steps"));

        return assemblyBuilder;
    }

    private static Type DefineType(
        System.Reflection.Emit.ModuleBuilder module,
        string fullName,
        bool implementsStep,
        bool withAttribute,
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
                        attributeType.GetProperty(nameof(ProcessStepAttribute.DisplayName))!,
                        attributeType.GetProperty(nameof(ProcessStepAttribute.Description))!
                    ],
                    [displayName, description]));
        }

        return typeBuilder.CreateType();
    }

    // Emits an IInformationStep (with the interface's init-only properties) plus an extra property.
    private static void DefineInformationStep(
        System.Reflection.Emit.ModuleBuilder module,
        string fullName,
        string extraProperty)
    {
        var typeBuilder =
            module.DefineType(
                fullName,
                System.Reflection.TypeAttributes.Public |
                System.Reflection.TypeAttributes.Class |
                System.Reflection.TypeAttributes.Sealed);

        typeBuilder.AddInterfaceImplementation(typeof(IInformationStep));
        typeBuilder.DefineDefaultConstructor(System.Reflection.MethodAttributes.Public);

        DefineProperty(typeBuilder, nameof(IInformationStep.InformationRequestId), typeof(string), implementsInterface: true);
        DefineProperty(typeBuilder, nameof(IInformationStep.Items), typeof(IReadOnlyList<InformationResponseItem>), implementsInterface: true);
        DefineProperty(typeBuilder, extraProperty, typeof(string), implementsInterface: false);

        var attributeType = typeof(ProcessStepAttribute);

        typeBuilder.SetCustomAttribute(
            new System.Reflection.Emit.CustomAttributeBuilder(
                attributeType.GetConstructor(Type.EmptyTypes)
                    ?? throw new InvalidOperationException("ProcessStepAttribute constructor not found."),
                [],
                [
                    attributeType.GetProperty(nameof(ProcessStepAttribute.DisplayName))!,
                    attributeType.GetProperty(nameof(ProcessStepAttribute.Description))!
                ],
                ["Dynamic information step", "Dynamically defined information step."]));

        typeBuilder.CreateType();
    }

    private static void DefineProperty(
        System.Reflection.Emit.TypeBuilder typeBuilder,
        string name,
        Type type,
        bool implementsInterface)
    {
        const System.Reflection.MethodAttributes accessorAttributes =
            System.Reflection.MethodAttributes.Public |
            System.Reflection.MethodAttributes.Virtual |
            System.Reflection.MethodAttributes.Final |
            System.Reflection.MethodAttributes.NewSlot |
            System.Reflection.MethodAttributes.HideBySig |
            System.Reflection.MethodAttributes.SpecialName;

        var field = typeBuilder.DefineField($"_{name}", type, System.Reflection.FieldAttributes.Private);
        var property = typeBuilder.DefineProperty(name, System.Reflection.PropertyAttributes.None, type, null);

        var getter = typeBuilder.DefineMethod($"get_{name}", accessorAttributes, type, Type.EmptyTypes);
        var getIl = getter.GetILGenerator();
        getIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        getIl.Emit(System.Reflection.Emit.OpCodes.Ldfld, field);
        getIl.Emit(System.Reflection.Emit.OpCodes.Ret);

        var setter =
            typeBuilder.DefineMethod(
                $"set_{name}",
                accessorAttributes,
                System.Reflection.CallingConventions.HasThis,
                typeof(void),
                [typeof(System.Runtime.CompilerServices.IsExternalInit)],
                null,
                [type],
                null,
                null);
        var setIl = setter.GetILGenerator();
        setIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        setIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
        setIl.Emit(System.Reflection.Emit.OpCodes.Stfld, field);
        setIl.Emit(System.Reflection.Emit.OpCodes.Ret);

        property.SetGetMethod(getter);
        property.SetSetMethod(setter);

        if (implementsInterface)
        {
            var interfaceProperty = typeof(IInformationStep).GetProperty(name)!;
            typeBuilder.DefineMethodOverride(getter, interfaceProperty.GetMethod!);
            typeBuilder.DefineMethodOverride(setter, interfaceProperty.SetMethod!);
        }
    }

    [ProcessStep(
        Description = "Test step",
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
