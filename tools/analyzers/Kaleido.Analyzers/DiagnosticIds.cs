namespace Kaleido.Analyzers;

internal static class DiagnosticIds
{
    public const string StaticClassExtensions = "KAL0001";
    public const string BclExceptionBan = "KAL0002";
    public const string NullForgivingOperator = "KAL0003";
    public const string ExceptionRecord = "KAL0004";
    public const string InjectableStatic = "KAL0005";
    public const string ServiceInstantiation = "KAL0006";
    public const string ServiceLocator = "KAL0007";
    public const string ConcreteDependency = "KAL0008";
    public const string PropertyInjection = "KAL0009";
    public const string DependencyRetention = "KAL0010";
    public const string ConstructorWork = "KAL0011";
    public const string InfrastructureInstantiation = "KAL0012";
    public const string InjectedDisposal = "KAL0013";
    public const string CaptiveDependency = "KAL0014";

    public const string FixtureNameSuffix = "KAL1001";
    public const string FixtureSutResolution = "KAL1002";
    public const string FixtureStructureMirror = "KAL1003";
    public const string SingleFixturePerSut = "KAL1004";
    public const string BuildServiceProviderOptions = "KAL1005";
}
