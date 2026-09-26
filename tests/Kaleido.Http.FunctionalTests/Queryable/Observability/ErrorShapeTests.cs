using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Http.Queryable;
using Kaleido.Queryable.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Queryable.AspNetCore.FunctionalTests.Observability;

public sealed class ErrorShapeTests(
    QueryableAspNetCoreFixture fixture)
    : IClassFixture<QueryableAspNetCoreFixture>
{
    [Fact]
    public async Task Request_WhenQueryBodyIsInvalid_ReturnsValidationErrorShape()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/kaleido/queryable/functional-records/query")
        {
            Content = JsonContent.Create(
                new QueryApiRequest(
                    new QueryBody(
                        Filter: QueryFilterNode.CreateCondition(
                            "bogus-field",
                            FilterOperator.Equals,
                            "1"))))
        };

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var raw = await response.Content.ReadAsStringAsync();

        using var body = JsonDocument.Parse(raw);

        var error = body.RootElement
            .GetProperty("errors")[0];

        Assert.StartsWith("qry_", error.GetProperty("code").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(
                error.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Request_WhenBodyIsMalformedJson_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/kaleido/queryable/functional-records/query")
        {
            Content = new StringContent(
                "{ not json }",
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
