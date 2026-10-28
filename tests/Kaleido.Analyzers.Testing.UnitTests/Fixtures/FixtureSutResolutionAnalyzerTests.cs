using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureSutResolutionAnalyzer>;

namespace Kaleido.Analyzers.Testing.Fixtures.UnitTests;

public sealed class FixtureSutResolutionAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.FixtureSutResolutionAnalyzer>
{
    protected override global::Kaleido.Analyzers.Testing.Fixtures.FixtureSutResolutionAnalyzer CreateSut() =>
        new();

    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL1002", DiagnosticSeverity.Error);

    [Fact]
    public async Task PrefixResolves_NoDiagnostic()
    {
        await RunAsync(@"
public class Widget { }

public class WidgetTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }

    [Fact]
    public async Task PrefixDoesNotResolve_Reports()
    {
        await RunAsync(@"
public class {|#0:MissingThingTests|}
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub,
            Expected.WithLocation(0).WithArguments("MissingThingTests", "MissingThing"));
    }

    // #404: a generic twin's fixture carries the arity.
    [Theory]
    [InlineData("ResultTests")]
    [InlineData("ResultOfTTests")]
    [InlineData("ResultOfT2Tests")]
    public async Task GenericTwinFixtures_ResolveByArity(string fixture)
    {
        await RunAsync(@"
public sealed record Result { }
public sealed record Result<T> { }
public sealed record Result<T1, T2> { }

public class " + fixture + @"
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }

    [Fact]
    public async Task AnArityTheTypeDoesNotHave_Reports()
    {
        await RunAsync(@"
public sealed record Result { }
public sealed record Result<T> { }

public class {|#0:ResultOfT3Tests|}
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub,
            Expected.WithLocation(0).WithArguments("ResultOfT3Tests", "ResultOfT3"));
    }
}
