using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoJsonEndpointFilterTests
    : SutFixture
{
    private static KaleidoJsonEndpointFilter CreateSut() => new();

    private readonly KaleidoJsonEndpointFilter _sut = CreateSut();

    [Fact]
    public async Task InvokeAsync_WhenResultIsValueResult_WrapsInJsonResult()
    {
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext());

        var result = await _sut.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(
                Results.Ok(new { Name = "test" })));

        Assert.IsType<JsonHttpResult<object>>(result);
    }

    [Fact]
    public async Task InvokeAsync_WhenResultIsNoContent_PassesThroughUnchanged()
    {
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext());

        var expected = Results.NoContent();

        var result = await _sut.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WhenResultIsNotFound_PassesThroughUnchanged()
    {
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext());

        var expected = Results.NotFound();

        var result = await _sut.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(expected));

        Assert.Same(expected, result);
    }
}
