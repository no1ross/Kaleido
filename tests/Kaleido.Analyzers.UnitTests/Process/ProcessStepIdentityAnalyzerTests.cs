using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.ProcessStepIdentityAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class ProcessStepIdentityAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.ProcessStepIdentityAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.ProcessStepIdentityAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace Kaleido.Processor
{
    public interface IProcessStep { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ProcessStepAttribute : System.Attribute
    {
        public string Version { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }
}";

    private static readonly DiagnosticResult AttributeWithoutInterface =
        new("KAL2010", DiagnosticSeverity.Error);

    private static readonly DiagnosticResult InterfaceWithoutAttribute =
        new("KAL2011", DiagnosticSeverity.Error);

    [Fact]
    public async Task AttributeWithoutInterface_Reports()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Version = ""1"", DisplayName = ""Upsert"", Description = ""Upserts."")]
public class {|#0:UpsertStep|} { }
" + KaleidoStubs,
            AttributeWithoutInterface.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task InterfaceWithoutAttribute_Reports()
    {
        await RunAsync(@"
public class {|#0:UpsertStep|} : Kaleido.Processor.IProcessStep { }
" + KaleidoStubs,
            InterfaceWithoutAttribute.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task InterfaceAndAttribute_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Version = ""1"", DisplayName = ""Upsert"", Description = ""Upserts."")]
public class UpsertStep : Kaleido.Processor.IProcessStep { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task AbstractInterfaceTypeWithoutAttribute_NoDiagnostic()
    {
        await RunAsync(@"
public abstract class StepBase : Kaleido.Processor.IProcessStep { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task PlainClass_NoDiagnostic()
    {
        await RunAsync(@"
public class Mirror { }
" + KaleidoStubs);
    }
}
