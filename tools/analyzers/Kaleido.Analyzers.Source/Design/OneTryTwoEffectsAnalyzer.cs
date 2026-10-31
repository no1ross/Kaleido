using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0022 - one <c>try</c> covers an operation with an effect and, after it, another operation on a
/// different dependency, and its <c>catch</c> handles the failure (#537, ADR 0034). The catch cannot
/// tell "the first failed" from "the first succeeded and the next failed", so it compensates, records or
/// reports on the wrong assumption - as the executor did when a failed save after a handler had run was
/// recorded as the step failing. Catch each operation's failure where it happens.
/// </summary>
/// <remarks>
/// <para>
/// An operation is an asynchronous call (it returns a task) on a dependency: a receiver whose type is an
/// interface or an abstract class, outside the <c>System</c> namespaces, and not a logger or an
/// observation. It has an effect unless its name reads as a query (<c>Get</c>, <c>Find</c>, <c>Load</c>,
/// <c>Read</c>, <c>Query</c>, ...). Two operations are on different dependencies when their receivers
/// differ: another field, parameter or local, or the same factory called for another target. Calls are
/// followed into the type's own methods declared in the same file, a few calls deep, so a helper does not
/// hide them.
/// </para>
/// <para>
/// A catch that only observes - logs or calls an observation, then rethrows - handles nothing and is
/// not reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneTryTwoEffectsAnalyzer : DiagnosticAnalyzer
{
    private const int MaxDepth = 4;

    private static readonly string[] QueryPrefixes =
        ["Get", "Find", "Load", "Read", "Query", "List", "Search", "Try", "Is", "Has", "Can", "Contains", "Count", "Wait"];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.OneTryTwoEffects,
            "One try covering an effect and a later operation must not handle their failure as one",
            "This try covers '{0}' and, after it, '{1}', and its catch handles the failure: a failure of '{1}' after '{0}' succeeded would be handled as if '{0}' had failed. Catch each operation's failure where it happens.",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TryStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var tryStatement = (TryStatementSyntax)context.Node;

        if (context.ContainingSymbol?.ContainingType is not { } type ||
            !tryStatement.Catches.Any(x => Handles(x, context.SemanticModel, context.CancellationToken)))
        {
            return;
        }

        var operations = new List<Operation>();
        Walk(tryStatement.Block, context.SemanticModel, type, operations, [], new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default), 0, context.CancellationToken);

        for (var i = 0; i < operations.Count; i++)
        {
            if (!operations[i].HasEffect)
            {
                continue;
            }

            var effect = operations[i];

            if (operations.Skip(i + 1).FirstOrDefault(x => x.Dependency != effect.Dependency && !Alternatives(effect, x)) is { } next)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, tryStatement.TryKeyword.GetLocation(), effect.Name, next.Name));
                return;
            }
        }
    }

    // The operations the node reaches, in order, following the type's own methods (in this file). Each
    // carries the branch arms it is in, from the try down, so alternatives are not taken for a sequence.
    private static void Walk(SyntaxNode node, SemanticModel model, INamedTypeSymbol type, List<Operation> operations, IReadOnlyList<Arm> path, HashSet<IMethodSymbol> visited, int depth, CancellationToken cancellationToken)
    {
        foreach (var invocation in node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            // An operation whose failure is caught where it happens is handled there (the remedy).
            if (CaughtWithin(invocation, node) ||
                model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            var armsHere = path.Concat(Arms(invocation, node)).ToArray();

            if (AsOperation(invocation, method, model, armsHere, cancellationToken) is { } operation)
            {
                operations.Add(operation);
                continue;
            }

            if (depth < MaxDepth &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, type) &&
                visited.Add(method.OriginalDefinition))
            {
                foreach (var body in method.OriginalDefinition.DeclaringSyntaxReferences
                             .Where(x => x.SyntaxTree == node.SyntaxTree)
                             .Select(x => x.GetSyntax(cancellationToken)))
                {
                    Walk(body, model, type, operations, armsHere, visited, depth + 1, cancellationToken);
                }
            }
        }
    }

    // The invocation is inside a try with a catch nested in the walked node.
    private static bool CaughtWithin(SyntaxNode invocation, SyntaxNode root) =>
        invocation.Ancestors()
            .TakeWhile(x => x != root)
            .OfType<TryStatementSyntax>()
            .Any(x => x.Catches.Count > 0 && x.Block.Span.Contains(invocation.Span));

    // The branch arms the node is in, below the root: the branches of an if, the sections of a switch,
    // the arms of a switch expression and of ?:.
    private static IEnumerable<Arm> Arms(SyntaxNode node, SyntaxNode root)
    {
        var child = node;

        // Up to the root itself: a statement directly in the walked block has its early exits there too.
        foreach (var ancestor in node.Ancestors().TakeWhile(x => x != root).Concat(node == root ? [] : [root]))
        {
            var arm = ancestor switch
            {
                IfStatementSyntax branch when child == branch.Statement => new Arm(branch, 0),
                IfStatementSyntax branch when child == branch.Else => new Arm(branch, 1),
                SwitchStatementSyntax choice when child is SwitchSectionSyntax section => new Arm(choice, choice.Sections.IndexOf(section)),
                SwitchExpressionSyntax choice when child is SwitchExpressionArmSyntax option => new Arm(choice, choice.Arms.IndexOf(option)),
                ConditionalExpressionSyntax choice when child == choice.WhenTrue => new Arm(choice, 0),
                ConditionalExpressionSyntax choice when child == choice.WhenFalse => new Arm(choice, 1),
                _ => null,
            };

            if (arm is not null)
            {
                yield return arm;
            }

            // An earlier if in the block that always leaves (return, throw) makes what follows it the
            // other arm: if (x) { await A(); return; } await B(); runs A or B, never both.
            if (ancestor is BlockSyntax block && child is StatementSyntax statement)
            {
                foreach (var exit in block.Statements.TakeWhile(x => x != statement).OfType<IfStatementSyntax>().Where(x => x.Else is null && AlwaysLeaves(x.Statement)))
                {
                    yield return new Arm(exit, 1);
                }
            }

            child = ancestor;
        }
    }

    private static bool AlwaysLeaves(StatementSyntax statement) =>
        statement switch
        {
            ReturnStatementSyntax or ThrowStatementSyntax => true,
            BlockSyntax { Statements.Count: > 0 } block => AlwaysLeaves(block.Statements.Last()),
            _ => false,
        };

    // Two operations in different arms of the same branch never both run.
    private static bool Alternatives(Operation first, Operation second) =>
        first.Path.Any(a => second.Path.Any(b => a.Construct == b.Construct && a.Index != b.Index));

    // An asynchronous call on a dependency, or null.
    private static Operation? AsOperation(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel model, IReadOnlyList<Arm> path, CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access ||
            !ReturnsTask(method) ||
            model.GetTypeInfo(access.Expression, cancellationToken).Type is not { } receiverType ||
            !IsDependency(receiverType))
        {
            return null;
        }

        // The receiver tells the dependencies apart: its symbol (a field, a parameter, a local), else
        // its text (a factory called for a given target).
        var dependency = model.GetSymbolInfo(access.Expression, cancellationToken).Symbol is { Kind: SymbolKind.Field or SymbolKind.Parameter or SymbolKind.Local or SymbolKind.Property } symbol
            ? symbol.ToDisplayString()
            : access.Expression.ToString();

        return new Operation(
            dependency,
            $"{access.Expression}.{method.Name}",
            !QueryPrefixes.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal)),
            path);
    }

    private static bool ReturnsTask(IMethodSymbol method) =>
        method.ReturnType.OriginalDefinition.ToDisplayString() is
            "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";

    private static bool IsDependency(ITypeSymbol type) =>
        (type.TypeKind == TypeKind.Interface || type.IsAbstract) &&
        !IsObserver(type) &&
        type.ContainingNamespace?.ToDisplayString() is { } ns &&
        ns != "System" && !ns.StartsWith("System.", StringComparison.Ordinal);

    // Logging and observability record what happens; they are not the work.
    private static bool IsObserver(ITypeSymbol? type) =>
        type?.Name is { } name &&
        (name.Contains("Observ") || name.Contains("Logger") || name.Contains("Telemetry"));

    // A catch handles the failure unless all it does is observe (log, record an observation) and
    // rethrow, or it catches only a cancellation of a token (#517): that is not the operations failing.
    private static bool Handles(CatchClauseSyntax clause, SemanticModel model, CancellationToken cancellationToken)
    {
        if (clause.Filter is { } filter &&
            model.GetOperation(filter.FilterExpression, cancellationToken) is Microsoft.CodeAnalysis.Operations.IPropertyReferenceOperation
            {
                Property: { Name: "IsCancellationRequested", ContainingType: { } token },
            } &&
            token.ToDisplayString() == "System.Threading.CancellationToken")
        {
            return false;
        }

        var statements = clause.Block.Statements;

        if (statements.Count == 0 || statements.Last() is not ThrowStatementSyntax)
        {
            return true;
        }

        return statements.Take(statements.Count - 1).Any(statement => !Observes(statement, model, cancellationToken));
    }

    private static bool Observes(StatementSyntax statement, SemanticModel model, CancellationToken cancellationToken) =>
        statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } &&
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method &&
        (method.Name.StartsWith("Log", StringComparison.Ordinal) ||
         IsObserver(method.ContainingType) ||
         (invocation.Expression is MemberAccessExpressionSyntax access && IsObserver(model.GetTypeInfo(access.Expression, cancellationToken).Type)));

    private sealed class Operation(string dependency, string name, bool hasEffect, IReadOnlyList<Arm> path)
    {
        public string Dependency { get; } = dependency;

        public string Name { get; } = name;

        public bool HasEffect { get; } = hasEffect;

        public IReadOnlyList<Arm> Path { get; } = path;
    }

    private sealed class Arm(SyntaxNode construct, int index)
    {
        public SyntaxNode Construct { get; } = construct;

        public int Index { get; } = index;
    }
}
