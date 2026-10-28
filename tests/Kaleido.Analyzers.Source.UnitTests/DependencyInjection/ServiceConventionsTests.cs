using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Kaleido.Analyzers.Source.DependencyInjection.UnitTests;

// #465: the DI model is harvested once per compilation and shared by the seven DI rules, and what
// counts as a registration, its lifetime and a composition root is judged by symbol, not by name.
public sealed class ServiceConventionsTests
    : global::Kaleido.UnitTests.SutFixture
{
    private const string DependencyInjection = @"
namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddScoped<TService, TImplementation>(this IServiceCollection services) => services;
        public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services) => services;
        public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services) => services;
    }

    public static class ServiceCollectionHostedServiceExtensions
    {
        public static IServiceCollection AddHostedService<THostedService>(this IServiceCollection services) => services;
    }
}
public interface IFoo { }
public class Foo : IFoo { }
public interface IBar { }
public class Bar : IBar { }
public class Worker { }
";

    private static CSharpCompilation Compile(string source) =>
        CSharpCompilation.Create(
            "Kaleido.Probe",
            [CSharpSyntaxTree.ParseText("using Microsoft.Extensions.DependencyInjection;\n" + source + DependencyInjection)],
            Basic.Reference.Assemblies.ReferenceAssemblies.Net80,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ServiceRegistrationModel.ServiceLifetime? LifetimeOf(ServiceRegistrationModel model, string typeName) =>
        model.Lifetimes.FirstOrDefault(x => x.Key.Name == typeName) is { Key: not null } pair ? pair.Value : null;

    [Fact]
    public void ModelFor_TheSameCompilation_HarvestsOnce()
    {
        var compilation = Compile("public static class AppExtensions { public static IServiceCollection AddApp(this IServiceCollection s) => s.AddScoped<IFoo, Foo>(); }");

        var first = ServiceConventions.ModelFor(compilation);
        var second = ServiceConventions.ModelFor(compilation);

        Assert.Same(first, second);
        Assert.NotSame(first, ServiceConventions.ModelFor(Compile("public static class Other { }")));
    }

    [Fact]
    public void Harvest_TakesLifetimes_FromMicrosoftsRegistrationMethods()
    {
        var model = ServiceConventions.ModelFor(Compile(@"
public static class AppExtensions
{
    public static IServiceCollection AddApp(this IServiceCollection s) =>
        s.AddSingleton<IFoo, Foo>().AddTransient<IBar, Bar>().AddHostedService<Worker>();
}"));

        Assert.Equal(ServiceRegistrationModel.ServiceLifetime.Singleton, LifetimeOf(model, "IFoo"));
        Assert.Equal(ServiceRegistrationModel.ServiceLifetime.Transient, LifetimeOf(model, "IBar"));
        Assert.Equal(ServiceRegistrationModel.ServiceLifetime.Singleton, LifetimeOf(model, "Worker"));
    }

    // The old name test took 'Singleton' anywhere in a method name; a Kaleido-style helper that merely
    // contains the word registers with the default lifetime.
    [Fact]
    public void Harvest_AHelperWhoseNameContainsSingleton_IsScoped()
    {
        var model = ServiceConventions.ModelFor(Compile(@"
public static class AppExtensions
{
    public static IServiceCollection AddApp(this IServiceCollection s) => s.AddSingletonCache<IFoo, Foo>();
    public static IServiceCollection AddSingletonCache<TService, TImplementation>(this IServiceCollection s) => s;
}"));

        Assert.Equal(ServiceRegistrationModel.ServiceLifetime.Scoped, LifetimeOf(model, "IFoo"));
    }

    // A method named like a registration on something that is not a registration builder registers nothing.
    [Fact]
    public void Harvest_ALookAlikeAddMethodOnAnotherReceiver_RegistersNothing()
    {
        var model = ServiceConventions.ModelFor(Compile(@"
public sealed class Menu { }
public static class AppExtensions
{
    public static IServiceCollection AddApp(this IServiceCollection s) { new Menu().AddSingleton<IFoo, Foo>(); return s; }
    public static Menu AddSingleton<TService, TImplementation>(this Menu menu) => menu;
}"));

        Assert.Empty(model.Lifetimes);
    }

    // A composition root is a static class extending a registration builder, whatever its name; a class
    // only named like one is not.
    [Fact]
    public void IsCompositionRoot_IsJudgedBySymbol_NotByName()
    {
        var compilation = Compile(@"
public static class Wiring { public static IServiceCollection AddApp(this IServiceCollection s) => s.AddScoped<IFoo, Foo>(); }
public static class LooksLikeServiceCollectionExtensions { public static string Name(string value) => value; }");

        Assert.True(ServiceConventions.IsCompositionRoot(compilation.GetTypeByMetadataName("Wiring")!));
        Assert.False(ServiceConventions.IsCompositionRoot(compilation.GetTypeByMetadataName("LooksLikeServiceCollectionExtensions")!));
        Assert.Equal(ServiceRegistrationModel.ServiceLifetime.Scoped, LifetimeOf(ServiceConventions.ModelFor(compilation), "IFoo"));
    }
}
