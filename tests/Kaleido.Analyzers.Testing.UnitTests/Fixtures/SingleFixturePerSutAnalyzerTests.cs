using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.SingleFixturePerSutAnalyzer>;

namespace Kaleido.Analyzers.Testing.UnitTests;

public sealed class SingleFixturePerSutAnalyzerTests
{
    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL1004", DiagnosticSeverity.Warning);

    [Fact]
    public async Task TwoFixturesSameSutPrefix_Reports()
    {
        // Two classes literally named WidgetTests in different namespaces
        // both map to SUT 'Widget' — second one is flagged
        await RunAsync(@"
public class Widget { }

namespace A
{
    public class WidgetTests
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}

namespace B
{
    public class {|#0:WidgetTests|}
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}" + XunitStub,
            Expected.WithLocation(0).WithArguments("WidgetTests", "WidgetTests", "Widget"));
    }

    [Fact]
    public async Task SingleFixture_NoDiagnostic()
    {
        await RunAsync(@"
public class Widget { }

public class WidgetTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }
}
