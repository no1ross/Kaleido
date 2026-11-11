using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Process.Registry.UnitTests;

public sealed class ProcessCapabilityDiscoveryExtensionsTests : SutFixture
{
    [Fact]
    public void Discover_WhenProcessStepAttributeOnTypeWithoutInterface_Throws()
    {
        var type = DefineType("Steps.AttributeOnlyStep", implementsStep: false, withAttribute: true);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { type }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("AttributeOnlyStep", ex.Message);
    }

    [Fact]
    public void Discover_WhenProcessStepTypeMissingAttribute_Throws()
    {
        var type = DefineType("Steps.InterfaceOnlyStep", implementsStep: true, withAttribute: false);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { type }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains("InterfaceOnlyStep", ex.Message);
    }

    [Theory]
    [InlineData(" ", "Description", nameof(ProcessStepAttribute.DisplayName))]
    [InlineData("Display", "", nameof(ProcessStepAttribute.Description))]
    public void Discover_WhenRequiredMetadataEmpty_Throws(string displayName, string description, string expectedProperty)
    {
        var type = DefineType("Steps.EmptyMetadataStep", implementsStep: true, withAttribute: true, displayName, description);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { type }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.MissingAttribute, ex.Code);
        Assert.Contains(expectedProperty, ex.Message);
    }

    [Fact]
    public void Discover_WhenTwoStepsShareTypeName_Throws()
    {
        var first = DefineType("First.SharedStep", implementsStep: true, withAttribute: true);
        var second = DefineType("Second.SharedStep", implementsStep: true, withAttribute: true);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { first, second }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.DuplicateStep, ex.Code);
        Assert.Contains("SharedStep", ex.Message);
    }

    [Fact]
    public void Discover_WhenInvalidTypeExcludedByTypeFilter_DoesNotThrow()
    {
        var type = DefineType("Excluded.AttributeOnlyStep", implementsStep: false, withAttribute: true);
        var (steps, handlers) = new[] { type }.DiscoverProcessCapabilities(x => x.Namespace != "Excluded");

        Assert.Empty(steps);
        Assert.Empty(handlers);
    }

    [Fact]
    public void Discover_WhenInformationStepDeclaresOtherProperties_Throws()
    {
        var type = DefineInformationStep("Steps.MixedInformationStep", "MemberId");
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { type }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, ex.Code);
        Assert.Contains("MemberId", ex.Message);
    }

    [Fact]
    public void Discover_WhenAStepHasNoHandler_Throws()
    {
        var type = DefineType("Steps.UnhandledStep", implementsStep: true, withAttribute: true);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { type }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.MissingHandler, ex.Code);
        Assert.Contains("Steps.UnhandledStep", ex.Message);
    }

    [Fact]
    public void Discover_WhenAStepHasTwoHandlers_Throws()
    {
        var step = DefineType("Steps.DoublyHandledStep", implementsStep: true, withAttribute: true);
        var first = DefineHandler("Handlers.FirstHandler", step);
        var second = DefineHandler("Handlers.SecondHandler", step);
        var ex = Assert.Throws<KaleidoConfigurationException>(() => new[] { step, first, second }.DiscoverProcessCapabilities(null));

        Assert.Equal(ProcessorErrorCodes.InvalidHandler, ex.Code);
        Assert.Contains("Handlers.FirstHandler", ex.Message);
        Assert.Contains("Handlers.SecondHandler", ex.Message);
    }

    [Fact]
    public void Discover_ReturnsStepAndHandlerMapping()
    {
        var step = DefineType("Steps.HandledStep", implementsStep: true, withAttribute: true);
        var handler = DefineHandler("Handlers.HandledStepHandler", step);
        var (steps, handlers) = new[] { step, handler }.DiscoverProcessCapabilities(null);

        Assert.Equal([step], steps);
        Assert.Equal(handler, handlers[step]);
    }

    private static System.Reflection.Emit.ModuleBuilder NewModule() =>
        System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new System.Reflection.AssemblyName($"DynamicSteps_{Guid.NewGuid():N}"),
            System.Reflection.Emit.AssemblyBuilderAccess.RunAndCollect).DefineDynamicModule("Steps");

    private static Type DefineType(string fullName, bool implementsStep, bool withAttribute, string displayName = "Dynamic step", string description = "Dynamically defined step.")
    {
        var typeBuilder = NewModule().DefineType(fullName, System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class | System.Reflection.TypeAttributes.Sealed);
        if (implementsStep)
        {
            typeBuilder.AddInterfaceImplementation(typeof(IProcessStep));
        }

        if (withAttribute)
        {
            SetAttribute(typeBuilder, displayName, description);
        }

        return typeBuilder.CreateType();
    }

    private static void SetAttribute(System.Reflection.Emit.TypeBuilder typeBuilder, string displayName, string description)
    {
        var attributeType = typeof(ProcessStepAttribute);
        typeBuilder.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(
            attributeType.GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException("ProcessStepAttribute constructor not found."),
            [],
            [attributeType.GetProperty(nameof(ProcessStepAttribute.DisplayName))!, attributeType.GetProperty(nameof(ProcessStepAttribute.Description))!],
            [displayName, description]));
    }

    // Emits a concrete IProcessStepHandler<TStep> whose ExecuteAsync throws NotImplementedException.
    private static Type DefineHandler(string fullName, Type stepType)
    {
        var handlerInterface = typeof(IProcessStepHandler<>).MakeGenericType(stepType);
        var typeBuilder = NewModule().DefineType(fullName, System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class | System.Reflection.TypeAttributes.Sealed);
        typeBuilder.AddInterfaceImplementation(handlerInterface);
        typeBuilder.DefineDefaultConstructor(System.Reflection.MethodAttributes.Public);
        var execute = typeBuilder.DefineMethod(
            nameof(IProcessStepHandler<IProcessStep>.ExecuteAsync),
            System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual | System.Reflection.MethodAttributes.Final |
            System.Reflection.MethodAttributes.NewSlot | System.Reflection.MethodAttributes.HideBySig,
            typeof(Task<ProcessStepHandlerResult>),
            [stepType, typeof(ProcessStepContext), typeof(CancellationToken)]);
        var il = execute.GetILGenerator();
        il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(NotImplementedException).GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException("NotImplementedException constructor not found."));
        il.Emit(System.Reflection.Emit.OpCodes.Throw);
        typeBuilder.DefineMethodOverride(execute, handlerInterface.GetMethod(nameof(IProcessStepHandler<IProcessStep>.ExecuteAsync))
            ?? throw new InvalidOperationException("ExecuteAsync not found."));
        return typeBuilder.CreateType();
    }

    // Emits an IInformationStep (with the interface's init-only properties) plus an extra property.
    private static Type DefineInformationStep(string fullName, string extraProperty)
    {
        var typeBuilder = NewModule().DefineType(fullName, System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class | System.Reflection.TypeAttributes.Sealed);
        typeBuilder.AddInterfaceImplementation(typeof(IInformationStep));
        typeBuilder.DefineDefaultConstructor(System.Reflection.MethodAttributes.Public);
        DefineProperty(typeBuilder, nameof(IInformationStep.InformationRequestId), typeof(string), implementsInterface: true);
        DefineProperty(typeBuilder, nameof(IInformationStep.ExchangeId), typeof(string), implementsInterface: true);
        DefineProperty(typeBuilder, nameof(IInformationStep.Items), typeof(IReadOnlyList<InformationResponseItem>), implementsInterface: true);
        DefineProperty(typeBuilder, extraProperty, typeof(string), implementsInterface: false);
        SetAttribute(typeBuilder, "Dynamic information step", "Dynamically defined information step.");
        return typeBuilder.CreateType();
    }

    private static void DefineProperty(System.Reflection.Emit.TypeBuilder typeBuilder, string name, Type type, bool implementsInterface)
    {
        const System.Reflection.MethodAttributes accessorAttributes =
            System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual | System.Reflection.MethodAttributes.Final |
            System.Reflection.MethodAttributes.NewSlot | System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.SpecialName;
        var field = typeBuilder.DefineField($"_{name}", type, System.Reflection.FieldAttributes.Private);
        var property = typeBuilder.DefineProperty(name, System.Reflection.PropertyAttributes.None, type, null);
        var getter = typeBuilder.DefineMethod($"get_{name}", accessorAttributes, type, Type.EmptyTypes);
        var getIl = getter.GetILGenerator();
        getIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        getIl.Emit(System.Reflection.Emit.OpCodes.Ldfld, field);
        getIl.Emit(System.Reflection.Emit.OpCodes.Ret);
        var setter = typeBuilder.DefineMethod($"set_{name}", accessorAttributes, System.Reflection.CallingConventions.HasThis,
            typeof(void), [typeof(System.Runtime.CompilerServices.IsExternalInit)], null, [type], null, null);
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
}
