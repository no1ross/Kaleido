using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Bootstrap;

/// <summary>
/// KAL2006 - the options passed to AddKaleido() should set Assemblies: they can only be set in code,
/// and missing or empty assemblies fail AddKaleido() at registration. Reported when the options are
/// left out, <c>null</c>, or an object creation (<c>new() { ... }</c>) whose initializer does not set
/// <c>Assemblies</c>; options passed as a variable or built elsewhere are not judged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AddKaleidoAssembliesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.AddKaleidoMissingAssemblies,
            "AddKaleido() options do not set Assemblies",
            "AddKaleido() requires a non-empty Assemblies collection - set Assemblies in the options passed to AddKaleido (new() { Assemblies = [typeof(Program).Assembly] })",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.AddKaleidoMissingAssemblies),
            description: "Assemblies can only be set in code; missing or empty Assemblies causes AddKaleido() to fail at registration, and explicit assemblies make discovery deterministic.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddKaleido" })
        {
            return;
        }

        if (OptionsArgument(invocation.ArgumentList) is { } options && !LeavesOutAssemblies(options))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    // services.AddKaleido(configuration, options): the named "options" argument, else the second one.
    private static ExpressionSyntax? OptionsArgument(ArgumentListSyntax arguments) =>
        arguments.Arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "options")?.Expression
        ?? (arguments.Arguments.Count >= 2 && arguments.Arguments[1].NameColon is null ? arguments.Arguments[1].Expression : null);

    private static bool LeavesOutAssemblies(ExpressionSyntax options) =>
        options switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NullLiteralExpression),
            BaseObjectCreationExpressionSyntax creation => !SetsAssemblies(creation.Initializer),
            _ => false,
        };

    private static bool SetsAssemblies(InitializerExpressionSyntax? initializer) =>
        initializer is not null
        && initializer.Expressions.Any(x =>
            x is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "Assemblies" } });
}
