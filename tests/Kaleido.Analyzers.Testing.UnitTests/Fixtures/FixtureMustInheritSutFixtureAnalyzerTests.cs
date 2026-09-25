using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureMustInheritSutFixtureAnalyzer>;

namespace Kaleido.Analyzers.Testing.UnitTests;

public sealed class FixtureMustInheritSutFixtureAnalyzerTests
{
    private const string Stubs = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}
public abstract class SutFixture<TSut> where TSut : class
{
    protected abstract TSut CreateSut();
}
public class Widget { }
";

    private static readonly DiagnosticResult Expected =
        new("KAL1006", DiagnosticSeverity.Error);

    [Fact]
    public async Task FixtureInheritingSutFixture_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Widget>
{
    protected override Widget CreateSut() => new Widget();
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs);
    }

    [Fact]
    public async Task FixtureNotInheriting_Reports()
    {
        await RunAsync(@"
public class {|#0:WidgetTests|}
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("WidgetTests", "Widget"));
    }

    [Fact]
    public async Task ClassWithoutTestMethods_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetHelper
{
    public void Helper() { }
}" + Stubs);
    }
}
