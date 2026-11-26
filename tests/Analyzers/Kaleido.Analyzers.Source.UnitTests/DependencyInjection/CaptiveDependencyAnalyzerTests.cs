using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.CaptiveDependencyAnalyzer>;

namespace Kaleido.Analyzers.Source.DependencyInjection.UnitTests;

public sealed class CaptiveDependencyAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.DependencyInjection.CaptiveDependencyAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.DependencyInjection.CaptiveDependencyAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0013", DiagnosticSeverity.Error);

    private const string Registrations = @"
using Microsoft.Extensions.DependencyInjection;
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
    public static void Add(this Microsoft.Extensions.DependencyInjection.IServiceCollection s)
    {
        s.AddScoped<IScoped, ScopedImpl>();
        s.AddSingleton<ISingleton>(sp =>
            new SingletonImpl({|#0:sp.GetRequiredService<IScoped>()|}));
    }
}",
            Expected.WithLocation(0).WithArguments("IScoped"));
    }

    [Fact]
    public async Task SingletonFactoryResolvingSingleton_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public static class AppServiceCollectionExtensions
{
    public static void Add(this Microsoft.Extensions.DependencyInjection.IServiceCollection s)
    {
        s.AddSingleton<IScoped, ScopedImpl>();
        s.AddSingleton<ISingleton>(sp =>
            new SingletonImpl(sp.GetRequiredService<IScoped>()));
    }
}");
    }
}
