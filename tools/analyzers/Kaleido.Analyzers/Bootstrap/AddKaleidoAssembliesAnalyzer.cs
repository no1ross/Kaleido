using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Bootstrap;

/// <summary>
/// KAL2006 - the options passed to AddKaleido() should set Assemblies: they can only be set in code,
/// and missing or empty assemblies fail AddKaleido() at registration. Reported when the options are
/// left out, <c>null</c>, or an object creation (<c>new() { ... }</c>) whose initializer does not set
/// <c>Assemblies</c>; options passed as a variable or built elsewhere are not judged.
/// </summary>
/// <remarks>
/// Judged by symbol (#465): only a call that binds to Kaleido's
/// <c>KaleidoServiceCollectionExtensions.AddKaleido</c>, and only an assignment to the real
/// <c>KaleidoServiceOptions.Assemblies</c> property counts - a consumer's own method named
/// <c>AddKaleido</c>, or a different member named <c>Assemblies</c>, is not mistaken for them.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AddKaleidoAssembliesAnalyzer : DiagnosticAnalyzer
{
    private const string ExtensionsTypeName = "Kaleido.KaleidoServiceCollectionExtensions";
    private const string OptionsTypeName = "Kaleido.KaleidoServiceOptions";

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
        context.RegisterCompilationStartAction(start =>
        {
            var extensions = start.Compilation.GetTypeByMetadataName(ExtensionsTypeName);
            var options = start.Compilation.GetTypeByMetadataName(OptionsTypeName);

            if (extensions is null || options is null)
            {
                return;
            }

            var assemblies = options.GetMembers("Assemblies").OfType<IPropertySymbol>().FirstOrDefault();

            start.RegisterOperationAction(
                operationContext => Analyze(operationContext, extensions, options, assemblies),
                OperationKind.Invocation);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol extensions,
        INamedTypeSymbol options,
        IPropertySymbol? assemblies)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;

        if (method.Name != "AddKaleido" || !SymbolEqualityComparer.Default.Equals(method.ContainingType, extensions))
        {
            return;
        }

        var argument = invocation.Arguments.FirstOrDefault(x =>
            x.Parameter is not null && SymbolEqualityComparer.Default.Equals(x.Parameter.Type.OriginalDefinition, options));

        if (argument is null || LeavesOutAssemblies(argument, assemblies))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation()));
        }
    }

    // Left out (the parameter's default), null, or created here without setting Assemblies. Options
    // from a variable, a method or a with-expression are not judged: what they hold is not visible.
    private static bool LeavesOutAssemblies(IArgumentOperation argument, IPropertySymbol? assemblies)
    {
        if (argument.ArgumentKind == ArgumentKind.DefaultValue)
        {
            return true;
        }

        var value = argument.Value;

        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        return value switch
        {
            ILiteralOperation { ConstantValue: { HasValue: true, Value: null } } => true,
            IObjectCreationOperation creation => !SetsAssemblies(creation.Initializer, assemblies),
            _ => false,
        };
    }

    private static bool SetsAssemblies(IObjectOrCollectionInitializerOperation? initializer, IPropertySymbol? assemblies) =>
        initializer is not null
        && initializer.Initializers.Any(x =>
            x is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation target }
            && SymbolEqualityComparer.Default.Equals(target.Property.OriginalDefinition, assemblies));
}
