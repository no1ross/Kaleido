using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.InfrastructureInstantiationAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class InfrastructureInstantiationAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0012", DiagnosticSeverity.Error);

    [Fact]
    public async Task NewHttpClient_Reports()
    {
        await RunAsync(@"
public class Consumer
{
    public void M() { var c = {|#0:new System.Net.Http.HttpClient()|}; }
}",
            Expected.WithLocation(0).WithArguments("HttpClient"));
    }

    [Fact]
    public async Task NewInsideServiceCollectionExtensions_NoDiagnostic()
    {
        await RunAsync(@"
public static class AppServiceCollectionExtensions
{
    public static object Build() => new System.Net.Http.HttpClient();
}");
    }

    [Fact]
    public async Task NewOtherType_NoDiagnostic()
    {
        await RunAsync(@"
public class Consumer
{
    public void M() { var s = new System.Text.StringBuilder(); }
}");
    }
}
