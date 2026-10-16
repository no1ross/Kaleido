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
    public async Task InvokeAsync_KeepsTheStatusCodeOfAValueResult()
    {
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext());

        var result = await _sut.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(
                Results.Created("/items/1", new { Name = "test" })));

        var json = Assert.IsType<JsonHttpResult<object>>(result);
        Assert.Equal(StatusCodes.Status201Created, json.StatusCode);
        Assert.Same(KaleidoJsonOptions.Options, json.JsonSerializerOptions);
    }

    [Fact]
    public async Task InvokeAsync_WhenAValueResultHasNoStatusCode_AnswersOk()
    {
        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext());

        var result = await _sut.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(new ValueOnlyResult("payload")));

        var json = Assert.IsType<JsonHttpResult<object>>(result);
        Assert.Equal(StatusCodes.Status200OK, json.StatusCode);
        Assert.Equal("payload", json.Value);
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

    private sealed class ValueOnlyResult(object value)
        : IResult, IValueHttpResult
    {
        public object? Value => value;

        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }
}
