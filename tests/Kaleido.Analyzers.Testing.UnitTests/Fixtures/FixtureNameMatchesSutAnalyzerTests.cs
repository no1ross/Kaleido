using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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
public sealed class Result { }
public sealed class Result<T> { }
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

    // #404: the generic twin's fixture is named by arity.
    [Fact]
    public async Task AGenericTwinsFixture_NamedByArity_NoDiagnostic()
    {
        await RunAsync(@"
public class ResultOfTTests : SutFixture<Result<int>>
{
    protected override Result<int> CreateSut() => new Result<int>();
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs);
    }

    [Fact]
    public async Task AGenericTwinsFixture_WithThePlainName_Reports()
    {
        await RunAsync(@"
public class {|#0:ResultTests|} : SutFixture<Result<int>>
{
    protected override Result<int> CreateSut() => new Result<int>();
    [Xunit.Fact]
    public void M_S_E() { }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("ResultTests", "Result", "ResultOfTTests"));
    }
}