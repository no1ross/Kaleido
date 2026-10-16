using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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

    private const string HelpersFixture = @"
namespace Acme.UnitTests
{
    public class HelpersTests
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}";

    private static Task RunWithReferencesAsync(params (string AssemblyName, string Source)[] references)
    {
        var test = Create(HelpersFixture + Stubs);
        test.TestState.ReferenceSources.AddRange(references);
        return test.RunAsync();
    }

    [Fact]
    public async Task NameResolvedSut_InaccessibleSameNamedTypeInAnEarlierReference_IsSkipped()
    {
        await RunWithReferencesAsync(
            ("Castle.Core", "namespace Castle.Xml { internal static class Helpers { } }"),
            ("TestProject", "namespace Acme { public static class Helpers { public static void M() { } } }"));
    }

    [Fact]
    public async Task NameResolvedSut_SameNamedTypeInAnEarlierReference_SourceAssemblyWins()
    {
        await RunWithReferencesAsync(
            ("Other.Library", "namespace Other { public class Helpers { } }"),
            ("TestProject", "namespace Acme { public class Helpers { public void M() { } } }"));
    }

    [Fact]
    public async Task NameResolvedSut_InternalSourceTypeVisibleToTheFixture_WinsOverAPublicSameNamedType()
    {
        await RunWithReferencesAsync(
            ("Castle.Core", "namespace Castle.Xml { public static class Helpers { } }"),
            ("TestProject", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"TestProject.UnitTests\")] namespace Acme { internal static class Helpers { internal static void M() { } } }"));
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
