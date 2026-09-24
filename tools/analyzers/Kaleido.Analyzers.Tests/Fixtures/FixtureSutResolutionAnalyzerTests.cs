using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Tests.AnalyzerTest<
    Kaleido.Analyzers.FixtureSutResolutionAnalyzer>;

namespace Kaleido.Analyzers.Tests;

public sealed class FixtureSutResolutionAnalyzerTests
{
    private const string XunitStub = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL1002", DiagnosticSeverity.Warning);

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
}
