using Microsoft.CodeAnalysis;
using Kaleido.Testing;
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
        // TestCode files have no real file path � analyzer must not crash
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
}
