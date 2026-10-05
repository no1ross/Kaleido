using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureNameMatchesSutAnalyzer>;

namespace Kaleido.Analyzers.Testing.Fixtures.UnitTests;

public sealed class FixtureNameMatchesSutAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.FixtureNameMatchesSutAnalyzer>
{
    protected override global::Kaleido.Analyzers.Testing.Fixtures.FixtureNameMatchesSutAnalyzer CreateSut() =>
        new();

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
public class Other { }
";

    private static readonly DiagnosticResult Expected =
        new("KAL1007", DiagnosticSeverity.Error);

    [Fact]
    public async Task NameMatchesSut_NoDiagnostic()
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
    public async Task NameMismatchesSut_Reports()
    {
        await RunAsync(@"
public class {|#0:WidgetTests|} : SutFixture<Other>
{
    protected override Other CreateSut() => new Other();
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("WidgetTests", "Other", "OtherTests"));
    }
}
