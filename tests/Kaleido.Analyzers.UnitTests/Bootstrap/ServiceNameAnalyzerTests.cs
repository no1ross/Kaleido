using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Bootstrap.ServiceNameAnalyzer>;

namespace Kaleido.Analyzers.Bootstrap.UnitTests;

public sealed class ServiceNameAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Bootstrap.ServiceNameAnalyzer>
{
    protected override global::Kaleido.Analyzers.Bootstrap.ServiceNameAnalyzer CreateSut() =>
        new();

    private const string KaleidoOptionsStub = @"
namespace Kaleido
{
    public class KaleidoServiceOptionsBuilder
    {
        public string ServiceName { get; set; } = string.Empty;
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2005", DiagnosticSeverity.Warning);

    private static readonly DiagnosticResult TooLong =
        new("KAL2020", DiagnosticSeverity.Warning);

    private static string Assign(string expression, string members = "") => @"
public class Program
{
" + members + @"
    public void Configure(Kaleido.KaleidoServiceOptionsBuilder o)
    {
        o.ServiceName = " + expression + @";
    }
}
" + KaleidoOptionsStub;

    [Theory]
    [MemberData(nameof(ServiceNameCases.Valid), MemberType = typeof(ServiceNameCases))]
    public async Task ValidServiceName_NoDiagnostic(string serviceName)
    {
        await RunAsync(Assign(SymbolDisplay.FormatLiteral(serviceName, quote: true)));
    }

    [Theory]
    [MemberData(nameof(ServiceNameCases.Invalid), MemberType = typeof(ServiceNameCases))]
    public async Task InvalidServiceName_Reports(string serviceName)
    {
        await RunAsync(
            Assign("{|#0:" + SymbolDisplay.FormatLiteral(serviceName, quote: true) + "|}"),
            Expected.WithLocation(0));
    }

    [Theory]
    [InlineData("MyService", "myservice")]
    [InlineData("My Svc.V2", "mysvcv2")]
    [InlineData("-intake", "intake")]
    [InlineData("..", "myservice")]
    public async Task InvalidServiceName_SuggestsAValidName(string serviceName, string suggestion)
    {
        await RunAsync(
            Assign("{|#0:\"" + serviceName + "\"|}"),
            Expected.WithLocation(0).WithArguments(serviceName, suggestion));
    }

    [Fact]
    public async Task ConstField_IsChecked()
    {
        await RunAsync(
            Assign("{|#0:Name|}", "    private const string Name = \"My-Service\";"),
            Expected.WithLocation(0).WithArguments("My-Service", "my-service"));
    }

    [Fact]
    public async Task ConstantInterpolation_IsChecked()
    {
        await RunAsync(
            Assign("{|#0:$\"{Prefix}.events\"|}", "    private const string Prefix = \"intake\";"),
            Expected.WithLocation(0).WithArguments("intake.events", "intakeevents"));
    }

    [Fact]
    public async Task Nameof_IsChecked()
    {
        await RunAsync(
            Assign("{|#0:nameof(Program)|}"),
            Expected.WithLocation(0).WithArguments("Program", "program"));
    }

    [Fact]
    public async Task ObjectInitializer_IsChecked()
    {
        await RunAsync(@"
public class Program
{
    public Kaleido.KaleidoServiceOptionsBuilder Create() =>
        new Kaleido.KaleidoServiceOptionsBuilder { ServiceName = {|#0:""My Service""|} };
}
" + KaleidoOptionsStub,
            Expected.WithLocation(0).WithArguments("My Service", "myservice"));
    }

    [Fact]
    public async Task NonConstantValue_NoDiagnostic()
    {
        await RunAsync(Assign("System.Environment.GetEnvironmentVariable(\"SERVICE\") ?? \"svc\""));
    }

    [Fact]
    public async Task ServiceNameOfAnotherType_NoDiagnostic()
    {
        await RunAsync(@"
public class OtherOptions
{
    public string ServiceName { get; set; } = string.Empty;
}

public class Program
{
    public void Configure(OtherOptions o)
    {
        o.ServiceName = ""My Service"";
    }
}");
    }

    [Fact]
    public async Task ServiceNameOverFifteenCharacters_Reports()
    {
        await RunAsync(
            Assign("{|#0:\"priorauthorizations\"|}"),
            TooLong.WithLocation(0).WithArguments("priorauthorizations", 19, 15));
    }

    [Fact]
    public async Task ServiceNameOfFifteenCharacters_NoDiagnostic()
    {
        await RunAsync(Assign("\"configurationsx\""));
    }
}
