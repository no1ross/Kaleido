using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Tests.AnalyzerTest<
    Kaleido.Analyzers.ServiceLocatorAnalyzer>;

namespace Kaleido.Analyzers.Tests;

public sealed class ServiceLocatorAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0007", DiagnosticSeverity.Warning);

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
