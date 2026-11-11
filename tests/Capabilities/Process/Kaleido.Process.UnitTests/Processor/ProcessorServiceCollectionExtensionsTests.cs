using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Registry;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Processor.UnitTests;

public sealed class ProcessorServiceCollectionExtensionsTests
    : SutFixture
{
    [Fact]
    public void AddProcessEventDispatch_RegistersWithoutProcessRuntime()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Services).Returns(services);

        builder.Object.AddProcessEventDispatch();
        builder.Object.AddProcessEventDispatch();

        Assert.Single(services, x => x.ServiceType == typeof(IOutboxEventDispatcher) && x.ImplementationType == typeof(ProcessOutboxEventDispatcher));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessorRegistry));
    }

    [Fact]
    public void AddProcess_RegistersProcessorRegistry()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            DisplayName = "Test Processor",
            Description = "Test processor.",
            Assemblies = new[] { typeof(TestStep).Assembly },
        }).AddProcess();

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
        Assert.Single(step.Result.OutputFields);
    }

    [Fact]
    public void AddProcess_RegistersTheStepRegistryOnce_FromTheScannedSteps()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = new[] { typeof(TestStep).Assembly } }).AddProcess();

        var descriptor =
            Assert.Single(services, x => x.ServiceType == typeof(IProcessorStepRegistry));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IProcessorStepRegistry>().Find(typeof(TestStep)));
    }

    [Fact]
    public void AddProcess_WithIsEntryProcessor_RegistrationReflectsIt()
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            Assemblies = new[] { typeof(TestStep).Assembly },
        }).AddProcess(new() { IsEntryProcessor = true });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IProcessorRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.True(registration.IsEntryProcessor);
    }

    [Fact]
    public void AddProcess_WhenProcessStepAttributeOnTypeWithoutInterface_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.AttributeOnlyStep", implementsStep: false, withAttribute: true));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("AttributeOnlyStep", ex.Message);
    }

    [Fact]
    public void AddProcess_WhenProcessStepTypeMissingAttribute_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.InterfaceOnlyStep", implementsStep: true, withAttribute: false));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains("InterfaceOnlyStep", ex.Message);
    }

    [Theory]
    [InlineData(" ", "Description", nameof(ProcessStepAttribute.DisplayName))]
    [InlineData("Display", "", nameof(ProcessStepAttribute.Description))]
    public void AddProcess_WhenRequiredMetadataEmpty_Throws(
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
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains(expectedProperty, ex.Message);
    }

    [Fact]
    public void AddProcess_WhenTwoStepsShareTypeName_Throws()
    {
        var assembly =
            BuildAssembly(module =>
            {
                DefineType(module, "First.SharedStep", implementsStep: true, withAttribute: true);
                DefineType(module, "Second.SharedStep", implementsStep: true, withAttribute: true);
            });

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.DuplicateStep, ex.Code);
        Assert.Contains("SharedStep", ex.Message);
    }

    [Fact]
    public void AddProcess_WhenInvalidTypeExcludedByTypeFilter_DoesNotThrow()
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
        }).AddProcess();

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessorStepRegistry));
    }

    [Fact]
    public void AddProcess_WhenInformationStepDeclaresOtherProperties_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineInformationStep(module, "Steps.MixedInformationStep", extraProperty: "MemberId"));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("MemberId", ex.Message);
    }

    [Fact]
    public void AddProcess_WhenNoAssemblyIsRegistered_Throws()
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([]);
        builder.SetupGet(x => x.Configuration).Returns(new ConfigurationBuilder().Build());

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddProcess());

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, ex.Code);
    }

    [Fact]
    public void AddProcess_WhenBuilderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IKaleidoBuilder)null!).AddProcess());
    }

    [Fact]
    public void AddProcess_WhenAStepHasNoHandler_Throws()
    {
        var assembly =
            BuildAssembly(module =>
                DefineType(module, "Steps.UnhandledStep", implementsStep: true, withAttribute: true));

        var ex =
            Assert.Throws<KaleidoConfigurationException>(() =>
                RegisterProcess(assembly));

        Assert.Equal(ProcessorErrorCodes.MissingHandler, ex.Code);
        Assert.Contains("Steps.UnhandledStep", ex.Message);
    }

    [Fact]
    public void AddProcess_WhenAStepHasTwoHandlers_Throws()
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
                RegisterProcess(assembly));

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
    public void AddProcess_IgnoresAbstractAndInterfaceStepTypes()
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

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = [assembly] }).AddProcess();

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessorStepRegistry));
    }

    [Fact]
    public void AddProcess_DoesNotSelectStateOrIdempotencyStores()
    {
        var services = NewProcessBuilder().Services;

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IKaleidoContextStore));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IKaleidoIdempotencyStore));
        Assert.Equal(typeof(NoKaleidoTransactions), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoTransactions)).ImplementationType);
    }

    [Fact]
    public void AddContextStore_IdempotencyStore_AndTransactions_RegisterExplicitStores_AsScoped()
    {
        var services = NewProcessBuilder().AddContextStore<OwnContextStore>().AddIdempotencyStore<OwnIdempotencyStore>().AddTransactions<OwnTransactions>().Services;

        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, x => x.ServiceType == typeof(IKaleidoContextStore)).Lifetime);
        Assert.Equal(typeof(OwnContextStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoContextStore)).ImplementationType);
        Assert.Equal(typeof(OwnIdempotencyStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoIdempotencyStore)).ImplementationType);
        Assert.Equal(typeof(OwnTransactions), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoTransactions)).ImplementationType);
    }

    [Fact]
    public void AddContextStore_TheSameStoreTwice_IsRegisteredOnce()
    {
        var services = NewProcessBuilder().AddContextStore<OwnContextStore>().AddContextStore<OwnContextStore>().Services;

        Assert.Equal(typeof(OwnContextStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoContextStore)).ImplementationType);
    }

    // One store per service: a second, different one (by type, instance or factory) is a mistake.
    [Fact]
    public void AddContextStore_WhenAnotherStoreIsRegistered_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        var builder = NewProcessBuilder(services);
        services.AddScoped(_ => Mock.Of<IKaleidoContextStore>());

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddContextStore<OwnContextStore>);

        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, exception.Code);
    }

    [Fact]
    public void AddContextStore_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddContextStore<OwnContextStore>());
    }

    private static IKaleidoBuilder NewProcessBuilder(IServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            Assemblies = [typeof(TestStep).Assembly],
            TypeFilter = type => type == typeof(TestStep),
        }).AddProcess();

    private sealed class OwnContextStore : IKaleidoContextStore
    {
        public Task<Kaleido.Processor.Context.ProcessorContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Kaleido.Processor.Context.ProcessorContext> SaveAsync(Kaleido.Processor.Context.ProcessorContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OwnIdempotencyStore : IKaleidoIdempotencyStore
    {
        public Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CompleteAsync(IdempotentResult result, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OwnTransactions : IKaleidoTransactions
    {
        public Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private static void RegisterProcess(
        System.Reflection.Assembly assembly)
    {
        var services = new ServiceCollection();

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", Assemblies = [assembly] }).AddProcess();
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
        DefineProperty(typeBuilder, nameof(IInformationStep.ExchangeId), typeof(string), implementsInterface: true);
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
