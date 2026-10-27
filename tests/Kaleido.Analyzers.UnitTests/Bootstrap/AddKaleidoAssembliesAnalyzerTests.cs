using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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
    public sealed record KaleidoServiceOptions
    {
        public System.Collections.Generic.IReadOnlyList<System.Reflection.Assembly> Assemblies { get; init; }
        public string ServiceName { get; init; }
    }
    public class KaleidoServices { }
    public static class KaleidoExtensions
    {
        public static KaleidoServices AddKaleido(
            this KaleidoServices services,
            object config,
            KaleidoServiceOptions options = null) => services;
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2006", DiagnosticSeverity.Warning);

    [Theory]
    [InlineData("services.AddKaleido(config)")]
    [InlineData("services.AddKaleido(config, null)")]
    [InlineData("services.AddKaleido(config, new())")]
    [InlineData("services.AddKaleido(config, new() { ServiceName = \"orders\" })")]
    [InlineData("services.AddKaleido(config, new KaleidoServiceOptions { ServiceName = \"orders\" })")]
    [InlineData("services.AddKaleido(config, options: new() { ServiceName = \"orders\" })")]
    public async Task Options_WithoutAssemblies_Report(string call)
    {
        await RunAsync(Program("{|#0:" + call + "|};"), Expected.WithLocation(0));
    }

    [Theory]
    [InlineData("services.AddKaleido(config, new() { Assemblies = new[] { typeof(Program).Assembly } });")]
    [InlineData("services.AddKaleido(config, new KaleidoServiceOptions { ServiceName = \"orders\", Assemblies = assemblies });")]
    [InlineData("services.AddKaleido(options: new() { Assemblies = assemblies }, config: config);")]
    public async Task Options_SettingAssemblies_NoDiagnostic(string call)
    {
        await RunAsync(Program(call));
    }

    // Options built elsewhere are not judged: the analyzer cannot see what they hold.
    [Fact]
    public async Task Options_FromAVariable_NoDiagnostic()
    {
        await RunAsync(Program("var options = new KaleidoServiceOptions(); services.AddKaleido(config, options);"));
    }

    private static string Program(string statement) => @"
using Kaleido;
public class Program
{
    public static void Main()
    {
        var services = new KaleidoServices();
        object config = new object();
        var assemblies = new[] { typeof(Program).Assembly };
        " + statement + @"
    }
}
" + KaleidoStub;
}
