using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// Shared model of the DI registrations visible in a compilation, harvested
/// from *ServiceCollectionExtensions classes: implementation types,
/// abstraction→implementation pairs, and service lifetimes. Used by the
/// KAL0005–KAL0014 DI enforcement rules.
/// </summary>
internal sealed class ServiceRegistrationModel
{
    /// <summary>Concrete implementation types registered with the container.</summary>
    public HashSet<INamedTypeSymbol> Implementations { get; } =
        new(SymbolEqualityComparer.Default);

    /// <summary>Service type (abstraction or self) → implementation type.</summary>
    public Dictionary<INamedTypeSymbol, INamedTypeSymbol> ServiceToImpl { get; } =
        new(SymbolEqualityComparer.Default);

    /// <summary>Service type → registered lifetime.</summary>
    public Dictionary<INamedTypeSymbol, ServiceLifetime> Lifetimes { get; } =
        new(SymbolEqualityComparer.Default);

    /// <summary>All registered service types (abstractions and self-registrations).</summary>
    public IEnumerable<INamedTypeSymbol> Services => ServiceToImpl.Keys;

    public enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }
}

internal static class ServiceConventions
{
    /// <summary>
    /// Harvests DI registrations from *ServiceCollectionExtensions classes in
    /// the compilation: generic args of Add*/TryAdd*/Use* calls, typeof() args,
    /// and new-expressions inside registration lambdas.
    /// </summary>
    public static ServiceRegistrationModel Harvest(
        Compilation compilation,
        System.Threading.CancellationToken cancellationToken)
    {
        var model = new ServiceRegistrationModel();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot(cancellationToken);
            var model_ = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!IsCompositionRoot(typeDecl))
                {
                    continue;
                }

                foreach (var invocation in typeDecl.DescendantNodes()
                             .OfType<InvocationExpressionSyntax>())
                {
                    var name = InvocationName(invocation);

                    if (name is null || !IsRegistrationMethod(name))
                    {
                        continue;
                    }

                    var lifetime = LifetimeOf(name);

                    // typeof(T) args
                    foreach (var typeOf in invocation.DescendantNodes()
                                 .OfType<TypeOfExpressionSyntax>())
                    {
                        AddType(model_, typeOf.Type, model, lifetime, cancellationToken);
                    }

                    // generic type args
                    var typeArgs =
                        invocation.Expression is MemberAccessExpressionSyntax access &&
                        access.Name is GenericNameSyntax generic
                            ? generic.TypeArgumentList.Arguments
                            : invocation.Expression is GenericNameSyntax directGeneric
                                ? directGeneric.TypeArgumentList.Arguments
                                : default;

                    if (typeArgs != default)
                    {
                        var resolved =
                            typeArgs
                                .Select(a => model_.GetTypeInfo(a, cancellationToken).Type)
                                .OfType<INamedTypeSymbol>()
                                .ToList();

                        foreach (var t in resolved.Where(t => t.TypeKind != TypeKind.Interface))
                        {
                            Register(model, t, t, lifetime);
                        }

                        // pair interface service args to their impl args
                        var services =
                            resolved.Where(t => t.TypeKind == TypeKind.Interface).ToList();
                        var impls =
                            resolved.Where(t => t.TypeKind != TypeKind.Interface).ToList();

                        if (services.Count == 1 && impls.Count >= 1)
                        {
                            Register(model, services[0], impls[impls.Count - 1], lifetime);
                        }
                    }

                    // returned new T() inside registration lambdas → impl type T
                    // (sp => new T(...) or sp => { return new T(...); })
                    foreach (var lambda in invocation.DescendantNodes()
                                 .OfType<AnonymousFunctionExpressionSyntax>())
                    {
                        HarvestReturnedNew(
                            lambda.Body, model_, model, lifetime, cancellationToken);
                    }
                }

