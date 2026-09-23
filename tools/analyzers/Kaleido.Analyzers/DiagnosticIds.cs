namespace Kaleido.Analyzers;

internal static class DiagnosticIds
{
    public const string StaticClassExtensions = "KAL0001";
    public const string BclExceptionBan = "KAL0002";
    public const string NullForgivingOperator = "KAL0003";
    public const string ExceptionRecord = "KAL0004";

    public const string FixtureNameSuffix = "KAL1001";
    public const string FixtureSutResolution = "KAL1002";
    public const string FixtureStructureMirror = "KAL1003";
    public const string SingleFixturePerSut = "KAL1004";
    public const string BuildServiceProviderOptions = "KAL1005";
}
