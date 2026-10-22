using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Design;

/// <summary>
/// KAL0015 — public and internal API members (properties, method return types,
/// method parameters) must not expose mutable collection types.
/// Use IReadOnlyCollection&lt;T&gt;, IReadOnlyList&lt;T&gt;, IReadOnlyDictionary&lt;K,V&gt;,
/// or IEnumerable&lt;T&gt; instead of List&lt;T&gt;, IList&lt;T&gt;, Dictionary&lt;K,V&gt;,
/// IDictionary&lt;K,V&gt;, HashSet&lt;T&gt;, ISet&lt;T&gt;, or ICollection&lt;T&gt; - and
/// FrozenDictionary&lt;K,V&gt; / FrozenSet&lt;T&gt; (immutable, fast lookups) for collections
/// built once and only read. A collection that is filled after construction is written through
/// methods, not exposed mutable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableCollectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.MutableCollectionInPublicApi,
            "Do not expose mutable collections in public APIs",
            "'{0}' is a mutable collection type — use IReadOnlyCollection<T>, IReadOnlyList<T>, IReadOnlyDictionary<K,V> or IEnumerable<T>, or FrozenDictionary<K,V>/FrozenSet<T> for a collection built once and only read",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    // Fully-qualified original-definition names of the mutable generic types to ban.
    private static readonly string[] MutableTypes =
    [
        "System.Collections.Generic.List<T>",
        "System.Collections.Generic.IList<T>",
        "System.Collections.Generic.Dictionary<TKey, TValue>",
        "System.Collections.Generic.IDictionary<TKey, TValue>",
        "System.Collections.Generic.HashSet<T>",
        "System.Collections.Generic.ISet<T>",
        "System.Collections.Generic.ICollection<T>",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeNode,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.Parameter);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case PropertyDeclarationSyntax property:
                AnalyzeProperty(context, property);
                break;
            case MethodDeclarationSyntax method:
                AnalyzeMethod(context, method);
                break;
            case ParameterSyntax parameter:
                AnalyzeParameter(context, parameter);
                break;
        }
    }

    private static void AnalyzeProperty(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property)
    {
        if (!IsAccessibleMember(context, property) || IsInheritedContract(context, property))
        {
            return;
        }

        CheckTypeSyntax(context, property.Type);
    }

    private static void AnalyzeMethod(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method)
    {
        if (!IsAccessibleMember(context, method) || IsInheritedContract(context, method))
        {
            return;
        }

        CheckTypeSyntax(context, method.ReturnType);
    }

    // Ownership: the diagnostic belongs to the member that declares the contract. An override, or an
    // explicit or implicit interface implementation, cannot choose its type - the base or interface
    // fixed it - so it is never reported; the declaring member is (when it is source in this
    // compilation), once.
    private static bool IsInheritedContract(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken);

        return symbol switch
        {
            IPropertySymbol property => property.IsOverride || property.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterfaceMember(property),
            IMethodSymbol method => method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterfaceMember(method),
            _ => false,
        };
    }

    private static bool ImplementsInterfaceMember(ISymbol member) =>
        member.ContainingType is { } type
        && type.AllInterfaces
            .SelectMany(x => x.GetMembers(member.Name))
            .Any(x => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(x), member));

    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter)
    {
        if (parameter.Type is null)
        {
            return;
        }

        // Only parameters of accessible public/internal methods on accessible types.
        if (parameter.Parent is not ParameterListSyntax { Parent: MethodDeclarationSyntax method })
        {
            return;
        }

        if (!IsAccessibleMember(context, method) || IsInheritedContract(context, method))
        {
            return;
        }

        CheckTypeSyntax(context, parameter.Type);
    }

    private static bool IsAccessibleMember(
        SyntaxNodeAnalysisContext context,
        MemberDeclarationSyntax member)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken);

        if (symbol is null)
        {
            return false;
        }

        if (symbol.DeclaredAccessibility is not
            (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return false;
        }

        var containingType = symbol.ContainingType;

        if (containingType is null || containingType.IsStatic)
        {
            return false;
        }

        return containingType.DeclaredAccessibility is
            Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
    }

    private static void CheckTypeSyntax(
        SyntaxNodeAnalysisContext context,
        TypeSyntax typeSyntax)
    {
        var typeInfo = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken);

        if (typeInfo.Type is not INamedTypeSymbol named || !named.IsGenericType)
        {
            return;
        }

        var definition = named.OriginalDefinition.ToDisplayString();

        foreach (var mutable in MutableTypes)
        {
            if (definition == mutable)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, typeSyntax.GetLocation(), named.ToDisplayString()));
                return;
            }
        }
    }
}