                // new T() returned by expression-bodied members or return
                // statements — e.g. internal factory methods on the extensions
                // class (CreateFoo() => new Foo())
                foreach (var member in typeDecl.DescendantNodes())
                {
                    switch (member)
                    {
                        case ArrowExpressionClauseSyntax arrow:
                            HarvestReturnedNew(
                                arrow.Expression, model_, model,
                                ServiceRegistrationModel.ServiceLifetime.Scoped,
                                cancellationToken);
                            break;
                        case ReturnStatementSyntax returnStatement:
                            HarvestReturnedNew(
                                returnStatement.Expression, model_, model,
                                ServiceRegistrationModel.ServiceLifetime.Scoped,
                                cancellationToken);
                            break;
                    }
                }
            }
        }

        return model;
    }

    /// <summary>
    /// Composition-root types: DI registration extensions and endpoint
    /// route builders. Service location and manual construction inside
    /// these types is legitimate — they are the wiring seam.
    /// </summary>
    public static bool IsCompositionRoot(SyntaxNode node) =>
        node is TypeDeclarationSyntax decl &&
        IsCompositionRootName(decl.Identifier.ValueText);

    public static bool IsInsideCompositionRoot(SyntaxNodeAnalysisContext context) =>
        context.ContainingSymbol?.ContainingType?.Name is { } name &&
        IsCompositionRootName(name);

    private static bool IsCompositionRootName(string name) =>
        name.EndsWith("ServiceCollectionExtensions", System.StringComparison.Ordinal) ||
        name.EndsWith("EndpointRouteBuilderExtensions", System.StringComparison.Ordinal);

    private static void AddType(
        SemanticModel model,
        TypeSyntax typeSyntax,
        ServiceRegistrationModel registrations,
        ServiceRegistrationModel.ServiceLifetime lifetime,
        System.Threading.CancellationToken cancellationToken)
    {
        if (model.GetTypeInfo(typeSyntax, cancellationToken).Type is INamedTypeSymbol t)
        {
            Register(registrations, t, t, lifetime);
        }
    }

    private static void Register(
        ServiceRegistrationModel model,
        INamedTypeSymbol service,
        INamedTypeSymbol impl,
        ServiceRegistrationModel.ServiceLifetime lifetime)
    {
        var implDef = impl.OriginalDefinition;
        var serviceDef = service.OriginalDefinition;

        if (implDef.TypeKind != TypeKind.Interface)
        {
            model.Implementations.Add(implDef);
        }

        if (!model.ServiceToImpl.ContainsKey(serviceDef))
        {
            model.ServiceToImpl[serviceDef] = implDef;
        }

        if (!model.Lifetimes.ContainsKey(serviceDef))
        {
            model.Lifetimes[serviceDef] = lifetime;
        }
    }

    private static void HarvestReturnedNew(
        SyntaxNode body,
        SemanticModel model,
        ServiceRegistrationModel registrations,
        ServiceRegistrationModel.ServiceLifetime lifetime,
        System.Threading.CancellationToken cancellationToken)
    {
        ExpressionSyntax? returned =
            body as ExpressionSyntax ??
            (body as BlockSyntax)?.Statements
                .OfType<ReturnStatementSyntax>()
                .Select(r => r.Expression)
                .FirstOrDefault();

        if (returned is not null &&
            (returned.IsKind(SyntaxKind.ObjectCreationExpression) ||
             returned.IsKind(SyntaxKind.ImplicitObjectCreationExpression)) &&
            model.GetTypeInfo(returned, cancellationToken).Type
                is INamedTypeSymbol t &&
            t.TypeKind == TypeKind.Class)
        {
            Register(registrations, t, t, lifetime);
        }
    }

    private static bool IsRegistrationMethod(string name) =>
        name.StartsWith("Add", System.StringComparison.Ordinal) ||
        name.StartsWith("TryAdd", System.StringComparison.Ordinal) ||
        name.StartsWith("Use", System.StringComparison.Ordinal);

    private static ServiceRegistrationModel.ServiceLifetime LifetimeOf(string name) =>
        name.Contains("Singleton")
            ? ServiceRegistrationModel.ServiceLifetime.Singleton
            : name.Contains("Transient")
                ? ServiceRegistrationModel.ServiceLifetime.Transient
                : ServiceRegistrationModel.ServiceLifetime.Scoped;

    private static string? InvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name switch
            {
                GenericNameSyntax generic => generic.Identifier.ValueText,
                IdentifierNameSyntax ident => ident.Identifier.ValueText,
                _ => null
            },
            GenericNameSyntax generic => generic.Identifier.ValueText,
            IdentifierNameSyntax ident => ident.Identifier.ValueText,
            _ => null
        };
}
