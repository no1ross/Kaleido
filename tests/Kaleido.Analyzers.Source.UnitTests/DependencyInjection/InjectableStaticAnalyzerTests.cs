using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.InjectableStaticAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class InjectableStaticAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0005", DiagnosticSeverity.Warning);

    [Fact]
    public async Task StaticServiceClass_Reports()
    {
        await RunAsync(@"
public static class {|#0:OrderService|}
{
    public static void Execute() { }
}",
            Expected.WithLocation(0).WithArguments("OrderService", "Execute"));
    }

    [Fact]
    public async Task StaticFactoryWithHelpers_Reports()
    {
        await RunAsync(@"
public static class {|#0:ResponseFactory|}
{
    public static object Build() => new object();
}",
            Expected.WithLocation(0).WithArguments("ResponseFactory", "Build"));
    }

    [Fact]
    public async Task StaticExtensionOnlyClass_NoDiagnostic()
    {
        await RunAsync(@"
public static class StringServiceExtensions
{
    public static int Length(this string s) => s.Length;
}");
    }

    [Fact]
    public async Task StaticClassWithoutServiceSuffix_NoDiagnostic()
    {
        await RunAsync(@"
public static class HeaderNames
{
    public static string Normalize(string s) => s;
}");
    }

    [Fact]
    public async Task InstanceServiceClass_NoDiagnostic()
    {
        await RunAsync(@"
public sealed class OrderService
{
    public void Execute() { }
}");
    }
}
