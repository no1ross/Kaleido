using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Kaleido.Analyzers.Source.DependencyInjection.UnitTests;

public sealed class ServiceRegistrationModelTests
    : global::Kaleido.UnitTests.SutFixture
{
    private static ServiceRegistrationModel CreateSut() => new();

    private static (INamedTypeSymbol Service, INamedTypeSymbol Implementation) Types()
    {
        var compilation = CSharpCompilation.Create(
            "Sample",
            [CSharpSyntaxTree.ParseText("public interface IClock { } public sealed class Clock : IClock { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return (compilation.GetTypeByMetadataName("IClock")!, compilation.GetTypeByMetadataName("Clock")!);
    }

    [Fact]
    public void Services_AreTheRegisteredServiceTypes()
    {
        var (service, implementation) = Types();
        var sut = CreateSut();

        sut.ServiceToImpl[service] = implementation;

        Assert.Equal([service], sut.Services, SymbolEqualityComparer.Default);
    }

    [Fact]
    public void ANewModel_HasNoRegistrations()
    {
        var sut = CreateSut();

        Assert.Empty(sut.Services);
        Assert.Empty(sut.Implementations);
        Assert.Empty(sut.Lifetimes);
    }
}
