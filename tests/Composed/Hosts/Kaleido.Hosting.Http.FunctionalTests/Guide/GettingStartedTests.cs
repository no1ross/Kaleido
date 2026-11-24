using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Guide.Examples.Queries;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Hosting.Http.FunctionalTests.Guide;

// docs/guide/getting-started.md, run as written: the orders service from its compiled examples,
// called over HTTP the way the guide shows.
public sealed class GettingStartedTests : IAsyncLifetime
{
    private WebApplication? _host;
    private WebApplication? _registry;
    private static readonly int[] InvalidFilterValues = [2];
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _registry = await StartRegistryAsync();
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "http://localhost/" });
        OrdersService.AddOrders(builder.Services, builder.Configuration);
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = _registry.GetTestServer().CreateHandler(),
            }));
        _host = builder.Build();
        OrdersService.MapOrders(_host);
        await _host.StartAsync(Cancel);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            await _host.DisposeAsync();
        }

        if (_registry is not null)
        {
            await _registry.StopAsync(CancellationToken.None);
            await _registry.DisposeAsync();
        }
    }

    [Fact]
    public async Task PlacingAnOrder_StartsAProcess_ThatRequiresTheConfirmation_AndTheOrderCanBeQueried()
    {
        using var client = _host!.GetTestClient();
        using var first = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item = "Pen" } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var placedResponse = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item = "Book", quantity = 2 } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, placedResponse.StatusCode);
        var placed = await placedResponse.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        var step = Assert.Single(placed.GetProperty("steps").EnumerateArray());
        Assert.Equal("completed", step.GetProperty("executionStatus").GetString());
        Assert.Equal(2, step.GetProperty("response").GetProperty("number").GetInt32());
        Assert.Equal("ConfirmOrderStep", placed.GetProperty("requiredStep").GetString());

        var processId = placed.GetProperty("processId").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders/1/processes/steps/confirmorderstep")
        {
            Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        using var confirmation = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var confirmed = await confirmation.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        Assert.Equal("completed", Assert.Single(confirmed.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());

        using var query = await client.PostAsJsonAsync("/orders/1/queryable/ordersource/query", new { query = new { searchText = "Book" } }, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        var orders = await query.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        var order = Assert.Single(orders.GetProperty("results").EnumerateArray());
        Assert.Equal("Book", order.GetProperty("item").GetString());
        Assert.Equal(2, order.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task TheRegistry_PublishesTheStepsAndTheSource()
    {
        using var client = _registry!.GetTestClient();
        using var response = await client.GetAsync("/registry", Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PlaceOrderStep", body, StringComparison.Ordinal);
        Assert.Contains("ConfirmOrderStep", body, StringComparison.Ordinal);
        Assert.Contains("OrderSource", body, StringComparison.Ordinal);
        var registry = Assert.IsType<KaleidoRegistry>(await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, Cancel));
        var steps = Assert.Single(registry.Processes).Steps;
        Assert.NotNull(steps);
        var answer = Assert.Single(steps, step => step.Name == "GiftWrapAnswersStep");
        Assert.True(answer.IsInformationStep);
        Assert.Empty(answer.Fields);
        Assert.False(Assert.Single(steps, step => step.Name == "GiftWrapStep").IsInformationStep);
    }

    [Fact]
    public async Task AView_ProjectsTheSource_AndPagesOnItsOwn()
    {
        using var client = _host!.GetTestClient();
        foreach (var (item, quantity) in new[] { ("Book", 1), ("Pen", 2), ("Lamp", 3) })
        {
            using var placed = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item, quantity } }, KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        }

        var page = await PostQueryAsync(client, "ordersource/orderlineview", new { query = new { page = new { size = 2, offset = 0 } } });

        Assert.Equal(["#1 Book", "#2 Pen"], page.GetProperty("results").EnumerateArray().Select(result => result.GetProperty("label").GetString()));
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AViewWithParameters_ReadsThemFromTheQuery()
    {
        using var client = _host!.GetTestClient();
        foreach (var (item, quantity) in new[] { ("Book", 1), ("Pens", 8) })
        {
            using var placed = await client.PostAsJsonAsync("/orders/1/processes/steps/placeorderstep", new { processStep = new { item, quantity } }, KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        }

        var large = await PostQueryAsync(client, "ordersource/largeordersview", new { parameters = new { minimumQuantity = 5 } });

        Assert.Equal("Pens", Assert.Single(large.GetProperty("results").EnumerateArray()).GetProperty("item").GetString());
    }

    [Fact]
    public async Task ADelegatedSource_AnswersItself_WithItsParameters()
    {
        using var client = _host!.GetTestClient();

        var rates = await PostQueryAsync(client, "shippingratesource", new { parameters = new { country = "NL" } });

        Assert.Equal(["post", "courier"], rates.GetProperty("results").EnumerateArray().Select(rate => rate.GetProperty("carrier").GetString()));
        Assert.Equal(2, rates.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AFilterTheQueryContextDoesNotDeclare_IsA400()
    {
        using var client = _host!.GetTestClient();
        using var response = await client.PostAsJsonAsync("/orders/1/queryable/ordersource/query", new
        {
            query = new { filter = new { condition = new { field = "Quantity", @operator = "equals", values = InvalidFilterValues } } },
        }, KaleidoJsonOptions.Options, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ASourceFilteredByTheCaller_ReturnsOnlyTheCallersRows()
    {
        await using var app = await StartAuthenticatedQueryHostAsync();
        using var ann = app.GetTestClient();
        ann.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("ann"));
        using var ben = app.GetTestClient();
        ben.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("ben"));

        var annResult = await PostQueryAsync(ann, "myinvoicessource", new { query = new { } });
        var benResult = await PostQueryAsync(ben, "myinvoicessource", new { query = new { } });

        Assert.Equal([1, 3], annResult.GetProperty("results").EnumerateArray().Select(invoice => invoice.GetProperty("number").GetInt32()));
        Assert.Equal([2], benResult.GetProperty("results").EnumerateArray().Select(invoice => invoice.GetProperty("number").GetInt32()));
    }

    [Fact]
    public async Task ARejectedShippingStep_CanBeCorrectedWithoutChangingTheProcess()
    {
        using var client = _host!.GetTestClient();
        var processId = await ConfirmOrderAsync(client);

        var rejected = await ExecuteStepAsync(client, "shiporderstep", new { carrier = "courier", address = "1 Main St" }, processId);
        var shipped = await ExecuteStepAsync(client, "shiporderstep", new { carrier = "post", address = "1 Main St" }, processId);

        Assert.Equal("rejected", Assert.Single(rejected.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Contains("ShipOrderStep", Available(rejected));
        Assert.Equal("completed", Assert.Single(shipped.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
    }

    [Fact]
    public async Task AFailedShippingStep_EndsTheProcess()
    {
        using var client = _host!.GetTestClient();
        var processId = await ConfirmOrderAsync(client);

        var failed = await ExecuteStepAsync(client, "shiporderstep", new { carrier = "post", address = "Base 1, Antarctica" }, processId);
        using var stateResponse = await client.GetAsync($"/orders/1/processes/{processId:D}", Cancel);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var state = await stateResponse.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);

        Assert.Equal("failed", Assert.Single(failed.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Equal("businessFailure", state.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ARequiredConfirmation_HoldsAnotherStepPending()
    {
        using var client = _host!.GetTestClient();
        var placed = await ExecuteStepAsync(client, "placeorderstep", new { item = "Book" }, null);

        var note = await ExecuteStepAsync(client, "addnotestep", new { text = "Leave at the door" }, placed.GetProperty("processId").GetGuid());

        Assert.NotEqual("completed", Assert.Single(note.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Equal("ConfirmOrderStep", note.GetProperty("requiredStep").GetString());
    }

    [Fact]
    public async Task RepeatableNotes_RunAgainWhileConfirmationIsNotOfferedAgain()
    {
        using var client = _host!.GetTestClient();
        var processId = await ConfirmOrderAsync(client);

        var first = await ExecuteStepAsync(client, "addnotestep", new { text = "Leave at the door" }, processId);
        var second = await ExecuteStepAsync(client, "addnotestep", new { text = "Ring twice" }, processId);

        Assert.Equal("completed", Assert.Single(first.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Equal("completed", Assert.Single(second.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Contains("AddNoteStep", Available(second));
        Assert.DoesNotContain("ConfirmOrderStep", Available(second));
    }

    [Fact]
    public async Task AvailableUntil_ClosesCancellationAfterShippingAndShippingAfterCancellation()
    {
        using var client = _host!.GetTestClient();
        var shippedProcess = await ConfirmOrderAsync(client);
        var cancelledProcess = await ConfirmOrderAsync(client);

        var shipped = await ExecuteStepAsync(client, "shiporderstep", new { carrier = "post", address = "1 Main St" }, shippedProcess);
        var cancelled = await ExecuteStepAsync(client, "cancelorderstep", new { }, cancelledProcess);

        Assert.DoesNotContain("CancelOrderStep", Available(shipped));
        Assert.Contains("ReturnOrderStep", Available(shipped));
        Assert.DoesNotContain("ShipOrderStep", Available(cancelled));
    }

    private static string[] Available(JsonElement result) =>
        result.GetProperty("availableSteps").EnumerateArray().Select(step => step.GetString() ?? string.Empty).ToArray();

    private static async Task<Guid> ConfirmOrderAsync(HttpClient client)
    {
        var placed = await ExecuteStepAsync(client, "placeorderstep", new { item = "Book" }, null);
        var processId = placed.GetProperty("processId").GetGuid();
        await ExecuteStepAsync(client, "confirmorderstep", new { }, processId);
        return processId;
    }

    [Fact]
    public async Task TheGiftWrapHandlerAsks_AndItsAnswersCompleteTheInformationStep()
    {
        using var client = _host!.GetTestClient();
        var (processId, asked) = await AskGiftWrapAsync(client);
        var required = asked.GetProperty("requiredInformationRequest");

        var answered = await ExecuteStepAsync(client, "giftwrapanswersstep", GiftWrapAnswers(required, ("wrap", "true"), ("paper", "gold")), processId);

        Assert.Equal("GiftWrapAnswersStep", asked.GetProperty("requiredStep").GetString());
        Assert.Equal("gift-wrap", required.GetProperty("informationRequestId").GetString());
        var result = Assert.Single(answered.GetProperty("steps").EnumerateArray());
        Assert.Equal("completed", result.GetProperty("executionStatus").GetString());
        Assert.Equal("gold", result.GetProperty("response").GetProperty("paper").GetString());
    }

    [Fact]
    public async Task GiftWrap_WhenTheConditionIsFalse_NeedsNoPaperAnswer()
    {
        using var client = _host!.GetTestClient();
        var (processId, asked) = await AskGiftWrapAsync(client);

        var answered = await ExecuteStepAsync(client, "giftwrapanswersstep", GiftWrapAnswers(asked.GetProperty("requiredInformationRequest"), ("wrap", "false")), processId);

        var result = Assert.Single(answered.GetProperty("steps").EnumerateArray());
        Assert.Equal("completed", result.GetProperty("executionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("response").GetProperty("paper").ValueKind);
    }

    [Fact]
    public async Task GiftWrap_AnAnswerToADisabledQuestion_RemainsPending()
    {
        using var client = _host!.GetTestClient();
        var (processId, asked) = await AskGiftWrapAsync(client);

        var rejected = await ExecuteStepAsync(client, "giftwrapanswersstep",
            GiftWrapAnswers(asked.GetProperty("requiredInformationRequest"), ("wrap", "false"), ("paper", "red")), processId);

        Assert.NotEqual("completed", Assert.Single(rejected.GetProperty("steps").EnumerateArray()).GetProperty("executionStatus").GetString());
        Assert.Equal("gift-wrap", rejected.GetProperty("requiredInformationRequest").GetProperty("informationRequestId").GetString());
        using var stateResponse = await client.GetAsync($"/orders/1/processes/{processId:D}", Cancel);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var state = await stateResponse.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
        Assert.Equal("gift-wrap", state.GetProperty("requiredInformationRequest").GetProperty("informationRequestId").GetString());
    }

    private static async Task<(Guid ProcessId, JsonElement Asked)> AskGiftWrapAsync(HttpClient client)
    {
        var placed = await ExecuteStepAsync(client, "placeorderstep", new { item = "Book" }, null);
        var processId = placed.GetProperty("processId").GetGuid();
        await ExecuteStepAsync(client, "confirmorderstep", new { }, processId);
        return (processId, await ExecuteStepAsync(client, "giftwrapstep", new { }, processId));
    }

    private static object GiftWrapAnswers(JsonElement required, params (string Item, string Value)[] answers) => new
    {
        informationRequestId = required.GetProperty("informationRequestId").GetString(),
        exchangeId = required.GetProperty("exchangeId").GetString(),
        items = answers.Select(x => new { itemId = x.Item, answers = new[] { new { value = x.Value } } }).ToArray(),
    };

    private static async Task<JsonElement> ExecuteStepAsync(HttpClient client, string step, object input, Guid? processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/orders/1/processes/steps/{step}")
        {
            Content = JsonContent.Create(new { processStep = input }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        using var response = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
    }

    private static async Task<WebApplication> StartAuthenticatedQueryHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(MyInvoicesSource).Assembly],
            TypeFilter = type => type == typeof(MyInvoicesSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddInMemoryOutboxProvider()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }

    private static async Task<JsonElement> PostQueryAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync($"/orders/1/queryable/{path}/query", body, KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(KaleidoJsonOptions.Options, Cancel);
    }

    private static async Task<WebApplication> StartRegistryAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(GettingStartedTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry()
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.MapKaleido().MapCentralRegistry();
        await app.StartAsync(Cancel);
        return app;
    }

    private sealed class RegistrationHandler(TaskCompletionSource registered) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/registry/instances" && response.IsSuccessStatusCode)
            {
                registered.TrySetResult();
            }

            return response;
        }
    }
}
