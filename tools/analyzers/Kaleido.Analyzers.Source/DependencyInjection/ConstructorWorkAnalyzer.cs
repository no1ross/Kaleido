using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.DependencyInjection;

/// <summary>
/// KAL0011 — DI constructors must not perform work on injected dependencies.
/// Assignment, argument guards, self-contained state building (LINQ shaping,
/// private helpers on this), and telemetry are fine; invoking behavior on an
/// injected dependency, I/O, Task.Run, or service resolution inside a
/// registered implementation's constructor is flagged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorWorkAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ConstructorWork,
            "DI constructors must not perform work",
            "Invocation '{0}' inside '{1}'s constructor does work — assign dependencies and validate arguments only",
            "Kaleido.Design",
            DiagnosticSeverity.Error,
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
            nodeContext => AnalyzeConstructor(nodeContext, registrations),
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeConstructor(
        SyntaxNodeAnalysisContext context,
        ServiceRegistrationModel registrations)
    {
        var declaration = (ConstructorDeclarationSyntax)context.Node;

        if (declaration.Body is null ||
            context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
                is not IMethodSymbol ctor ||
            !registrations.Implementations.Contains(
                ctor.ContainingType.OriginalDefinition))
        {
            return;
        }

        foreach (var invocation in declaration.Body.DescendantNodes()
                     .OfType<InvocationExpressionSyntax>())
        {
            var symbol =
                context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
                    .Symbol as IMethodSymbol;

            if (symbol is null || IsAllowedCall(symbol, invocation, context))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    symbol.Name,
                    ctor.ContainingType.Name));
        }
    }

    private static bool IsAllowedCall(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SyntaxNodeAnalysisContext context)
    {
        if (method.ContainingType?.ToDisplayString() is "System.ArgumentNullException" or
                                                          "System.ArgumentException" ||
            method.Name is "ThrowIfNull" or "ThrowIfNullOrEmpty" ||
            // ILogger extension calls are telemetry, not work
            method.ContainingNamespace?.ToDisplayString()
                .StartsWith("Microsoft.Extensions.Logging", System.StringComparison.Ordinal) == true ||
            // pure data-shaping of injected data
            method.ContainingNamespace?.ToDisplayString() == "System.Linq")
        {
            return true;
        }

        // calls on 'this' (implicit or explicit) — self-contained init helpers
        var receiver =
            (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;

        if (receiver is null ||
            receiver.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ThisExpression))
        {
            return true;
        }

        var receiverSymbol =
            context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol;

        // flag invocations on injected dependencies — the constructor's own
        // parameters and the fields/properties that captured them
        if (receiverSymbol is IFieldSymbol or IPropertySymbol ||
            receiverSymbol is IParameterSymbol parameter &&
            parameter.ContainingSymbol is IMethodSymbol containing &&
            containing.MethodKind == MethodKind.Constructor)
        {
            return false;
        }

        // flag starting background work / I/O / service resolution explicitly
        var receiverType =
            context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type;

        if (receiverType?.ToDisplayString() is "System.Threading.Tasks.Task" or
                                               "System.Threading.Thread" or
                                               "System.IServiceProvider" ||
            receiverType?.ToDisplayString()
                .StartsWith("System.IO", System.StringComparison.Ordinal) == true)
        {
            return false;
        }

        return true;
    }
}
