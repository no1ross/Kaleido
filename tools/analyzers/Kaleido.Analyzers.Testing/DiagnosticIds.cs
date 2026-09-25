namespace Kaleido.Analyzers.Testing;

internal static class DiagnosticIds
{
    // Test fixture rules (KAL1xxx) — apply to unit-test projects.

    /// <summary>Test fixture class names must end with the 'Tests' suffix.</summary>
    public const string FixtureNameSuffix = "KAL1001";

    /// <summary>The prefix of a fixture name (before 'Tests') must resolve to an actual type in the compilation; fixtures named after scenarios rather than a SUT are flagged.</summary>
    public const string FixtureSutResolution = "KAL1002";

    /// <summary>A fixture file must mirror the SUT's directory structure: src/{Project}/{path}/Sut.cs maps to tests/{TestProject}/{path}/SutTests.cs.</summary>
    public const string FixtureStructureMirror = "KAL1003";

    /// <summary>Only one fixture class per subject under test; two fixtures resolving to the same SUT type are flagged.</summary>
    public const string SingleFixturePerSut = "KAL1004";

    /// <summary>Test-built ServiceProviders must pass ValidateScopes = true and ValidateOnBuild = true to BuildServiceProvider() to catch captive dependencies at build time.</summary>
    public const string BuildServiceProviderOptions = "KAL1005";

    /// <summary>Every concrete unit-test fixture must inherit SutFixture&lt;TSut&gt; to declare its subject under test explicitly.</summary>
    public const string FixtureMustInheritSutFixture = "KAL1006";

    /// <summary>A fixture that inherits SutFixture&lt;TSut&gt; must be named exactly {TSut.Name}Tests so the name encodes the subject.</summary>
    public const string FixtureNameMatchesSut = "KAL1007";

    /// <summary>The SUT type may only be constructed inside the CreateSut() method; new-ing it elsewhere creates per-test construction drift.</summary>
    public const string SutConstruction = "KAL1008";

    /// <summary>Every testable source type (public/internal, non-static, non-abstract class with behavior) must have a corresponding {TypeName}Tests fixture in the unit-test project.</summary>
    public const string FixtureCoverage = "KAL1009";

    /// <summary>A fixture class named {Type}Tests exists but contains no [Fact] or [Theory] test methods — it is an empty stub.</summary>
    public const string FixtureEmpty = "KAL1010";

    /// <summary>Exception types must not be declared as records; record value-equality and copy semantics are meaningless and harmful on exception types.</summary>
    public const string ExceptionRecord = "KAL1011";
}
