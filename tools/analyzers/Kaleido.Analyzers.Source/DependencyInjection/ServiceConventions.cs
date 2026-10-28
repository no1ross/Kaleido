using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.DependencyInjection;

/// <summary>
/// Shared model of the DI registrations visible in a compilation, harvested from its composition
/// roots: implementation types, abstraction-to-implementation pairs, and service lifetimes. Used by
/// the KAL0004-KAL0013 DI enforcement rules.
/// </summary>
internal sealed class ServiceRegistrationModel
{
    /// <summary>Concrete implementation types registered with the container.</summary>
    public HashSet<INamedTypeSymbol> Implementations { get; } =
        new(SymbolEqualityComparer.Default);

    /// <summary>Service type (abstraction or self) to implementation type.</summary>
    public Dictionary<INamedTypeSymbol, INamedTypeSymbol> ServiceToImpl { get; } =
        new(SymbolEqualityComparer.Default);

    /// <summary>Service type to registered lifetime.</summary>
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
    // The types a composition root extends, and the types whose methods register services (#465).
    private static readonly ImmutableHashSet<string> RegistrationReceivers =
        ImmutableHashSet.Create(
            "Microsoft.Extensions.DependencyInjection.IServiceCollection",
            "Microsoft.Extensions.DependencyInjection.IHttpClientBuilder",
            "Kaleido.IKaleidoBuilder");

    private static readonly ImmutableHashSet<string> CompositionRootReceivers =
        RegistrationReceivers.Add("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder");

    private const string ServiceDescriptorType = "Microsoft.Extensions.DependencyInjection.ServiceDescriptor";

    // Microsoft.Extensions.DependencyInjection methods that register with a lifetime other than the
    // scoped default, by their exact names on the resolved MS DI symbol (not a substring of any name).
    private static readonly ImmutableHashSet<string> SingletonMethods =
        ImmutableHashSet.Create(
            "AddSingleton", "TryAddSingleton", "AddKeyedSingleton", "TryAddKeyedSingleton",
            "Singleton", "KeyedSingleton", "AddHostedService");

    private static readonly ImmutableHashSet<string> TransientMethods =
        ImmutableHashSet.Create(
            "AddTransient", "TryAddTransient", "AddKeyedTransient", "TryAddKeyedTransient",
            "Transient", "KeyedTransient");

    // One harvest per compilation, shared by the seven DI analyzers (#465). Lazy: of two analyzers
    // starting at once, one harvests and the other waits for it.
    private static readonly ConditionalWeakTable<Compilation, Lazy<ServiceRegistrationModel>> Models = new();

    /// <summary>The compilation's registration model, harvested once and shared.</summary>
    public static ServiceRegistrationModel ModelFor(Compilation compilation) =>
        Models.GetValue(
            compilation,
            c => new Lazy<ServiceRegistrationModel>(() => Harvest(c, System.Threading.CancellationToken.None)))
        .Value;

