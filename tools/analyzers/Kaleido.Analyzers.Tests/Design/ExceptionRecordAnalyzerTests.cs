using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Tests.AnalyzerTest<
    Kaleido.Analyzers.ExceptionRecordAnalyzer>;

namespace Kaleido.Analyzers.Tests;

public sealed class ExceptionRecordAnalyzerTests
{
    [Fact]
    public async Task RecordDerivingFromException_Reports()
    {
        // record : Exception produces a cascade of compiler errors (CS0115,
        // CS8867) — filter them out and assert KAL0004 still fires
        var test = Create(@"
public record {|#0:BadException|} : System.Exception;");
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("KAL0004", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("BadException"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ClassDerivingFromException_NoDiagnostic()
    {
        await RunAsync(@"
public class GoodException : System.Exception;");
    }

    [Fact]
    public async Task PlainRecord_NoDiagnostic()
    {
        await RunAsync(@"
public record Data(string Name);");
    }
}
