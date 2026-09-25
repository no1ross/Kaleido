using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Design.ExceptionRecordAnalyzer>;

namespace Kaleido.Analyzers.Testing.UnitTests;

public sealed class ExceptionRecordAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL1011", DiagnosticSeverity.Error);

    [Fact]
    public async Task RecordException_Reports()
    {
        // record : Exception produces a cascade of compiler errors (CS0115, CS8867)
        // — suppress them and assert KAL1011 still fires
        var test = Create(@"
public record {|#0:TestException|} : System.Exception;");
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.ExpectedDiagnostics.Add(Expected.WithLocation(0).WithArguments("TestException"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ClassException_NoDiagnostic()
    {
        await RunAsync(@"
public class TestException : System.Exception { }");
    }

    [Fact]
    public async Task RecordNonException_NoDiagnostic()
    {
        await RunAsync(@"
public record TestResult(bool Success);");
    }
}
