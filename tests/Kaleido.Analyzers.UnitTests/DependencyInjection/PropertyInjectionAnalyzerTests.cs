using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.DependencyInjection.PropertyInjectionAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class PropertyInjectionAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0009", DiagnosticSeverity.Warning);

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
    public async Task SettableServiceProperty_Reports()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public IFoo {|#0:Foo|} { get; set; }
}",
            Expected.WithLocation(0).WithArguments("Foo", "Impl", "IFoo"));
    }

    [Fact]
    public async Task SetMethodInjection_Reports()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public void {|#0:SetFoo|}(IFoo foo) { }
}",
            Expected.WithLocation(0).WithArguments("SetFoo", "Impl", "IFoo"));
    }

    [Fact]
    public async Task GetOnlyProperty_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public IFoo Foo { get; }
}");
    }

    [Fact]
    public async Task SettableNonServiceProperty_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Impl
{
    public string Name { get; set; }
}");
    }
}
