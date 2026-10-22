using System.Net;
using System.Text.Json;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/queries.md, run: views, view parameters, a delegated source, a caller-filtered source,
// and what an undeclared filter gets.
public sealed class QueriesExampleTests
{
    private static async Task PlaceAsync(GuideHost host, string item, int quantity) =>
        await host.StepAsync("PlaceOrderStep", new { item, quantity });

    private static List<JsonElement> Results(JsonElement answer) => [.. answer.GetProperty("results").EnumerateArray()];

    [Fact]
    public async Task AView_ProjectsTheSource_AndPagesOnItsOwn()
    {
        await using var host = await GuideHost.StartAsync();
        await PlaceAsync(host, "Book", 1);
        await PlaceAsync(host, "Pen", 2);
        await PlaceAsync(host, "Lamp", 3);

        var page = await host.QueryAsync("OrderSource/OrderLineView", new { query = new { page = new { size = 2, offset = 0 } } });

        Assert.Equal(["#1 Book", "#2 Pen"], Results(page).Select(x => x.GetProperty("label").GetString()));
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AViewWithParameters_ReadsThemFromTheQuery()
    {
        await using var host = await GuideHost.StartAsync();
        await PlaceAsync(host, "Book", 1);
        await PlaceAsync(host, "Pens", 8);

        var large = await host.QueryAsync("OrderSource/LargeOrdersView", new { parameters = new { minimumQuantity = 5 } });

        Assert.Equal("Pens", Assert.Single(Results(large)).GetProperty("item").GetString());
    }

    [Fact]
    public async Task ADelegatedSource_AnswersItself_WithItsParameters()
    {
        await using var host = await GuideHost.StartAsync();

        var rates = await host.QueryAsync("ShippingRateSource", new { parameters = new { country = "NL" } });

        Assert.Equal(["post", "courier"], Results(rates).Select(x => x.GetProperty("carrier").GetString()));
        Assert.Equal(2, rates.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ASourceFilteredByTheCaller_ReturnsOnlyTheCallersRows()
    {
        await using var host = await GuideHost.StartAsync(KaleidoAuthorizationMode.Authenticated);

        var ann = await host.QueryAsync("MyInvoicesSource", new { }, caller: "ann");
        var ben = await host.QueryAsync("MyInvoicesSource", new { }, caller: "ben");

        Assert.Equal([1, 3], Results(ann).Select(x => x.GetProperty("number").GetInt32()));
        Assert.Equal([2], Results(ben).Select(x => x.GetProperty("number").GetInt32()));
    }

    [Fact]
    public async Task AFilterTheQueryContextDoesNotDeclare_IsA400()
    {
        await using var host = await GuideHost.StartAsync();

        var (status, body) = await host.SendAsync(HttpMethod.Post, "/orders/queryable/OrderSource/query", new
        {
            query = new { filter = new { condition = new { field = "Quantity", @operator = "Equals", values = new List<int> { 2 } } } },
        });

        Assert.True(status == HttpStatusCode.BadRequest, $"{(int)status} {body}");
    }
}
