using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.ServiceLocatorAnalyzer>;

namespace Kaleido.Analyzers.Source.DependencyInjection.UnitTests;

public sealed class ServiceLocatorAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.DependencyInjection.ServiceLocatorAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.DependencyInjection.ServiceLocatorAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0007", DiagnosticSeverity.Error);

    private const string References = @"
using Microsoft.Extensions.DependencyInjection;
public interface IFoo { }
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceProviderServiceExtensions
    {
        public static T GetRequiredService<T>(this System.IServiceProvider provider) => default;
        public static object GetRequiredService(this System.IServiceProvider provider, System.Type type) => null;
    }
    public interface IServiceScope : System.IDisposable
    {
        System.IServiceProvider ServiceProvider { get; }
    }
    public interface IServiceScopeFactory
    {
        IServiceScope CreateScope();
    }
}
";

    [Fact]
    public async Task GetRequiredService_OnClosedType_Reports()
    {
        await RunAsync(References + @"
public sealed class Consumer
{
    private readonly System.IServiceProvider provider;
    public Consumer(System.IServiceProvider provider) => this.provider = provider;
    public void M() { var x = {|#0:provider.GetRequiredService<IFoo>()|}; }
}",
            Expected.WithLocation(0).WithArguments("GetRequiredService"));
    }

    [Fact]
    public async Task GetRequiredService_OnRuntimeType_NoDiagnostic()
    {
        await RunAsync(References + @"
public sealed class Consumer
{
    private readonly System.IServiceProvider provider;
    public Consumer(System.IServiceProvider provider) => this.provider = provider;
    public void M(System.Type handlerType) { var x = provider.GetRequiredService(handlerType); }
}");
    }

    [Fact]
    public async Task GetRequiredService_FromScopeCreatedInSameMethod_NoDiagnostic()
    {
        await RunAsync(References + @"
public sealed class Invoker
{
    private readonly IServiceScopeFactory scopeFactory;
    public Invoker(IServiceScopeFactory scopeFactory) => this.scopeFactory = scopeFactory;
    public void M()
    {
        using var scope = scopeFactory.CreateScope();
        var x = scope.ServiceProvider.GetRequiredService<IFoo>();
    }
}");
    }

    [Fact]
    public async Task GetRequiredService_FromInjectedScope_Reports()
    {
        await RunAsync(References + @"
public sealed class Consumer
{
    private readonly IServiceScope scope;
    public Consumer(IServiceScope scope) => this.scope = scope;
    public void M() { var x = {|#0:scope.ServiceProvider.GetRequiredService<IFoo>()|}; }
}",
            Expected.WithLocation(0).WithArguments("GetRequiredService"));
    }

    [Fact]
    public async Task GetRequiredService_InsideServiceCollectionExtensions_NoDiagnostic()
    {
        await RunAsync(References + @"
public static class AppServiceCollectionExtensions
{
    public static object Build(System.IServiceProvider provider) =>
        provider.GetRequiredService<IFoo>();
}");
    }
}
