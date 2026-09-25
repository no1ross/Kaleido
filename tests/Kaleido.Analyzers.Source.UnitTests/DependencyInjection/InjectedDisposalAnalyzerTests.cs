using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.InjectedDisposalAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class InjectedDisposalAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0013", DiagnosticSeverity.Error);

    private const string Registrations = @"
public static class AppServiceCollectionExtensions
{
    public static void Add(object s)
    {
        s.Add<IFoo, Foo>();
    }
}
public interface IFoo : System.IDisposable { }
public class Foo : IFoo
{
    public void Dispose() { }
}
public static class ServiceCollectionExtensionsForObject
{
    public static void Add<TService, TImpl>(this object s) { }
}
";

    [Fact]
    public async Task DisposeOnInjectedField_Reports()
    {
        await RunAsync(Registrations + @"
public class Consumer : System.IDisposable
{
    private readonly IFoo _foo;
    public Consumer(IFoo foo) => _foo = foo;
    public void Dispose() { {|#0:_foo.Dispose()|}; }
}",
            Expected.WithLocation(0).WithArguments("Dispose", "_foo", "IFoo"));
    }

    [Fact]
    public async Task DisposeOnInjectedParameter_Reports()
    {
        await RunAsync(Registrations + @"
public class Consumer
{
    public void M(IFoo foo) { {|#0:foo.Dispose()|}; }
}",
            Expected.WithLocation(0).WithArguments("Dispose", "foo", "IFoo"));
    }

    [Fact]
    public async Task DisposeOnSelfCreatedLocal_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Consumer
{
    public void M() { var s = new System.IO.MemoryStream(); s.Dispose(); }
}");
    }
}
