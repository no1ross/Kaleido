using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Process;

/// <summary>
/// KAL2007 — [ProcessStep] class names must end in "Step".
/// The framework and remote client mirror types derive the step name by removing the "Step"
/// suffix from the type name. A class without the suffix breaks that convention.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProcessStepSuffixAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "Kaleido.Process.ProcessStepAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ProcessStepSuffix,
            "[ProcessStep] class name must end in 'Step'",
            "[ProcessStep] class '{0}' should be named with a 'Step' suffix (e.g. '{0}Step'). The framework derives the step name by removing the 'Step' suffix from the type name.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Remote client mirror types rely on the 'Step' suffix convention to derive step names at compile time.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken)
                is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        var hasAttribute = false;
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == AttributeFullName)
            {
                hasAttribute = true;
                break;
            }
        }

        if (!hasAttribute)
        {
            return;
        }

        var name = classDecl.Identifier.Text;
        if (name.EndsWith("Step", System.StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), name));
    }
}
