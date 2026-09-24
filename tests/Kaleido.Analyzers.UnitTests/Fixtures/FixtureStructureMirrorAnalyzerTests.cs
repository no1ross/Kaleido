using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Fixtures.FixtureStructureMirrorAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class FixtureStructureMirrorAnalyzerTests
{
    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    [Fact]
    public async Task FixtureWithoutSourcePath_Skipped()
    {
        // TestCode files have no real file path — analyzer must not crash
        // and must skip (no path → no diagnostic)
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