    /// <summary>
    /// Harvests DI registrations from the compilation's composition roots: generic args of
    /// registration calls, typeof() args, and new-expressions inside registration lambdas and
    /// factory members. Use <see cref="ModelFor"/>, which harvests once per compilation.
    /// </summary>
    internal static ServiceRegistrationModel Harvest(
        Compilation compilation,
        System.Threading.CancellationToken cancellationToken)
    {
        var model = new ServiceRegistrationModel();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken) is not { } type ||
                    !IsCompositionRoot(type))
                {
                    continue;
                }

                foreach (var invocation in typeDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (RegistrationLifetime(invocation, semanticModel, cancellationToken) is not { } lifetime)
                    {
                        continue;
                    }

                    // typeof(T) args
                    foreach (var typeOf in invocation.DescendantNodes().OfType<TypeOfExpressionSyntax>())
                    {
                        AddType(semanticModel, typeOf.Type, model, lifetime, cancellationToken);
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
                                .Select(a => semanticModel.GetTypeInfo(a, cancellationToken).Type)
                                .OfType<INamedTypeSymbol>()
                                .ToList();

                        foreach (var t in resolved.Where(t => t.TypeKind != TypeKind.Interface))
                        {
                            Register(model, t, t, lifetime);
                        }

                        // pair interface service args to their impl args
                        var services = resolved.Where(t => t.TypeKind == TypeKind.Interface).ToList();
                        var impls = resolved.Where(t => t.TypeKind != TypeKind.Interface).ToList();

                        if (services.Count == 1 && impls.Count >= 1)
                        {
                            Register(model, services[0], impls[impls.Count - 1], lifetime);
                        }
                    }

                    // returned new T() inside registration lambdas -> impl type T
                    // (sp => new T(...) or sp => { return new T(...); })
                    foreach (var lambda in invocation.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>())
                    {
                        HarvestReturnedNew(lambda.Body, semanticModel, model, lifetime, cancellationToken);
                    }
                }

                // new T() returned by expression-bodied members or return statements - e.g. internal
                // factory methods on the extensions class (CreateFoo() => new Foo())
                foreach (var member in typeDecl.DescendantNodes())
                {
                    switch (member)
                    {
                        case ArrowExpressionClauseSyntax arrow:
                            HarvestReturnedNew(
                                arrow.Expression, semanticModel, model,
                                ServiceRegistrationModel.ServiceLifetime.Scoped,
                                cancellationToken);
                            break;
                        case ReturnStatementSyntax returnStatement
                            when returnStatement.Expression is not null:
                            HarvestReturnedNew(
                                returnStatement.Expression, semanticModel, model,
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
    /// A composition root: a static class with an extension method on a registration or routing
    /// builder (<c>IServiceCollection</c>, <c>IKaleidoBuilder</c>, <c>IHttpClientBuilder</c>,
    /// <c>IEndpointRouteBuilder</c>). Service location and manual construction inside it is
    /// legitimate - it is the wiring seam. Judged by symbol, not by the class name (#465).
    /// </summary>
    public static bool IsCompositionRoot(INamedTypeSymbol type) =>
        type.IsStatic &&
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => m.IsExtensionMethod &&
                      m.Parameters.Length > 0 &&
                      m.Parameters[0].Type is INamedTypeSymbol receiver &&
                      CompositionRootReceivers.Contains(FullName(receiver)));

    public static bool IsInsideCompositionRoot(SyntaxNodeAnalysisContext context)
    {
        for (var type = context.ContainingSymbol?.ContainingType; type is not null; type = type.ContainingType)
        {
            if (IsCompositionRoot(type))
            {
                return true;
            }
        }

        return false;
    }

    // The lifetime a registration call registers with, or null when the call registers nothing: a
    // method on IServiceCollection/IKaleidoBuilder/IHttpClientBuilder or a ServiceDescriptor factory,
    // resolved by symbol. Scoped unless MS DI's method says otherwise.
    private static ServiceRegistrationModel.ServiceLifetime? RegistrationLifetime(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var info = semanticModel.GetSymbolInfo(invocation, cancellationToken);

        if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is not IMethodSymbol method)
        {
            return null;
        }

        var original = method.ReducedFrom ?? method;
        var receiver = original.IsExtensionMethod && original.Parameters.Length > 0
            ? original.Parameters[0].Type as INamedTypeSymbol
            : original.ContainingType;

        var registers =
            (receiver is not null && RegistrationReceivers.Contains(FullName(receiver.OriginalDefinition))) ||
            FullName(original.ContainingType) == ServiceDescriptorType;

        if (!registers)
        {
            return null;
        }

        var fromMicrosoftDi = original.ContainingNamespace.ToDisplayString()
            .StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal);

        return !fromMicrosoftDi ? ServiceRegistrationModel.ServiceLifetime.Scoped
            : SingletonMethods.Contains(original.Name) ? ServiceRegistrationModel.ServiceLifetime.Singleton
            : TransientMethods.Contains(original.Name) ? ServiceRegistrationModel.ServiceLifetime.Transient
            : ServiceRegistrationModel.ServiceLifetime.Scoped;
    }

    private static string FullName(INamedTypeSymbol type) =>
        $"{type.ContainingNamespace.ToDisplayString()}.{type.Name}";

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
        var returned =
            body as ExpressionSyntax ??
            (body as BlockSyntax)?.Statements
                .OfType<ReturnStatementSyntax>()
                .Select(r => r.Expression)
                .FirstOrDefault();

        if (returned is not null &&
            (returned.IsKind(SyntaxKind.ObjectCreationExpression) ||
             returned.IsKind(SyntaxKind.ImplicitObjectCreationExpression)) &&
            model.GetTypeInfo(returned, cancellationToken).Type is INamedTypeSymbol t &&
            t.TypeKind == TypeKind.Class)
        {
            Register(registrations, t, t, lifetime);
        }
    }
}
