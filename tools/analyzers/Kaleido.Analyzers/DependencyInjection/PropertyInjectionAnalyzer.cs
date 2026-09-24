using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0009 — services use constructor injection only. A registered
/// implementation with a settable service-typed property (or a Set* method
/// injecting a service) hides a dependency behind mutable state.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.PropertyInjection,
            "Services must use constructor injection",
            "'{0}' on service '{1}' injects '{2}' outside the constructor — use constructor injection",
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

        context.RegisterSymbolAction(
            symbolContext => AnalyzeType(symbolContext, registrations),
            SymbolKind.NamedType);
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!registrations.Implementations.Contains(type.OriginalDefinition))
        {
            return;
        }

        var services =
            registrations.Services.ToList();

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.SetMethod is null ||
                property.SetMethod.IsInitOnly ||
                property.Type is not INamedTypeSymbol propertyType ||
                !services.Any(s => SymbolEqualityComparer.Default.Equals(
                    s, propertyType.OriginalDefinition)))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    property.Locations[0],
                    property.Name,
                    type.Name,
                    propertyType.Name));
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.MethodKind != MethodKind.Ordinary ||
                !method.ReturnsVoid ||
                method.Parameters.Length != 1 ||
                !method.Name.StartsWith("Set", System.StringComparison.Ordinal) ||
                method.Parameters[0].Type is not INamedTypeSymbol paramType ||
                !services.Any(s => SymbolEqualityComparer.Default.Equals(
                    s, paramType.OriginalDefinition)))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    method.Locations[0],
                    method.Name,
                    type.Name,
                    paramType.Name));
        }
    }
}
