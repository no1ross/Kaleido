using Kaleido.Http;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Observability;

public sealed class CorrelationEchoTests(
    QueryableAspNetCoreFixture fixture)
    : IClassFixture<QueryableAspNetCoreFixture>
{
    private HttpClient CreateClientWithHeaders(
        string? requestId = null,
        Guid? processId = null,
        Guid? processorInstanceId = null,
        string? sourceProcessor = null,
        string? stepName = null)
    {
        var client = fixture.Client;

        if (requestId is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.RequestId, requestId);
        if (processId.HasValue)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.ProcessId, processId.Value.ToString());
        if (processorInstanceId.HasValue)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.ProcessorInstanceId, processorInstanceId.Value.ToString());
        if (sourceProcessor is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.SourceProcessor, sourceProcessor);
        if (stepName is not null)
            client.DefaultRequestHeaders.Add(KaleidoCorrelationHeaders.StepName, stepName);

        return client;
    }

    [Fact]
    public async Task Request_WhenCorrelationHeadersSent_EchoesFullContext()
    {
        var processId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        var client = CreateClientWithHeaders(
            requestId: "req-77",
            processId: processId,
            processorInstanceId: instanceId,
            sourceProcessor: "intake",
            stepName: "Capture");

        var response = await client.GetAsync("/kaleido/queryable");

        response.EnsureSuccessStatusCode();

        Assert.Equal("req-77", Header(response, KaleidoCorrelationHeaders.RequestId));
        Assert.Equal(processId.ToString(), Header(response, KaleidoCorrelationHeaders.ProcessId));
        Assert.Equal(instanceId.ToString(), Header(response, KaleidoCorrelationHeaders.ProcessorInstanceId));
        Assert.Equal("intake", Header(response, KaleidoCorrelationHeaders.SourceProcessor));
        Assert.Equal("Capture", Header(response, KaleidoCorrelationHeaders.StepName));
    }

    [Fact]
    public async Task Request_WhenNoHeaders_EchoesGeneratedRequestIdOnly()
    {
        var response = await fixture.Client.GetAsync("/kaleido/queryable");

        response.EnsureSuccessStatusCode();

        Assert.False(
            string.IsNullOrWhiteSpace(
                Header(response, KaleidoCorrelationHeaders.RequestId)));
        Assert.Null(Header(response, KaleidoCorrelationHeaders.ProcessId));
        Assert.Null(Header(response, KaleidoCorrelationHeaders.StepName));
    }

    private static string? Header(
        HttpResponseMessage response,
        string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(",", values)
            : null;
}
