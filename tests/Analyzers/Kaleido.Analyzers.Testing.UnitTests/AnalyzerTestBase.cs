using Kaleido.Testing;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.UnitTests;

#pragma warning disable KAL0001 // Test-only static factory — not an extension-method host
internal static class AnalyzerTest<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Kaleido.Testing.AnalyzerTest<TAnalyzer> Create(
        string source,
        params DiagnosticResult[] expected)
    {
        var test =
            new Kaleido.Testing.AnalyzerTest<TAnalyzer>
            {
                TestCode = source,
                // KAL1001/1002/1003/1004 gate on assembly name ending with .UnitTests
                AssemblyName = "TestProject.UnitTests"
            };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    public static Task RunAsync(
        string source,
        params DiagnosticResult[] expected) =>
        Create(source, expected).RunAsync();
}
