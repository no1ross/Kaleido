using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Structure.InterfaceCoLocationAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class InterfaceCoLocationAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0015", DiagnosticSeverity.Warning);

    [Fact]
    public async Task InterfaceInSameFileAsImpl_NoDiagnostic()
    {
        await RunAsync(@"
public interface IFoo { }
public class Foo : IFoo { }
");
    }

    [Fact]
    public async Task InterfaceInSeparateFile_Reports()
    {
        var test = Create("");
        test.TestState.Sources.Add(("IFoo.cs", @"
public interface {|#0:IFoo|} { }
"));
        test.TestState.Sources.Add(("Foo.cs", @"
public class Foo : IFoo { }
"));
        test.ExpectedDiagnostics.Add(
            Expected.WithLocation(0).WithArguments("IFoo", "Foo.cs", "Foo"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ProviderInterfaceWithoutSameNamedImpl_NoDiagnostic()
    {
        await RunAsync(@"
public interface IStore { }
public class InMemoryStore : IStore { }
public class SqlStore : IStore { }
");
    }

    [Fact]
    public async Task InterfaceNamedButNotImplementedBySameNamedType_NoDiagnostic()
    {
        var test = Create("");
        test.TestState.Sources.Add(("IFoo.cs", @"
public interface IFoo { }
"));
        test.TestState.Sources.Add(("Foo.cs", @"
public class Foo { }
"));
        await test.RunAsync();
    }
}
