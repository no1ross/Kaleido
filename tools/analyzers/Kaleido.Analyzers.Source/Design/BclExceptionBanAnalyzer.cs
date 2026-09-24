using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Kaleido.Analyzers.Source;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0002 — throw Kaleido exceptions, not general BCL exception types
/// (AGENTS.md: "Always use custom exceptions from Kaleido.Exceptions").
/// Argument-guard exceptions and the ASP.NET pipeline 400 contract are allow-listed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BclExceptionBanAnalyzer : DiagnosticAnalyzer
{
    // Allowed BCL exception families — deliberate contracts, not accidental usage:
    //   ArgumentException      — parameter guards (incl. ThrowIf* helpers)
    //   FormatException        — ValueConverter parse-failure contract
    //   BadHttpRequestException— ASP.NET Core maps it to a 400 by design
    private static readonly string[] AllowedBases =
    {
        "System.ArgumentException",
        "System.FormatException",
        "Microsoft.AspNetCore.Http.BadHttpRequestException"
    };

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.BclExceptionBan,
            "Do not throw general BCL exception types",
            "Throw a Kaleido*Exception instead of '{0}'",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeThrow,
            SyntaxKind.ThrowStatement,
            SyntaxKind.ThrowExpression);
    }

    private static void AnalyzeThrow(SyntaxNodeAnalysisContext context)
    {
        ExpressionSyntax? thrown =
            context.Node switch
            {
                ThrowStatementSyntax statement => statement.Expression,
                ThrowExpressionSyntax expression => expression.Expression,
                _ => null
            };

        if (thrown is not ObjectCreationExpressionSyntax creation &&
            thrown is not ImplicitObjectCreationExpressionSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(thrown, context.CancellationToken).Type
                is not INamedTypeSymbol exceptionType)
        {
            return;
        }

        if (!exceptionType.DerivesFrom("System.Exception") &&
            exceptionType.ToDisplayString() != "System.Exception")
        {
            return;
        }

        var fullName = exceptionType.ToDisplayString();

        if (fullName.StartsWith("Kaleido.", System.StringComparison.Ordinal))
        {
            return;
        }

        foreach (var allowed in AllowedBases)
        {
            if (fullName == allowed || exceptionType.DerivesFrom(allowed))
            {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, thrown.GetLocation(), fullName));
    }
}
