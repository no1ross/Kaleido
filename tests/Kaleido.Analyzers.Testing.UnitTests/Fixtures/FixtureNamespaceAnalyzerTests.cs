using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Testing.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Testing.Fixtures.FixtureNamespaceAnalyzer>;

namespace Kaleido.Analyzers.Testing.Fixtures.UnitTests;

public sealed class FixtureNamespaceAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.FixtureNamespaceAnalyzer>
{
    protected override global::Kaleido.Analyzers.Testing.Fixtures.FixtureNamespaceAnalyzer CreateSut() =>
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
namespace Acme.Widgets
{
    public class Widget { }
}
namespace Acme
{
    public class Gadget { }
}
";

    private static readonly DiagnosticResult Expected =
        new("KAL1013", DiagnosticSeverity.Error);

    [Fact]
    public async Task SutFixtureInSutNamespacePlusUnitTests_NoDiagnostic()
    {
        await RunAsync(@"
namespace Acme.Widgets.UnitTests
{
    public class WidgetTests : SutFixture<Acme.Widgets.Widget>
    {
        protected override Acme.Widgets.Widget CreateSut() => new Acme.Widgets.Widget();
        [Xunit.Fact]
        public void M_S_E() { }
    }
}" + Stubs);
    }

    [Fact]
    public async Task SutFixtureInFolderStyleNamespace_Reports()
    {
        await RunAsync(@"
namespace TestProject.UnitTests.Widgets
{
    public class {|#0:WidgetTests|} : SutFixture<Acme.Widgets.Widget>
    {
        protected override Acme.Widgets.Widget CreateSut() => new Acme.Widgets.Widget();
        [Xunit.Fact]
        public void M_S_E() { }
    }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("WidgetTests", "Acme.Widgets.Widget", "Acme.Widgets.UnitTests"));
    }

    [Fact]
    public async Task NameResolvedSut_InWrongNamespace_Reports()
    {
        await RunAsync(@"
namespace Acme.Widgets.UnitTests
{
    public class {|#0:GadgetTests|}
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("GadgetTests", "Acme.Gadget", "Acme.UnitTests"));
    }

    [Fact]
    public async Task UnresolvableFixture_NoDiagnostic()
    {
        await RunAsync(@"
namespace Anywhere
{
    public class ScenarioTests
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}" + Stubs);
    }
}
