using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Fixtures.BuildServiceProviderAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class BuildServiceProviderAnalyzerTests
{
    private const string Preamble = @"
using Microsoft.Extensions.DependencyInjection;
";

    private const string DiStub = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }

    public class ServiceProviderOptions
    {
        public bool ValidateScopes { get; set; }
        public bool ValidateOnBuild { get; set; }
    }

    public static class ServiceProviderServiceExtensions
    {
        public static object BuildServiceProvider(this IServiceCollection services) => null!;
        public static object BuildServiceProvider(this IServiceCollection services, ServiceProviderOptions options) => null!;
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL1005", DiagnosticSeverity.Warning);

    [Fact]
    public async Task BuildServiceProvider_NoArgs_Reports()
    {
        await RunAsync(Preamble + @"
public class C
{
    public void M(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        var p = {|#0:services.BuildServiceProvider()|};
    }
}" + DiStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task BuildServiceProvider_WithValidationOptions_NoDiagnostic()
    {
        await RunAsync(Preamble + @"
public class C
{
    public void M(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        var p = services.BuildServiceProvider(
            new Microsoft.Extensions.DependencyInjection.ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }
}" + DiStub);
    }

    [Fact]
    public async Task BuildServiceProvider_OnlyValidateScopes_Reports()
    {
        await RunAsync(Preamble + @"
public class C
{
    public void M(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        var p = {|#0:services.BuildServiceProvider(
            new Microsoft.Extensions.DependencyInjection.ServiceProviderOptions
            {
                ValidateScopes = true
            })|};
    }
}" + DiStub,
            Expected.WithLocation(0));
    }
}
