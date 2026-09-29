using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Http.MapRegistryWithoutClientsAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Http;

public sealed class MapRegistryWithoutClientsAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Http.MapRegistryWithoutClientsAnalyzer>
{
    protected override global::Kaleido.Analyzers.Http.MapRegistryWithoutClientsAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
public static class EndpointExtensions
{
    public static object MapRegistry(this object app) => app;
}
public static class KaleidoExtensions
{
    public static object AddHttpClients(this object builder) => builder;
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2006", DiagnosticSeverity.Info);

    [Fact]
    public async Task MapRegistry_WithoutAddHttpClients_Reports()
    {
        await RunAsync(@"
public class Program
{
    public static void Main()
    {
        object app = new object();
        {|#0:app.MapRegistry()|};
    }
}
" + KaleidoStubs,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task MapRegistry_WithAddHttpClients_NoDiagnostic()
    {
        await RunAsync(@"
public class Program
{
    public static void Main()
    {
        object builder = new object();
        object app = new object();
        builder.AddHttpClients();
        app.MapRegistry();
    }
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task NoMapRegistry_NoDiagnostic()
    {
        await RunAsync(@"
public class Program
{
    public static void Main()
    {
        object builder = new object();
        builder.AddHttpClients();
    }
}
" + KaleidoStubs);
    }
}
