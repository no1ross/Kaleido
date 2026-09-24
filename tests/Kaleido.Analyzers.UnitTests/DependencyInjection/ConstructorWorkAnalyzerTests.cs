using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.DependencyInjection.ConstructorWorkAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class ConstructorWorkAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0011", DiagnosticSeverity.Warning);

    private const string Registrations = @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.Add<IFoo, Foo>();
        s.Add<Impl>();
    }
}
public interface IFoo
{
    void Initialize();
}
public class Foo : IFoo
{
    public void Initialize() { }
}
public static class ServiceCollectionExtensionsForObject
{
    public static void Add<TService, TImpl>(this object s) { }
    public static void Add<TService>(this object s) { }
}
";

    [Fact]
    public async Task CallOnInjectedParameter_Reports()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public Impl(IFoo foo)
    {
        {|#0:foo.Initialize()|};
    }
}",
            Expected.WithLocation(0).WithArguments("Initialize", "Impl"));
    }

    [Fact]
    public async Task AssignmentAndThrowIfNull_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    private readonly IFoo _foo;
    public Impl(IFoo foo)
    {
        System.ArgumentNullException.ThrowIfNull(foo);
        _foo = foo;
    }
}");
    }

    [Fact]
    public async Task LinqShaping_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    private readonly string[] _names;
    public Impl(IFoo foo, System.Collections.Generic.IEnumerable<string> names)
    {
        _foo = foo;
        _names = System.Linq.Enumerable.ToArray(names);
    }
    private readonly IFoo _foo;
}");
    }
}
