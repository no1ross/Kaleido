using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Process.Eventing;
using Kaleido.Process.Registry;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.UnitTests;

public sealed class ProcessServiceCollectionExtensionsTests
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
            ServiceVersion = "1",
            DisplayName = "Test Processor",
            Description = "Test processor.",
            Assemblies = new[] { typeof(TestStep).Assembly },
            TypeFilter = type => type == typeof(TestStep),
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

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", ServiceVersion = "1", Assemblies = new[] { typeof(TestStep).Assembly } }).AddProcess();

        var descriptor =
            Assert.Single(services, x => x.ServiceType == typeof(IProcessorStepRegistry));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, x => x.ServiceType == typeof(TestStepHandler)).Lifetime);

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
            ServiceVersion = "1",
            Assemblies = new[] { typeof(TestStep).Assembly },
            TypeFilter = type => type == typeof(TestStep),
        }).AddProcess(new() { IsEntryProcessor = true });

        using var provider = services.BuildServiceProvider();

        var registry =
            provider.GetRequiredService<IProcessorRegistry>();

        var registration =
            Assert.Single(registry.Registrations);

        Assert.True(registration.IsEntryProcessor);
    }

    [Fact]
    public void AddProcess_CodeDefault_SnapshotsTheCallersRoles()
    {
        var roles = new List<string> { "reader" };
        var code = new KaleidoProcessOptions { DefaultAuthorization = new AuthorizationMetadata("staff", roles) };
        var services = NewProcessBuilder(options: code).Services;
        roles.Add("later");
        using var provider = services.BuildServiceProvider();

        var effective = provider.GetRequiredService<KaleidoProcessOptions>();
        var registration = provider.GetRequiredService<IProcessorStepRegistry>().GetRegistration(typeof(TestStep));
        Assert.Equal(["reader"], effective.DefaultAuthorization?.Roles);
        Assert.Equal(["reader"], registration.Metadata.Authorization.Roles);
        Assert.Equal(["reader", "later"], code.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddProcess_ConfigurationOverridesCodeDefault_AndStepAttributeWins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Process:IsEntryProcessor"] = "true",
                ["Kaleido:Process:DefaultAuthorization:Policy"] = "configured-policy",
                ["Kaleido:Process:DefaultAuthorization:Roles:0"] = "radiology",
            })
            .Build();
        var roles = new List<string> { "code-role" };
        var code = new KaleidoProcessOptions { DefaultAuthorization = new AuthorizationMetadata("code-policy", roles) };
        var services = NewProcessBuilder(configuration: configuration, options: code,
            typeFilter: type => type == typeof(TestStep) || type == typeof(SecuredStep)).Services;
        roles.Add("later");
        using var provider = services.BuildServiceProvider();

        var effective = provider.GetRequiredService<KaleidoProcessOptions>();
        var steps = provider.GetRequiredService<IProcessorStepRegistry>();
        Assert.True(effective.IsEntryProcessor);
        Assert.Equal("configured-policy", effective.DefaultAuthorization?.Policy);
        Assert.Equal(["radiology"], effective.DefaultAuthorization?.Roles);
        Assert.Equal("code-policy", code.DefaultAuthorization.Policy);
        Assert.Equal(["code-role", "later"], code.DefaultAuthorization.Roles);
        Assert.Equal("configured-policy", steps.GetRegistration(typeof(TestStep)).Metadata.Authorization.Policy);
        Assert.Equal("explicit-policy", steps.GetRegistration(typeof(SecuredStep)).Metadata.Authorization.Policy);
    }

    [Fact]
    public void AddProcess_ConfiguredDefaultWithoutCodeOptions_BindsOnce()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Process:DefaultAuthorization:Roles:0"] = "reader",
            })
            .Build();
        var services = NewProcessBuilder(configuration: configuration).Services;
        using var provider = services.BuildServiceProvider();

        var rule = provider.GetRequiredService<KaleidoProcessOptions>().DefaultAuthorization;
        Assert.Null(rule?.Policy);
        Assert.Equal(["reader"], rule?.Roles);
    }

    [Fact]
    public void AddProcess_RejectsConflictingConfiguredDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Process:DefaultAuthorization:AllowAnonymous"] = "true",
                ["Kaleido:Process:DefaultAuthorization:Roles:0"] = "reader",
            })
            .Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => NewProcessBuilder(configuration: configuration));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
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
    public void AddProcess_WhenDiscoveryFails_DoesNotPartiallyRegisterServices()
    {
        var assembly = BuildAssembly(module =>
        {
            var step = DefineType(module, "Steps.ValidStep", implementsStep: true, withAttribute: true);
            DefineHandler(module, "Handlers.ValidHandler", step);
            DefineType(module, "Steps.UnhandledStep", implementsStep: true, withAttribute: true);
        });
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            ServiceVersion = "1",
            Assemblies = [assembly],
        });
        var before = services.ToArray();

        var ex = Assert.Throws<KaleidoConfigurationException>(() => builder.AddProcess());

        Assert.Equal(ProcessorErrorCodes.MissingHandler, ex.Code);
        Assert.Equal(before, services);
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

        services.AddKaleido(new ConfigurationBuilder().Build(), new() { ServiceName = "test-processor", ServiceVersion = "1", Assemblies = [assembly] }).AddProcess();

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessorStepRegistry));
    }

    [Fact]
    public void AddProcess_DoesNotSelectStateStore()
    {
        var services = NewProcessBuilder().Services;

        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IProcessContextStore));
        Assert.Equal(typeof(NoProcessTransactions), Assert.Single(services, x => x.ServiceType == typeof(IProcessTransactions)).ImplementationType);
    }

    [Fact]
    public void AddContextStore_AndTransactions_RegisterExplicitStores_AsScoped()
    {
        var services = NewProcessBuilder().AddContextStore<OwnContextStore>().AddTransactions<OwnTransactions>().Services;

        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services, x => x.ServiceType == typeof(IProcessContextStore)).Lifetime);
        Assert.Equal(typeof(OwnContextStore), Assert.Single(services, x => x.ServiceType == typeof(IProcessContextStore)).ImplementationType);
        Assert.Equal(typeof(OwnTransactions), Assert.Single(services, x => x.ServiceType == typeof(IProcessTransactions)).ImplementationType);
    }

    [Fact]
    public void AddContextStore_TheSameStoreTwice_IsRegisteredOnce()
    {
        var services = NewProcessBuilder().AddContextStore<OwnContextStore>().AddContextStore<OwnContextStore>().Services;

        Assert.Equal(typeof(OwnContextStore), Assert.Single(services, x => x.ServiceType == typeof(IProcessContextStore)).ImplementationType);
    }

    // One store per service: a second, different one (by type, instance or factory) is a mistake.
    [Fact]
    public void AddContextStore_WhenAnotherStoreIsRegistered_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        var builder = NewProcessBuilder(services);
        services.AddScoped(_ => Mock.Of<IProcessContextStore>());

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.AddContextStore<OwnContextStore>);

        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, exception.Code);
    }

    [Fact]
    public void AddContextStore_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddContextStore<OwnContextStore>());
    }

    private static IKaleidoBuilder NewProcessBuilder(
        IServiceCollection? services = null,
        IConfiguration? configuration = null,
        KaleidoProcessOptions? options = null,
        Func<Type, bool>? typeFilter = null) =>
        (services ?? new ServiceCollection()).AddKaleido(configuration ?? new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-processor",
            ServiceVersion = "1",
            Assemblies = [typeof(TestStep).Assembly],
            TypeFilter = typeFilter ?? (type => type == typeof(TestStep)),
        }).AddProcess(options);

    private sealed class OwnContextStore : IProcessContextStore
    {
        public Task<Kaleido.Process.Context.ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Kaleido.Process.Context.ProcessContext> SaveAsync(Kaleido.Process.Context.ProcessContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OwnTransactions : IProcessTransactions
    {
        public Task<IProcessTransaction> BeginAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
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

    [ProcessStep(
        Description = "Test step",
        DisplayName = "Test Step")]
    public sealed record TestStep : IProcessStep;

    [ProcessStep(DisplayName = "Secured step", Description = "Step with an explicit authorization rule.")]
    [KaleidoAuthorization(Policy = "explicit-policy")]
    public sealed record SecuredStep : IProcessStep;

    public sealed class SecuredStepHandler : IProcessStepHandler<SecuredStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(SecuredStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

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
