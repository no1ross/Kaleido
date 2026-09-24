using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Kaleido.Analyzers.Testing.UnitTests;

#pragma warning disable KAL0001 // Test-only static factory — not an extension-method host
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
                TestCode = source,
                // KAL1001/1002/1003/1004 gate on assembly name ending with .UnitTests
                SolutionTransforms =
                {
                    (solution, projectId) =>
                    {
                        var project = solution.GetProject(projectId);
                        return project is null
                            ? solution
                            : solution.WithProjectAssemblyName(
                                projectId,
                                (project.AssemblyName ?? "TestProject") + ".UnitTests");
                    }
                }
            };

        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    public static Task RunAsync(
        string source,
        params DiagnosticResult[] expected) =>
        Create(source, expected).RunAsync();
}
