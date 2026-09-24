using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.DependencyInjection;

/// <summary>
/// KAL0013 — container-owned dependencies must not be disposed by consumers.
/// The container owns the lifetime of injected services; calling Dispose()
/// on a field/parameter typed as a registered service (or using it in a
/// using statement) breaks shared lifetimes. Resources the class creates
/// itself are unaffected.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectedDisposalAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.InjectedDisposal,
            "Do not dispose container-owned dependencies",
            "'{0}' on '{1}' disposes a container-owned dependency ('{2}') — the container manages its lifetime",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var registrations =
            ServiceConventions.Harvest(
                context.Compilation, context.CancellationToken);

        if (registrations.Implementations.Count == 0)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeInvocation(nodeContext, registrations),
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeUsing(nodeContext, registrations),
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.LocalDeclarationStatement);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax access ||
            access.Name.Identifier.ValueText != "Dispose")
        {
            return;
        }

        var target =
            context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken)
                .Symbol;

        // fields and parameters only — locals created in-scope are self-owned
        if (target is not IFieldSymbol && target is not IParameterSymbol)
        {
            return;
        }

        var targetType = (target as ISymbol) switch
        {
            IFieldSymbol field => field.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };

        if (!IsRegisteredService(targetType, registrations))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                "Dispose",
                target!.Name,
                targetType!.Name));
    }

    private static void AnalyzeUsing(
        SyntaxNodeAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;

        if (declaration.UsingKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None))
        {
            return;
        }

        foreach (var variable in declaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } initializer)
            {
                continue;
            }

            var type =
                context.SemanticModel.GetTypeInfo(initializer, context.CancellationToken)
                    .Type;

            if (!IsRegisteredService(type, registrations))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    variable.GetLocation(),
                    "using",
                    variable.Identifier.ValueText,
                    type!.Name));
        }
    }

    private static bool IsRegisteredService(
        ITypeSymbol? type,
        ServiceRegistrationModel registrations)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var def = named.OriginalDefinition;

        return registrations.Implementations.Contains(def) ||
               registrations.Services.Any(s =>
                   SymbolEqualityComparer.Default.Equals(s, def));
    }
}
