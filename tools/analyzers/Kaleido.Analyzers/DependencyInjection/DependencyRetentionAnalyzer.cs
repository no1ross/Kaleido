using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0010 — injected dependencies must be retained safely. Flags on
/// registered service implementations: (a) a mutable (non-readonly) field
/// typed as a registered service, and (b) a primary-constructor parameter
/// typed as a registered service that is never referenced by any member —
/// a dependency that was accepted but ignored.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependencyRetentionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor MutableFieldRule =
        new(
            DiagnosticIds.DependencyRetention,
            "Injected dependency fields must be readonly",
            "Field '{0}' on service '{1}' holds '{2}' — make it readonly",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnusedParameterRule =
        new(
            DiagnosticIds.DependencyRetention,
            "Injected dependency is never used",
            "Constructor parameter '{0}' on service '{1}' is never used — remove it or use it",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MutableFieldRule, UnusedParameterRule);

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

        context.RegisterSymbolAction(
            symbolContext => AnalyzeType(symbolContext, registrations),
            SymbolKind.NamedType);
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class ||
            !registrations.Implementations.Contains(type.OriginalDefinition))
        {
            return;
        }

        var services = registrations.Services.ToList();

        var isServiceType = (ITypeSymbol t) =>
            t is INamedTypeSymbol named &&
            services.Any(s => SymbolEqualityComparer.Default.Equals(
                s, named.OriginalDefinition));

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsReadOnly || field.IsImplicitlyDeclared || field.IsConst ||
                !isServiceType(field.Type))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    MutableFieldRule,
                    field.Locations[0],
                    field.Name,
                    type.Name,
                    field.Type.Name));
        }

        // primary-ctor params that are never referenced
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.IsImplicitlyDeclared || ctor.DeclaringSyntaxReferences.Length == 0)
            {
                continue;
            }

            foreach (var parameter in ctor.Parameters)
            {
                if (!isServiceType(parameter.Type) || IsReferenced(parameter, type, context))
                {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        UnusedParameterRule,
                        parameter.Locations[0],
                        parameter.Name,
                        type.Name));
            }
        }
    }

    private static bool IsReferenced(
        IParameterSymbol parameter,
        INamedTypeSymbol type,
        SymbolAnalysisContext context)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var typeDecl = reference.GetSyntax(context.CancellationToken);

            foreach (var identifier in typeDecl.DescendantNodes()
                         .OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText != parameter.Name)
                {
                    continue;
                }

                // inside a member body or initializer (not the parameter list)
                if (identifier.Ancestors().Any(a =>
                        a is MethodDeclarationSyntax or PropertyDeclarationSyntax or
                            AccessorDeclarationSyntax or AnonymousFunctionExpressionSyntax or
                            FieldDeclarationSyntax or EqualsValueClauseSyntax or
                            ConstructorDeclarationSyntax))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
