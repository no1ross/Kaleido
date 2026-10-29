using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0021 - a wall-clock read that decides behavior (a lease, an expiry, a retry, a TTL...) reads the
/// injected <c>TimeProvider</c> (#501). Time is then a production dependency of the component, made
/// explicit. A read that is only recorded - a timestamp stored, assigned or passed on - is not a
/// clock dependency and is not reported: testability alone does not justify the abstraction.
/// </summary>
/// <remarks>
/// A read decides when its value is an operand of a comparison or of date arithmetic (<c>+</c>,
/// <c>-</c>, <c>Add*</c>, <c>Subtract</c>, <c>CompareTo</c>, <c>Compare</c>), directly, through a member
/// of it (<c>UtcTicks</c>), or through a local it initializes. A branch of <c>?:</c> or <c>??</c> passes
/// the value on unchanged.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WallClockDecisionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.WallClockDecision,
            "A wall-clock read that decides behavior uses the injected TimeProvider",
            "'{0}' decides behavior here - read the time from the injected TimeProvider",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.PropertyReference);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var read = (IPropertyReferenceOperation)context.Operation;

        if (!IsWallClock(read.Property) || read.SemanticModel is not { } model)
        {
            return;
        }

        var (decides, end) = Follow(read);

        if (decides || (StoredLocal(end) is { } local && LocalDecides(local, read, model, context.CancellationToken)))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, read.Syntax.GetLocation(), $"{read.Property.ContainingType.Name}.{read.Property.Name}"));
        }
    }

    private static bool IsWallClock(IPropertySymbol property) =>
        property.IsStatic &&
        (property.ContainingType.ToDisplayString(), property.Name) is
            ("System.DateTimeOffset", "UtcNow" or "Now") or
            ("System.DateTime", "UtcNow" or "Now" or "Today");

    // Walks up from the value: true when it reaches a decision; otherwise where the value ends up.
    private static (bool Decides, IOperation? End) Follow(IOperation current) =>
        current.Parent switch
        {
            IConversionOperation or IParenthesizedOperation => Follow(current.Parent),
            IPropertyReferenceOperation member when ReferenceEquals(member.Instance, current) => Follow(member),
            IConditionalOperation conditional when !ReferenceEquals(conditional.Condition, current) => Follow(conditional),
            ICoalesceOperation coalesce when !ReferenceEquals(coalesce.Value, current) => Follow(coalesce),
            IBinaryOperation binary => (IsDecision(binary.OperatorKind), binary),
            IInvocationOperation call when ReferenceEquals(call.Instance, current) => (IsArithmeticOrComparison(call.TargetMethod.Name), call),
            IArgumentOperation { Parent: IInvocationOperation call } => (call.TargetMethod.IsStatic && call.TargetMethod.Name == "Compare", call),
            var parent => (false, parent),
        };

    private static bool IsDecision(BinaryOperatorKind kind) =>
        kind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or
            BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals or
            BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual or
            BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual;

    private static bool IsArithmeticOrComparison(string method) =>
        method.StartsWith("Add", System.StringComparison.Ordinal) ||
        method is "Subtract" or "CompareTo";

    // The local the value is stored in (`var now = DateTimeOffset.UtcNow;`, `now = ...`), if any.
    private static ILocalSymbol? StoredLocal(IOperation? end) =>
        end switch
        {
            IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
            ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } => target.Local,
            _ => null,
        };

    // Whether any use of the local, in the body that declares it (the file, for top-level statements),
    // decides something.
    private static bool LocalDecides(ILocalSymbol local, IOperation read, SemanticModel model, System.Threading.CancellationToken cancellationToken)
    {
        var body = read.Syntax.FirstAncestorOrSelf<SyntaxNode>(node =>
                node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            ?? read.Syntax.SyntaxTree.GetRoot(cancellationToken);

        return body.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == local.Name)
            .Select(name => model.GetOperation(name, cancellationToken))
            .OfType<ILocalReferenceOperation>()
            .Any(use => SymbolEqualityComparer.Default.Equals(use.Local, local) && Follow(use).Decides);
    }
}
