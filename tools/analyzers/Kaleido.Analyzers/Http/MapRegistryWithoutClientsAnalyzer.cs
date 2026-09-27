using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Http;

/// <summary>
/// KAL2006 — MapRegistry() requires AddHttpClients() to be registered in the same compilation.
/// This is a best-effort heuristic: if MapRegistry() is found and AddHttpClients() is not, warn.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MapRegistryWithoutClientsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.MapRegistryWithoutClients,
            "MapRegistry() called without AddHttpClients()",
            "MapRegistry() is called but AddHttpClients() was not found in this compilation. The registry endpoint requires the Kaleido HTTP client infrastructure to be registered.",
            "Kaleido.Usage",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "AddHttpClients() registers the client factories that MapRegistry() depends on to discover downstream processors and queryables.",
            customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var mapRegistryLocations = new ConcurrentBag<Location>();
        var addHttpClientsFound = new ConcurrentBag<bool>();

        context.RegisterSyntaxNodeAction(
            nodeContext =>
            {
                var invocation = (InvocationExpressionSyntax)nodeContext.Node;
                var name = GetMethodName(invocation);

                if (name == "MapRegistry")
                {
                    mapRegistryLocations.Add(invocation.GetLocation());
                }
                else if (name == "AddHttpClients")
                {
                    addHttpClientsFound.Add(true);
                }
            },
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(endContext =>
        {
            if (!addHttpClientsFound.IsEmpty)
            {
                return;
            }

            foreach (var location in mapRegistryLocations)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        });
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };
}
