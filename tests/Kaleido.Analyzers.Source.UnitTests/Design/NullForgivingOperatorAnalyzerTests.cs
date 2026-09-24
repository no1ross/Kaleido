using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.NullForgivingOperatorAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class NullForgivingOperatorAnalyzerTests
{
    [Fact]
    public async Task SuppressionOperator_Reports()
    {
        await RunAsync(@"
#nullable enable
public class C
{
    public void M()
    {
        string? s = null;
        var x = s{|#0:!|};
        _ = x;
    }
}",
            new DiagnosticResult("KAL0003", DiagnosticSeverity.Warning)
                .WithLocation(0));
    }

    [Fact]
    public async Task NullCoalescing_NoDiagnostic()
    {
        await RunAsync(@"
#nullable enable
public class C
{
    public void M()
    {
        string? s = null;
        var x = s ?? """";
        _ = x;
    }
}");
    }
}
