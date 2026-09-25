using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.DependencyInjection.ServiceInstantiationAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class ServiceInstantiationAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0006", DiagnosticSeverity.Error);

    private const string Registrations = @"
public static class AppServiceCollectionExtensions
{
    public static object CreateFoo() => new Foo();
    public static object CreateSelf() => new SelfService();
}
public interface IFoo { }
public class Foo : IFoo { }
public class SelfService { }
";

    [Fact]
    public async Task New_OnRegisteredType_Reports()
    {
        await RunAsync(Registrations + @"
public class Bar
{
    public void M() { var x = {|#0:new Foo()|}; }
}",
            Expected.WithLocation(0).WithArguments("Foo"));
    }

    [Fact]
    public async Task TargetTypedNew_OnRegisteredType_Reports()
    {
        await RunAsync(Registrations + @"
public class Bar
{
    public void M() { Foo x = {|#0:new()|}; }
}",
            Expected.WithLocation(0).WithArguments("Foo"));
    }

    [Fact]
    public async Task New_InsideServiceCollectionExtensions_NoDiagnostic()
    {
        await RunAsync(Registrations);
    }

    [Fact]
    public async Task New_OnUnregisteredType_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public class Plain { }
public class Bar
{
    public void M() { var x = new Plain(); var y = new System.Collections.Generic.List<int>(); }
}");
    }

    [Fact]
    public async Task New_OnRecordOrException_NoDiagnostic()
    {
        await RunAsync(Registrations + @"
public record Dto(int Value);
public class FooException : System.Exception { }
public class Bar
{
    public void M() { var x = new Dto(1); var e = new FooException(); }
}");
    }
}
