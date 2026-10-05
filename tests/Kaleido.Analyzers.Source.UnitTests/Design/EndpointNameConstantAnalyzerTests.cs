using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.EndpointNameConstantAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class EndpointNameConstantAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.EndpointNameConstantAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.EndpointNameConstantAnalyzer CreateSut() =>
        new();

    private const string Stubs = @"
namespace Microsoft.AspNetCore.Builder
{
    public interface IEndpointConventionBuilder { }

    public static class RoutingEndpointConventionBuilderExtensions
    {
        public static TBuilder WithName<TBuilder>(this TBuilder builder, string endpointName)
            where TBuilder : IEndpointConventionBuilder => builder;
    }
}

public static class ThingEndpointNames
{
    public const string Get = ""GetThing"";
    public static string Step(string name) => name + ""-step"";
}

public static class OtherNames
{
    public const string Get = ""GetThing"";
}";

    private static readonly DiagnosticResult Expected =
        new("KAL0020", DiagnosticSeverity.Warning);

    [Fact]
    public async Task WithName_StringLiteral_Reports()
    {
        await RunAsync(@"
using Microsoft.AspNetCore.Builder;

public class C
{
    public void M(IEndpointConventionBuilder b) => b.WithName({|#0:""GetSteps""|});
}" + Stubs,
            Expected.WithLocation(0).WithArguments("\"GetSteps\""));
    }

    [Fact]
    public async Task WithName_ConstantFromOtherType_Reports()
    {
        await RunAsync(@"
using Microsoft.AspNetCore.Builder;

public class C
{
    public void M(IEndpointConventionBuilder b) => b.WithName({|#0:OtherNames.Get|});
}" + Stubs,
            Expected.WithLocation(0).WithArguments("OtherNames.Get"));
    }

    [Fact]
    public async Task WithName_EndpointNamesConstant_NoDiagnostic()
    {
        await RunAsync(@"
using Microsoft.AspNetCore.Builder;

public class C
{
    public void M(IEndpointConventionBuilder b) => b.WithName(ThingEndpointNames.Get);
}" + Stubs);
    }

    [Fact]
    public async Task WithName_EndpointNamesFactory_NoDiagnostic()
    {
        await RunAsync(@"
using Microsoft.AspNetCore.Builder;

public class C
{
    public void M(IEndpointConventionBuilder b, string step) => b.WithName(ThingEndpointNames.Step(step));
}" + Stubs);
    }
}
