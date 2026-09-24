using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Fixtures.FixtureNameSuffixAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class FixtureNameSuffixAnalyzerTests
{
    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL1001", DiagnosticSeverity.Warning);

    [Fact]
    public async Task FixtureEndingInTests_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub);
    }

    [Fact]
    public async Task FixtureNotEndingInTests_Reports()
    {
        await RunAsync(@"
public class {|#0:WidgetSuite|}
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStub,
            Expected.WithLocation(0).WithArguments("WidgetSuite", "WidgetSuite"));
    }

    [Fact]
    public async Task ClassWithoutTestMethods_NoDiagnostic()
    {
        await RunAsync(@"
public class RandomName
{
    public void Helper() { }
}" + XunitStub);
    }
}
