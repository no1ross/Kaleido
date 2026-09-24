using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.DependencyInjection.DependencyRetentionAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class DependencyRetentionAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0010", DiagnosticSeverity.Warning);

    private const string Registrations = @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.Add<IFoo, Foo>();
        s.Add<Impl>();
    }
}
public interface IFoo { }
public class Foo : IFoo { }
public static class ServiceCollectionExtensionsForObject
{
    public static void Add<TService, TImpl>(this object s) { }
    public static void Add<TService>(this object s) { }
}
";

    [Fact]
    public async Task MutableServiceField_Reports()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    private IFoo {|#0:_foo|};
}",
            Expected.WithLocation(0).WithArguments("_foo", "Impl", "IFoo"));
    }

    [Fact]
    public async Task UnusedCtorParameter_Reports()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public Impl(IFoo {|#0:foo|}) { }
}",
            Expected.WithLocation(0).WithArguments("foo", "Impl"));
    }

    [Fact]
    public async Task ReadonlyServiceField_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    private readonly IFoo _foo;
    public Impl(IFoo foo) => _foo = foo;
}");
    }

    [Fact]
    public async Task UsedPrimaryCtorParameter_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl(IFoo foo)
{
    public void M() => _ = foo;
}");
    }
}
