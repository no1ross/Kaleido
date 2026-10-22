using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0016 — public and internal async methods (returning Task, Task&lt;T&gt;, ValueTask or ValueTask&lt;T&gt;)
/// must accept a CancellationToken parameter so callers can propagate cancellation.
/// The token has no default value, so every caller passes its own on (#313).
/// Exempt: overrides and interface implementations (the signature is fixed at the
/// interface/base), and the ASP.NET Core InvokeAsync(HttpContext) and BindAsync(HttpContext)
/// conventions (the pipeline passes no CancellationToken; it is HttpContext.RequestAborted).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private const string CancellationTokenTypeName = "System.Threading.CancellationToken";
    private const string HttpContextTypeName = "Microsoft.AspNetCore.Http.HttpContext";
    private const string TaskTypeName = "System.Threading.Tasks.Task";
    private const string ValueTaskTypeName = "System.Threading.Tasks.ValueTask";

    public const string MissingToken =
        "does not accept a CancellationToken — add a 'CancellationToken cancellationToken' parameter so callers can propagate cancellation";

    public const string DefaultedToken =
        "has a CancellationToken with a default value — remove the default, so every caller passes its own on";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.AsyncMissingCancellationToken,
            "Async methods must accept a CancellationToken",
            "Async method '{0}' {1}",
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

        // Skip overrides and interface implementations (explicit or implicit) — the signature is
        // fixed upstream: the interface or base is checked itself, and MA0061 keeps an
        // implementation's default in step with it (e.g. IHealthCheck's defaulted token).
        if (method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterfaceMember(method))
        {
            return;
        }

        // Must return Task or Task<T>.
        if (!ReturnsTask(method.ReturnType))
        {
            return;
        }

        var token = method.Parameters.FirstOrDefault(
            p => p.Type.ToDisplayString() == CancellationTokenTypeName);

        if (token is not null)
        {
            // Every caller passes its token on: no default to fall back on.
            if (token.HasExplicitDefaultValue)
            {
                Report(context, method, DefaultedToken);
            }

            return;
        }

        // Exempt the ASP.NET Core conventions whose signature the framework fixes, with the token on
        // the context (HttpContext.RequestAborted): middleware InvokeAsync(HttpContext) and the
        // minimal-API binding hook static BindAsync(HttpContext).
        if (method.Name is "InvokeAsync" or "BindAsync" &&
            method.Parameters.Length == 1 &&
            method.Parameters[0].Type.ToDisplayString() == HttpContextTypeName)
        {
            return;
        }

        Report(context, method, MissingToken);
    }

    // Report at the method identifier so the location is the name, not the keyword.
    private static void Report(SymbolAnalysisContext context, IMethodSymbol method, string reason)
    {
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
        var location = syntax is MethodDeclarationSyntax decl
            ? decl.Identifier.GetLocation()
            : method.Locations[0];

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name, reason));
    }

    private static bool ImplementsInterfaceMember(IMethodSymbol method) =>
        method.ContainingType is { } type &&
        type.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
            .Any(member => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), method));

    private static bool ReturnsTask(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named)
        {
            return false;
        }

        var original = named.OriginalDefinition.ToDisplayString();

        // Task, Task<T>, ValueTask or ValueTask<T>.
        return original is TaskTypeName or ValueTaskTypeName or
               (TaskTypeName + "<TResult>") or (ValueTaskTypeName + "<TResult>");
    }
}
