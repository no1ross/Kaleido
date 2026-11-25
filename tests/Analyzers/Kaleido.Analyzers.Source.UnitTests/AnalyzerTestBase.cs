using Kaleido.Testing;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.UnitTests;

internal static class AnalyzerTest<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    // The registration API the DI rules recognize by symbol (#465), in its real namespace: a stub
    // of Microsoft.Extensions.DependencyInjection.Abstractions, compiled as a reference assembly.
    private const string DependencyInjectionStub = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services) => services;
        public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services) => services;
        public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services) => services;
        public static IServiceCollection AddScoped<TService>(this IServiceCollection services, System.Func<System.IServiceProvider, TService> factory) => services;
        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, System.Func<System.IServiceProvider, TService> factory) => services;
        public static IServiceCollection AddTransient<TService>(this IServiceCollection services, System.Func<System.IServiceProvider, TService> factory) => services;
    }
}";

    public static Kaleido.Testing.AnalyzerTest<TAnalyzer> Create(
        string source,
        params DiagnosticResult[] expected)
    {
        var test =
            new Kaleido.Testing.AnalyzerTest<TAnalyzer>
            {
                TestCode = source
            };

        test.TestState.ReferenceSources.Add(("Microsoft.Extensions.DependencyInjection.Abstractions", DependencyInjectionStub));

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    public static Task RunAsync(
        string source,
        params DiagnosticResult[] expected) =>
        Create(source, expected).RunAsync();
}
