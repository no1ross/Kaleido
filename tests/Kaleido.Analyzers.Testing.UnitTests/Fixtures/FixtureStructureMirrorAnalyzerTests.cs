using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureStructureMirrorAnalyzer>;

namespace Kaleido.Analyzers.Testing.Fixtures.UnitTests;

public sealed class FixtureStructureMirrorAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.FixtureStructureMirrorAnalyzer>
{
    protected override global::Kaleido.Analyzers.Testing.Fixtures.FixtureStructureMirrorAnalyzer CreateSut() =>
        new();

    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    [Fact]
    public async Task FixtureWithoutSourcePath_Skipped()
    {
        // TestCode files have no real file path — analyzer must not crash
        // and must skip (no path ? no diagnostic)
        await RunAsync(@"
public class Widget { }

public class WidgetTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }

    [Fact]
    public async Task UnresolvablePrefix_Skipped()
    {
        await RunAsync(@"
public class MissingThingTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }

    // #405: the SUT comes from a referenced assembly (as src/ does for every unit-test project); its
    // file is found among the src/ paths passed as additional files.
    private static Kaleido.Testing.AnalyzerTest<FixtureStructureMirrorAnalyzer> MetadataTest(string sutCode, string fixturePath, string fixtureCode, params string[] srcFiles)
    {
        var test = Create("");
        test.TestState.Sources.Clear();
        test.TestState.Sources.Add((fixturePath, fixtureCode + XunitStub));
        test.TestState.ReferenceSources.Add(("Sample", sutCode));

        foreach (var file in srcFiles)
        {
            test.TestState.AdditionalFiles.Add((file, string.Empty));
        }

        return test;
    }

    private const string Widget = "namespace Sample.Things { public class Widget { public int Next() => 1; } }";

    private const string WidgetTests = "public class {|#0:WidgetTests|} { [Xunit.Fact] public void M_S_E() { } }";

    [Fact]
    public async Task AMetadataSut_FixtureInTheMirroredFolder_NoDiagnostic()
    {
        await MetadataTest(Widget, "/repo/tests/Sample.UnitTests/Things/WidgetTests.cs", WidgetTests, "/repo/src/Sample/Things/Widget.cs").RunAsync();
    }

    [Fact]
    public async Task AMetadataSut_FixtureInAnotherFolder_Reports()
    {
        var test = MetadataTest(Widget, "/repo/tests/Sample.UnitTests/Wrong/WidgetTests.cs", WidgetTests, "/repo/src/Sample/Things/Widget.cs", "/repo/src/Sample/Other/Gadget.cs");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("KAL1003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("WidgetTests", System.IO.Path.Combine("Things", "WidgetTests.cs"), System.IO.Path.Combine("Sample", "Things", "Widget.cs")));
        await test.RunAsync();
    }

    [Fact]
    public async Task AMetadataSut_WhoseFileIsNotFound_IsSkipped()
    {
        await MetadataTest(Widget, "/repo/tests/Sample.UnitTests/Wrong/WidgetTests.cs", WidgetTests, "/repo/src/Sample/Things/Gadget.cs").RunAsync();
    }

    // The same file name in two folders: the one matching the namespace is the SUT's.
    [Fact]
    public async Task AMetadataSut_FileNameInTwoFolders_UsesTheNamespacesFolder()
    {
        await MetadataTest(Widget, "/repo/tests/Sample.UnitTests/Things/WidgetTests.cs", WidgetTests, "/repo/src/Sample/Legacy/Widget.cs", "/repo/src/Sample/Things/Widget.cs").RunAsync();
    }

    [Fact]
    public async Task AMetadataGenericTwin_IsFoundByItsArityName()
    {
        const string twins = "namespace Sample.Things { public sealed record Result { } public sealed record Result<T> { } }";

        await MetadataTest(twins, "/repo/tests/Sample.UnitTests/Things/ResultOfTTests.cs", "public class ResultOfTTests { [Xunit.Fact] public void M_S_E() { } }",
            "/repo/src/Sample/Things/Result.cs", "/repo/src/Sample/Things/ResultOfT.cs").RunAsync();
    }
}