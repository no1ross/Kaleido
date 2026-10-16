using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpEndpointRouteBuilderExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static WebApplication CreateEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddMcpServer().WithHttpTransport();

        return builder.Build();
    }

    private static string?[] Patterns(IEndpointRouteBuilder endpoints) =>
        [.. endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(x => x.RoutePattern.RawText)
            .Distinct(StringComparer.Ordinal)];

    [Fact]
    public void MapKaleidoMcp_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KaleidoMcpEndpointRouteBuilderExtensions.MapKaleidoMcp(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MapKaleidoMcp_WithABlankPattern_Throws_AndMapsNothing(string pattern)
    {
        var endpoints = CreateEndpoints();

        Assert.Throws<ArgumentException>(() => endpoints.MapKaleidoMcp(pattern));

        Assert.Empty(Patterns(endpoints));
    }

    [Fact]
    public void MapKaleidoMcp_WithANullPattern_Throws()
    {
        var endpoints = CreateEndpoints();

        Assert.Throws<ArgumentNullException>(() => endpoints.MapKaleidoMcp(null!));
    }

    [Fact]
    public void MapKaleidoMcp_MapsTheMcpEndpoint_AtSlashMcp_ByDefault()
    {
        var endpoints = CreateEndpoints();

        var convention = endpoints.MapKaleidoMcp();

        Assert.NotNull(convention);
        Assert.NotEmpty(Patterns(endpoints));
        Assert.All(Patterns(endpoints), x => Assert.StartsWith("/mcp", x, StringComparison.Ordinal));
    }

    [Fact]
    public void MapKaleidoMcp_MapsTheMcpEndpoint_AtTheGivenPattern()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapKaleidoMcp("/agents");

        Assert.NotEmpty(Patterns(endpoints));
        Assert.All(Patterns(endpoints), x => Assert.StartsWith("/agents", x, StringComparison.Ordinal));
    }
}
