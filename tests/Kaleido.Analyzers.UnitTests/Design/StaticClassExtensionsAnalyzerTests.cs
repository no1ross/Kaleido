using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Design.StaticClassExtensionsAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class StaticClassExtensionsAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0001", DiagnosticSeverity.Warning);

    [Fact]
    public async Task StaticClass_WithExtensionMethod_NoDiagnostic()
    {
        await RunAsync(@"
public static class StringExt
{
    public static string Shout(this string s) => s + ""!"";
}");
    }

    [Fact]
    public async Task StaticClass_WithStaticHelper_Reports()
    {
        await RunAsync(@"
public static class {|#0:Helpers|}
{
    public static int Add(int a, int b) => a + b;
}",
            Expected.WithLocation(0).WithArguments("Helpers", "Add"));
    }

    [Fact]
    public async Task StaticClass_ConstantsOnly_NoDiagnostic()
    {
        await RunAsync(@"
public static class ErrorCodes
{
    public const string Foo = ""foo"";
}");
    }

    [Fact]
    public async Task NonStaticClass_NoDiagnostic()
    {
        await RunAsync(@"
public class Helpers
{
    public static int Add(int a, int b) => a + b;
}");
    }
}
