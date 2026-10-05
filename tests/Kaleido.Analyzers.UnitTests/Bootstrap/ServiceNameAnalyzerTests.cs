using Microsoft.CodeAnalysis;
using Kaleido.Testing;
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
    public class KaleidoServiceOptions
    {
        public string ServiceName { get; set; } = string.Empty;
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2005", DiagnosticSeverity.Warning);

    [Fact]
    public async Task UppercaseServiceName_Reports()
    {
        await RunAsync(@"
public class Program
{
    public void Configure(Kaleido.KaleidoServiceOptions o)
    {
        o.ServiceName = {|#0:""MyService""|};
    }
}
" + KaleidoOptionsStub,
            Expected.WithLocation(0).WithArguments("MyService", "myservice"));
    }

    [Fact]
    public async Task SpaceInServiceName_Reports()
    {
        await RunAsync(@"
public class Program
{
    public void Configure(Kaleido.KaleidoServiceOptions o)
    {
        o.ServiceName = {|#0:""my service""|};
    }
}
" + KaleidoOptionsStub,
            Expected.WithLocation(0).WithArguments("my service", "myservice"));
    }

    [Fact]
    public async Task HyphenInServiceName_Reports()
    {
        await RunAsync(@"
public class Program
{
    public void Configure(Kaleido.KaleidoServiceOptions o)
    {
        o.ServiceName = {|#0:""my-service""|};
    }
}
" + KaleidoOptionsStub,
            Expected.WithLocation(0).WithArguments("my-service", "myservice"));
    }

    [Fact]
    public async Task LowercaseServiceName_NoDiagnostic()
    {
        await RunAsync(@"
public class Program
{
    public void Configure(Kaleido.KaleidoServiceOptions o)
    {
        o.ServiceName = ""intake"";
    }
}
" + KaleidoOptionsStub);
    }

    [Fact]
    public async Task ValidServiceName_NoDiagnostic()
    {
        await RunAsync(@"
public class Program
{
    public void Configure(Kaleido.KaleidoServiceOptions o)
    {
        o.ServiceName = ""priorauth"";
    }
}
" + KaleidoOptionsStub);
    }
}
