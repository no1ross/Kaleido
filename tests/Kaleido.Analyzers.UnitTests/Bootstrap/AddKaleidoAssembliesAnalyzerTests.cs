using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Bootstrap.AddKaleidoAssembliesAnalyzer>;

namespace Kaleido.Analyzers.Bootstrap.UnitTests;

public sealed class AddKaleidoAssembliesAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Bootstrap.AddKaleidoAssembliesAnalyzer>
{
    protected override global::Kaleido.Analyzers.Bootstrap.AddKaleidoAssembliesAnalyzer CreateSut() =>
        new();

    private const string KaleidoStub = @"
namespace Kaleido
{
    public class KaleidoServiceOptions
    {
        public System.Reflection.Assembly[] Assemblies { get; set; } =
            System.Array.Empty<System.Reflection.Assembly>();
    }
    public class KaleidoServices { }
    public static class KaleidoExtensions
    {
        public static KaleidoServices AddKaleido(
            this KaleidoServices services,
            object config,
            System.Action<KaleidoServiceOptions> configure) => services;
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2009", DiagnosticSeverity.Warning);

    [Fact]
    public async Task Lambda_NoAssemblies_Reports()
    {
        await RunAsync(@"
using Kaleido;
public class Program
{
    public static void Main()
    {
        var services = new KaleidoServices();
        object config = new object();
        {|#0:services.AddKaleido(config, o => { })|};
    }
}
" + KaleidoStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task Lambda_SetsAssembliesArray_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido;
public class Program
{
    public static void Main()
    {
        var services = new KaleidoServices();
        object config = new object();
        services.AddKaleido(config, o =>
        {
            o.Assemblies = new[] { typeof(Program).Assembly };
        });
    }
}
" + KaleidoStub);
    }

    [Fact]
    public async Task Lambda_AssignsAssembliesFromVariable_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido;
public class Program
{
    public static void Main()
    {
        var services = new KaleidoServices();
        object config = new object();
        var assemblies = new[] { typeof(Program).Assembly };
        services.AddKaleido(config, o => { o.Assemblies = assemblies; });
    }
}
" + KaleidoStub);
    }

    [Fact]
    public async Task Lambda_EmptyBody_Reports()
    {
        await RunAsync(@"
using Kaleido;
public class Program
{
    public static void Main()
    {
        var services = new KaleidoServices();
        object config = new object();
        {|#0:services.AddKaleido(config, o => { })|};
    }
}
" + KaleidoStub,
            Expected.WithLocation(0));
    }
}
