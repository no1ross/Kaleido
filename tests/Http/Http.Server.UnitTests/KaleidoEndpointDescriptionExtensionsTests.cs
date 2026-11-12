using Kaleido.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Server.UnitTests;

// #553, #564: a Kaleido endpoint's errors, as OpenAPI reads them.
public sealed class KaleidoEndpointDescriptionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static IReadOnlyList<IProducesResponseTypeMetadata> Described(params int[] statusCodes)
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGet("/x", () => "x").ProducesKaleidoErrors(statusCodes);

        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
        return [.. endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Where(x => x.StatusCode >= 400)];
    }

    [Fact]
    public void EachStatus_IsAKaleidoErrorResponse_AndServerErrorsAlwaysAre()
    {
        var produced = Described(StatusCodes.Status403Forbidden, StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden);

        Assert.Equal([400, 403, 500], produced.Select(x => x.StatusCode));
        Assert.All(produced, x => Assert.Equal((typeof(KaleidoErrorResponse), "application/json"), (x.Type, Assert.Single(x.ContentTypes))));
    }

    [Fact]
    public void WithNoStatus_OnlyTheServerErrorIsDescribed()
    {
        Assert.Equal([500], Described().Select(x => x.StatusCode));
    }

    [Fact]
    public void ProducesKaleidoErrors_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((RouteHandlerBuilder)null!).ProducesKaleidoErrors());
    }
}
