using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0019 — public and internal async methods (returning Task or Task&lt;T&gt;)
/// must accept a CancellationToken parameter so callers can propagate cancellation.
/// Exempt: overrides and explicit interface implementations (token is fixed at
/// the interface/base), and the ASP.NET Core middleware InvokeAsync(HttpContext)
/// convention (the pipeline itself does not pass a CancellationToken).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";
    private const string HttpContextTypeName = "Microsoft.AspNetCore.Http.HttpContext";
    private const string TaskTypeName = "System.Threading.Tasks.Task";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.AsyncMissingCancellationToken,
            "Async methods should accept a CancellationToken",
            "Async method '{0}' does not accept a CancellationToken — add 'CancellationToken cancellationToken = default' so callers can propagate cancellation",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Only public or internal methods.
        if (method.DeclaredAccessibility is not
            (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return;
        }

        // Skip overrides and explicit interface implementations — type is fixed upstream.
        if (method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0)
        {
            return;
        }

        // Must return Task or Task<T>.
        if (!ReturnsTask(method.ReturnType))
        {
            return;
        }

        // Already has a CancellationToken parameter.
        if (method.Parameters.Any(
                p => p.Type.ToDisplayString() == CancellationTokenTypeName))
        {
            return;
        }

        // Exempt the ASP.NET Core middleware InvokeAsync(HttpContext) convention.
        if (method.Name == "InvokeAsync" &&
            method.Parameters.Length == 1 &&
            method.Parameters[0].Type.ToDisplayString() == HttpContextTypeName)
        {
            return;
        }

        // Report at the method identifier so the location is the name, not the keyword.
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
        var location = syntax is MethodDeclarationSyntax decl
            ? decl.Identifier.GetLocation()
            : method.Locations[0];

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name));
    }

    private static bool ReturnsTask(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named)
        {
            return false;
        }

        var original = named.OriginalDefinition.ToDisplayString();

        // Task (non-generic) or Task<T> (generic).
        return original == TaskTypeName ||
               original == TaskTypeName + "<TResult>";
    }
}
