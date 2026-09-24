using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Kaleido.Analyzers.UnitTests;

internal static class AnalyzerTest<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> Create(
        string source,
        params DiagnosticResult[] expected)
    {
        var test =
            new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
            {
                ReferenceAssemblies =
                    ReferenceAssemblies.Net.Net80,
                TestCode = source
            };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    public static Task RunAsync(
        string source,
        params DiagnosticResult[] expected) =>
        Create(source, expected).RunAsync();
}
