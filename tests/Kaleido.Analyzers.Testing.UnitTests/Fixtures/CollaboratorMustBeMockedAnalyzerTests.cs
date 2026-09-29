using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.CollaboratorMustBeMockedAnalyzer>;

namespace Kaleido.Analyzers.Testing.UnitTests;

public sealed class CollaboratorMustBeMockedAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.CollaboratorMustBeMockedAnalyzer>
{
    protected override global::Kaleido.Analyzers.Testing.Fixtures.CollaboratorMustBeMockedAnalyzer CreateSut() =>
        new();

    private const string Stubs = @"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}
public abstract class SutFixture<TSut> where TSut : class
{
    protected abstract TSut CreateSut();
}
public abstract class SutFixture { }
";

    // Compiled into a separate in-memory "Kaleido.Runtime" assembly so the
    // analyzer sees these as production-framework types, not test-assembly types.
    private const string FrameworkStubs = @"
namespace Kaleido.Runtime
{
    public class Widget
    {
        public Widget() { }
        public Widget(object dependency) { }
        public void Run() { }
    }
    public interface ICollaborator
    {
        void Help();
    }
    public class Collaborator : ICollaborator
    {
        public void Help() { }
    }
    public class DomainData
    {
        public string? Value { get; set; }
    }
    public class CollaboratorOptions
    {
    }
    public record CollaboratorContract(int Id);
    public class NonTestable
    {
    }
}
";

    private static readonly DiagnosticResult Expected =
        new("KAL1012", DiagnosticSeverity.Error);

    private static Task RunAsync(string source, params DiagnosticResult[] expected)
    {
        var test = Create(source + Stubs, expected);
        test.TestState.ReferenceSources.Add(("Kaleido.Runtime", FrameworkStubs));
        return test.RunAsync();
    }

    [Fact]
    public async Task NewCollaboratorInTestMethod_Reports()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E() { var c = {|#0:new Kaleido.Runtime.Collaborator()|}; }
}",
            Expected.WithLocation(0).WithArguments("Collaborator"));
    }

    [Fact]
    public async Task NewCollaboratorInCreateSut_Reports()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() =>
        new({|#0:new Kaleido.Runtime.Collaborator()|});
    [Xunit.Fact]
    public void M_S_E() { }
}",
            Expected.WithLocation(0).WithArguments("Collaborator"));
    }

    [Fact]
    public async Task NewSutInTestMethod_NoCollaboratorDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E() { var sut = new Kaleido.Runtime.Widget(); }
}");
    }

    [Fact]
    public async Task NewDomainData_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E() { var d = new Kaleido.Runtime.DomainData(); }
}");
    }

    [Fact]
    public async Task NewDtoOrRecord_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E()
    {
        var o = new Kaleido.Runtime.CollaboratorOptions();
        var c = new Kaleido.Runtime.CollaboratorContract(1);
    }
}");
    }

    [Fact]
    public async Task NewTestAssemblyDouble_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E() { var d = new WidgetDouble(); }
}
public class WidgetDouble
{
    public void Help() { }
}");
    }

    [Fact]
    public async Task NewCollaboratorOutsideFixture_NoDiagnostic()
    {
        await RunAsync(@"
public class WidgetTests : SutFixture<Kaleido.Runtime.Widget>
{
    protected override Kaleido.Runtime.Widget CreateSut() => new();
    [Xunit.Fact]
    public void M_S_E() { }
}
public static class Helpers
{
    public static object Make() => new Kaleido.Runtime.Collaborator();
}");
    }
}
