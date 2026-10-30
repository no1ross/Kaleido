using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Kaleido.Analyzers.Shared;

/// <summary>
/// Shared by KAL2004 (Kaleido.Analyzers) and KAL0018 (Kaleido.Analyzers.Source): decides whether a
/// catch-all clause (<c>catch</c> or <c>catch (Exception)</c>) can receive an
/// <see cref="System.OperationCanceledException"/> as a general failure.
/// </summary>
/// <remarks>
/// The filter is evaluated over the compiler's operation tree, not its text. For the catch variable it
/// understands type tests (<c>is T</c>, <c>is not T</c>, declaration, recursive, <c>and</c>/<c>or</c>/<c>not</c>
/// patterns), <c>!</c>, <c>&amp;&amp;</c>, <c>||</c> and boolean constants; anything else (method calls,
/// comparisons, <c>GetType()</c>, other variables) is treated as possibly true either way. The clause is safe
/// when the filter is never true for a cancellation, or true only for cancellations (a cancellation handler,
/// like <c>catch (OperationCanceledException)</c>), or when an earlier unfiltered clause catches every
/// cancellation.
/// </remarks>
internal static class CancellationCatch
{
    private const string OperationCanceledExceptionName = "System.OperationCanceledException";

    public static bool SeesCancellationAsFailure(CatchClauseSyntax clause, SemanticModel model, CancellationToken cancellationToken)
    {
        if (model.Compilation.GetTypeByMetadataName(OperationCanceledExceptionName) is not { } cancellation ||
            !IsCatchAll(clause, model, cancellationToken) ||
            HasEarlierCancellationClause(clause, model, cancellation, cancellationToken))
        {
            return false;
        }

        if (clause.Filter is null)
        {
            return true;
        }

        var local = clause.Declaration is null ? null : model.GetDeclaredSymbol(clause.Declaration, cancellationToken);
        var outcome = Evaluate(model.GetOperation(clause.Filter.FilterExpression, cancellationToken), local, cancellation);
        return outcome.CancellationTrue && outcome.OtherTrue;
    }

    private static bool IsCatchAll(CatchClauseSyntax clause, SemanticModel model, CancellationToken cancellationToken) =>
        clause.Declaration is null ||
        model.GetTypeInfo(clause.Declaration.Type, cancellationToken).Type?.ToDisplayString() == "System.Exception";

    // An earlier clause handles the cancellations: an unfiltered catch (OperationCanceledException), or
    // one filtered on a token having been cancelled - catch (OperationCanceledException) when
    // (token.IsCancellationRequested). A later catch-all then only sees the OperationCanceledExceptions
    // no token of the caller raised (a downstream timeout), which are failures (#517).
    private static bool HasEarlierCancellationClause(
        CatchClauseSyntax clause, SemanticModel model, INamedTypeSymbol cancellation, CancellationToken cancellationToken) =>
        clause.Parent is TryStatementSyntax tryStatement &&
        tryStatement.Catches
            .TakeWhile(earlier => earlier != clause)
            .Any(earlier =>
                earlier.Declaration is { } declaration &&
                (earlier.Filter is null || IsTokenCancelled(earlier.Filter, model, cancellationToken)) &&
                model.GetTypeInfo(declaration.Type, cancellationToken).Type is { } type &&
                Covers(type, cancellation));

    private static bool IsTokenCancelled(CatchFilterClauseSyntax filter, SemanticModel model, CancellationToken cancellationToken) =>
        model.GetOperation(filter.FilterExpression, cancellationToken) is IPropertyReferenceOperation
        {
            Property: { Name: "IsCancellationRequested", ContainingType: { } token },
        } &&
        token.ToDisplayString() == "System.Threading.CancellationToken";

