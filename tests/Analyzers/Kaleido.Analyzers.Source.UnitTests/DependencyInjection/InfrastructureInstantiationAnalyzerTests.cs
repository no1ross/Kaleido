using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.InfrastructureInstantiationAnalyzer>;

namespace Kaleido.Analyzers.Source.DependencyInjection.UnitTests;

public sealed class InfrastructureInstantiationAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.DependencyInjection.InfrastructureInstantiationAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.DependencyInjection.InfrastructureInstantiationAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0011", DiagnosticSeverity.Error);

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
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddApp(this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
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
