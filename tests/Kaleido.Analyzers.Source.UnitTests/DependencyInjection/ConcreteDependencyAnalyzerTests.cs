using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.ConcreteDependencyAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class ConcreteDependencyAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0008", DiagnosticSeverity.Warning);

    private const string Registrations = @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.Add<IFoo, Foo>();
        s.Add<Bar>();
    }
}
public interface IFoo { }
public class Foo : IFoo { }
public class Bar { }
public static class ServiceCollectionExtensionsForObject
{
    public static void Add<TService, TImpl>(this object s) { }
    public static void Add<TService>(this object s) { }
}
";

    [Fact]
    public async Task ConcreteImplRegisteredUnderInterface_Reports()
    {
        await RunAsync(Registrations + @"
public class Consumer
{
    public Consumer(Foo {|#0:foo|}) { }
}",
            Expected.WithLocation(0).WithArguments("foo", "Foo", "IFoo"));
    }

    [Fact]
    public async Task ConcreteSelfRegistered_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Consumer
{
    public Consumer(Bar bar) { }
}");
    }

    [Fact]
    public async Task InterfaceParam_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Consumer
{
    public Consumer(IFoo foo) { }
}");
    }
}
