using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureEmptyAnalyzer>;

namespace Kaleido.Analyzers.Testing.UnitTests;

public sealed class FixtureEmptyAnalyzerTests
{
    private const string XunitStubs = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
    public class TheoryAttribute : System.Attribute { }
}
";

    private static readonly DiagnosticResult Expected =
        new("KAL1010", DiagnosticSeverity.Warning);

    [Fact]
    public async Task FixtureWithFact_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests
{
    [Xunit.Fact]
    public void M_S_E() { }
}" + XunitStubs);
    }

    [Fact]
    public async Task FixtureWithTheory_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests
{
    [Xunit.Theory]
    public void M_S_E(int x) { }
}" + XunitStubs);
    }

    [Fact]
    public async Task FixtureWithMethodsButNoTestAttributes_Reports()
    {
        await RunAsync(@"
public class {|#0:WidgetTests|}
{
    public void Helper() { }
    public void AnotherHelper() { }
}" + XunitStubs,
            Expected.WithLocation(0).WithArguments("WidgetTests"));
    }

    [Fact]
    public async Task EmptyClassEndingInTests_NoDiagnostic()
    {
        // No ordinary methods at all — not a fixture stub, just an empty class
        await RunAsync(@"
public class WidgetTests
{
}" + XunitStubs);
    }

    [Fact]
    public async Task AbstractClassEndingInTests_NoDiagnostic()
    {
        await RunAsync(@"
public abstract class WidgetTests
{
    public void Helper() { }
}" + XunitStubs);
    }

    [Fact]
    public async Task ClassNotEndingInTests_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetHelper
{
    public void DoSomething() { }
}" + XunitStubs);
    }
}
