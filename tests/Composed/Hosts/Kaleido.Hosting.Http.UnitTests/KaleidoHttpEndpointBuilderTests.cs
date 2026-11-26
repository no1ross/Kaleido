using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Hosting.Http.UnitTests;

public sealed class KaleidoHttpEndpointBuilderTests : Kaleido.UnitTests.SutFixture
{
    private static KaleidoHttpEndpointBuilder CreateSut(WebApplication app) => new(app.MapGroup(string.Empty));

    [Fact]
    public void Endpoints_UsesTheSameRootForEverySelection()
    {
        using var app = WebApplication.CreateBuilder().Build();

        Assert.IsAssignableFrom<IEndpointConventionBuilder>(CreateSut(app));
    }

    [Fact]
    public void RequireAvailable_RejectsDuplicateCapabilityMappings()
    {
        using var app = WebApplication.CreateBuilder().Build();
        var sut = CreateSut(app);
        sut.MarkMapped("Process");

        var error = Assert.Throws<KaleidoConfigurationException>(() => sut.RequireAvailable("Process"));

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, error.Code);
        sut.RequireAvailable("Queryable");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequireAvailable_RejectsMixingTheRegistryAuthorityWithAService(bool registryFirst)
    {
        using var app = WebApplication.CreateBuilder().Build();
        var sut = CreateSut(app);
        sut.MarkMapped(registryFirst ? "CentralRegistry" : "Process");

        var error = Assert.Throws<KaleidoConfigurationException>(() => sut.RequireAvailable(registryFirst ? "Process" : "CentralRegistry"));

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, error.Code);
    }

    [Fact]
    public void Add_AppliesRootConventionsToEndpointsMappedBeforeAndAfterTheCall()
    {
        using var app = WebApplication.CreateBuilder().Build();
        var sut = CreateSut(app);
        sut.Endpoints.MapGet("/before", () => Results.Ok());
        sut.Add(endpoint => endpoint.Metadata.Add(new Marker("shared")));
        sut.Endpoints.MapGet("/after", () => Results.Ok());

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.Equal(2, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.Equal("shared", Assert.Single(endpoint.Metadata.OfType<Marker>()).Name));
    }

    private sealed record Marker(string Name);
}
