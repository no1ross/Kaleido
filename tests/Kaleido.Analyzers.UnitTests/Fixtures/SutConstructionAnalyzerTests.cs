using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Fixtures.SutConstructionAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class SutConstructionAnalyzerTests
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
public class Widget
{
    public Widget(object dependency) { }
}
public class Dependency { }
";

    private static readonly DiagnosticResult Expected =
        new("KAL1008", DiagnosticSeverity.Warning);

    [Fact]
    public async Task NewInCreateSut_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Widget>
{
    protected override Widget CreateSut() => new Widget(new Dependency());
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs);
    }

    [Fact]
    public async Task NewInTestMethod_Reports()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Widget>
{
    protected override Widget CreateSut() => new Widget(new Dependency());
    [Xunit.Fact]
    public void M_S_E() { var sut = {|#0:new Widget(new Dependency())|}; }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("Widget"));
    }

    [Fact]
    public async Task NewOfOtherTypeInTestMethod_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Widget>
{
    protected override Widget CreateSut() => new Widget(new Dependency());
    [Xunit.Fact]
    public void M_S_E() { var dep = new Dependency(); }
}" + Stubs);
    }
}