    private static Outcome Evaluate(IOperation? operation, ISymbol? local, INamedTypeSymbol cancellation)
    {
        switch (operation)
        {
            case null:
                return Outcome.Unknown;
            case { ConstantValue: { HasValue: true, Value: bool constant } }:
                return new Outcome(constant, !constant, constant, !constant);
            case IConversionOperation { IsImplicit: true } conversion:
                return Evaluate(conversion.Operand, local, cancellation);
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } negation:
                return Evaluate(negation.Operand, local, cancellation).Not();
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } both:
                return Evaluate(both.LeftOperand, local, cancellation).And(Evaluate(both.RightOperand, local, cancellation));
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr } either:
                return Evaluate(either.LeftOperand, local, cancellation).Or(Evaluate(either.RightOperand, local, cancellation));
            case IIsTypeOperation isType when IsCatchVariable(isType.ValueOperand, local):
                var test = TypeTest(isType.TypeOperand, cancellation);
                return isType.IsNegated ? test.Not() : test;
            case IIsPatternOperation isPattern when IsCatchVariable(isPattern.Value, local):
                return Evaluate(isPattern.Pattern, cancellation);
            default:
                return Outcome.Unknown;
        }
    }

    private static Outcome Evaluate(IPatternOperation pattern, INamedTypeSymbol cancellation) =>
        pattern switch
        {
            IDiscardPatternOperation => Outcome.Always,
            ITypePatternOperation type => TypeTest(type.MatchedType, cancellation),
            IDeclarationPatternOperation { MatchesNull: false } declaration => TypeTest(declaration.MatchedType, cancellation),
            IRecursivePatternOperation recursive => TypeTest(recursive.MatchedType, cancellation).MayFail(),
            INegatedPatternOperation negated => Evaluate(negated.Pattern, cancellation).Not(),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And } both =>
                Evaluate(both.LeftPattern, cancellation).And(Evaluate(both.RightPattern, cancellation)),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } either =>
                Evaluate(either.LeftPattern, cancellation).Or(Evaluate(either.RightPattern, cancellation)),
            _ => Outcome.Unknown,
        };

    private static bool IsCatchVariable(IOperation operation, ISymbol? local)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return local is not null &&
            operation is ILocalReferenceOperation reference &&
            SymbolEqualityComparer.Default.Equals(reference.Local, local);
    }

    // What "exception is T" can be, for cancellations and for every other exception.
    private static Outcome TypeTest(ITypeSymbol? type, INamedTypeSymbol cancellation)
    {
        if (type is null || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            return Outcome.Unknown;
        }

        var covers = Covers(type, cancellation);
        var cancellationOrDerived = DerivesFromOrEquals(type, cancellation);
        var coversAll = type.SpecialType == SpecialType.System_Object || type.ToDisplayString() == "System.Exception";

        return new Outcome(
            cancellationTrue: covers || cancellationOrDerived,
            cancellationFalse: !covers,
            otherTrue: !cancellationOrDerived,
            otherFalse: !coversAll);
    }

    // Every OperationCanceledException is a T: T is the type itself or one of its base types.
    private static bool Covers(ITypeSymbol type, INamedTypeSymbol cancellation) =>
        DerivesFromOrEquals(cancellation, type);

    private static bool DerivesFromOrEquals(ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the filter can be true / false for a cancellation, and for any other exception.
    private readonly struct Outcome(bool cancellationTrue, bool cancellationFalse, bool otherTrue, bool otherFalse)
    {
        public static readonly Outcome Unknown = new(true, true, true, true);
        public static readonly Outcome Always = new(true, false, true, false);

        public bool CancellationTrue { get; } = cancellationTrue;

        public bool CancellationFalse { get; } = cancellationFalse;

        public bool OtherTrue { get; } = otherTrue;

        public bool OtherFalse { get; } = otherFalse;

        public Outcome Not() => new(CancellationFalse, CancellationTrue, OtherFalse, OtherTrue);

        public Outcome And(Outcome other) =>
            new(CancellationTrue && other.CancellationTrue, CancellationFalse || other.CancellationFalse,
                OtherTrue && other.OtherTrue, OtherFalse || other.OtherFalse);

        public Outcome Or(Outcome other) =>
            new(CancellationTrue || other.CancellationTrue, CancellationFalse && other.CancellationFalse,
                OtherTrue || other.OtherTrue, OtherFalse && other.OtherFalse);

        // A property sub-pattern can fail even when the type matches.
        public Outcome MayFail() => new(CancellationTrue, true, OtherTrue, true);
    }
}
