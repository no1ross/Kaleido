using Kaleido.Http.FunctionalTests.Queryable.Fixtures;

namespace Kaleido.Http.FunctionalTests.Queryable.Observability;

public sealed class CorrelationEchoTests(
    QueryableAspNetCoreFixture fixture)
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private HttpClient CreateClientWithHeaders(
        string? requestId = null,
        Guid? processId = null,
        string? callingProcessor = null,
        string? callingStep = null)
    {
        var client = fixture.TestServer.CreateClient();

        if (requestId is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.RequestId, requestId);
        if (processId.HasValue)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.ProcessId, processId.Value.ToString());
        if (callingProcessor is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.CallingProcessor, callingProcessor);
        if (callingStep is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.CallingStep, callingStep);

        return client;
    }

    [Fact]
    public async Task Request_WhenCorrelationHeadersSent_EchoesEndToEndOnly()
    {
        var processId = Guid.NewGuid();

        var client = CreateClientWithHeaders(
            requestId: "req-77",
            processId: processId,
            callingProcessor: "intake",
            callingStep: "Capture");

        var response = await client.GetAsync("/kaleido/registry");

        response.EnsureSuccessStatusCode();

        Assert.Equal("req-77", Header(response, KaleidoCorrelationHeaders.RequestId));
        Assert.Equal(processId.ToString(), Header(response, KaleidoCorrelationHeaders.ProcessId));
        // per-hop identity describes the request, not the response
        Assert.Null(Header(response, KaleidoCorrelationHeaders.CallingProcessor));
        Assert.Null(Header(response, KaleidoCorrelationHeaders.CallingStep));
        Assert.Null(Header(response, "X-Kaleido-Processor-Instance-Id"));
    }

    [Fact]
    public async Task Request_WhenNoHeaders_EchoesGeneratedRequestIdOnly()
    {
        var client = fixture.TestServer.CreateClient();

        var response = await client.GetAsync("/kaleido/registry");

        response.EnsureSuccessStatusCode();

        Assert.False(
            string.IsNullOrWhiteSpace(
                Header(response, KaleidoCorrelationHeaders.RequestId)));
        Assert.Null(Header(response, KaleidoCorrelationHeaders.ProcessId));
        Assert.Null(Header(response, KaleidoCorrelationHeaders.CallingStep));
    }

    private static string? Header(
        HttpResponseMessage response,
        string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(",", values)
            : null;
}
