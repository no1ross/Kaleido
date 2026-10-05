using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Layout.InterfaceCoLocationAnalyzer>;

namespace Kaleido.Analyzers.Source.Layout.UnitTests;

public sealed class InterfaceCoLocationAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Layout.InterfaceCoLocationAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Layout.InterfaceCoLocationAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0015", DiagnosticSeverity.Error);

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
