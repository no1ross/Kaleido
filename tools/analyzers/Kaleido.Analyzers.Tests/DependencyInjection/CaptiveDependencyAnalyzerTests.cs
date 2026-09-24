using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Tests.AnalyzerTest<
    Kaleido.Analyzers.CaptiveDependencyAnalyzer>;

namespace Kaleido.Analyzers.Tests;

public sealed class CaptiveDependencyAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0014", DiagnosticSeverity.Warning);

    private const string Registrations = @"
public interface IScoped { }
public interface ISingleton { }
public class ScopedImpl : IScoped { }
public class SingletonImpl : ISingleton
{
    public SingletonImpl(IScoped scoped) { }
}
public static class ServiceProviderServiceExtensions
{
    public static T GetRequiredService<T>(this System.IServiceProvider provider) => default;
}
";

    [Fact]
    public async Task SingletonFactoryResolvingScoped_Reports()
    {
        await RunAsync(Registrations + @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.AddScoped<IScoped, ScopedImpl>();
        s.AddSingleton<ISingleton>(sp =>
            new SingletonImpl({|#0:sp.GetRequiredService<IScoped>()|}));
    }
}
public static class ServiceCollectionExtensionsForObject
{
    public static void AddScoped<TService, TImpl>(this object s) { }
    public static void AddSingleton<TService>(this object s, System.Func<System.IServiceProvider, TService> f) { }
}",
            Expected.WithLocation(0).WithArguments("IScoped"));
    }

    [Fact]
    public async Task SingletonFactoryResolvingSingleton_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.AddSingleton<IScoped, ScopedImpl>();
        s.AddSingleton<ISingleton>(sp =>
            new SingletonImpl(sp.GetRequiredService<IScoped>()));
    }
}
public static class ServiceCollectionExtensionsForObject
{
    public static void AddSingleton<TService, TImpl>(this object s) { }
    public static void AddSingleton<TService>(this object s, System.Func<System.IServiceProvider, TService> f) { }
}");
    }
}
