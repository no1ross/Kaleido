using System.Net;
using System.Net.Http.Json;
using Kaleido.Http;
using Kaleido.Http.Server;
using Kaleido.Eventing;
using Kaleido.Registry;
using Kaleido.Storage;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.Server.FunctionalTests;

public sealed class QueryableHttpServerFunctionalTests
{
    private const string SourceUrl = "/local/1/queryable/localrecordssource/query";
    private const string ViewUrl = "/local/1/queryable/localrecordssource/localrecordsview/query";
    private static readonly int[] InvalidFilterValues = [1];
    private static readonly string[] ReaderRoles = ["reader"];
    private static readonly string[] ViewerRoles = ["viewer"];

    [Fact]
    public async Task LocalSource_QueriesAndPagesWithoutACentralRegistry()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(SourceUrl, new { query = new { page = new { size = 1, offset = 1 } } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("results")[0].GetProperty("id").GetInt32());
        Assert.True(response.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Fact]
    public async Task AService_MapsOnlyItsVersionedQueryRoute()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var request = new { query = new { } };
        using var versioned = await client.PostAsJsonAsync(SourceUrl, request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        using var unversioned = await client.PostAsJsonAsync("/local/queryable/localrecordssource/query", request, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
    }

    [Fact]
    public async Task AQuery_EchoesOnlyEndToEndCorrelation_NotInboundPerHopIdentity()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var processId = Guid.NewGuid();
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, "req-77");
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, "intake");
        client.DefaultRequestHeaders.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingStep, "Capture");

        using var response = await client.PostAsJsonAsync(SourceUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("req-77", Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.RequestId)));
        Assert.Equal(processId.ToString("D"), Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.CallingProcessor));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
        Assert.False(response.Headers.Contains("X-Kaleido-Processor-Instance-Id"));
    }

    [Fact]
    public async Task AQueryWithoutCorrelationHeaders_GeneratesOnlyItsRequestId()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(SourceUrl, new { query = new { } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.RequestId))));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.CallingStep));
    }

    [Fact]
    public async Task LocalView_UsesItsOwnPageAndProjectsResults()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(ViewUrl, new { query = new { page = new { size = 1, offset = 0 } } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal("one", body.GetProperty("results")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidQuery_AnswersAQueryableError()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(SourceUrl, new { query = new { filter = new { condition = new { field = "id", @operator = "bogus", values = InvalidFilterValues } } } }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("qry_unsupported_operator", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnrelatedEndpoint_HasNoKaleidoCorrelationHeaders()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("host", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.RequestId));
    }

    [Theory]
    [InlineData("text/plain", "{}", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("application/json", "{broken", HttpStatusCode.BadRequest)]
    public async Task InvalidBody_UsesExpectedContentTypeAndJsonFailureContract(string contentType, string body, HttpStatusCode expectedStatus)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, SourceUrl)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, contentType),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.UnsupportedMediaType)
        {
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal("argument_error", error.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeclaredSourceRole_UsesHostAuthenticationAndAuthorization()
    {
        await using var app = await StartAsync(KaleidoAuthorizationMode.Authenticated);
        using var client = app.GetTestClient();
        using var anonymous = await client.PostAsJsonAsync(SourceUrl, new { }, KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Post, SourceUrl)
        {
            Content = JsonContent.Create(new { }, options: KaleidoJsonOptions.Options),
        };
        deniedRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice"));
        using var denied = await client.SendAsync(deniedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var allowedRequest = new HttpRequestMessage(HttpMethod.Post, SourceUrl)
        {
            Content = JsonContent.Create(new { }, options: KaleidoJsonOptions.Options),
        };
        allowedRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ReaderRoles));
        using var allowed = await client.SendAsync(allowedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var hostEndpoint = await client.GetAsync("/ordinary", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, hostEndpoint.StatusCode);
    }

    [Fact]
    public async Task ViewAuthorization_UsesItsOwnRoleInsteadOfTheSourceRole()
    {
        await using var app = await StartAsync(KaleidoAuthorizationMode.Authenticated);
        using var client = app.GetTestClient();
        using var deniedRequest = new HttpRequestMessage(HttpMethod.Post, ViewUrl)
        {
            Content = JsonContent.Create(new { }, options: KaleidoJsonOptions.Options),
        };
        deniedRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ReaderRoles));
        using var denied = await client.SendAsync(deniedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var allowedRequest = new HttpRequestMessage(HttpMethod.Post, ViewUrl)
        {
            Content = JsonContent.Create(new { }, options: KaleidoJsonOptions.Options),
        };
        allowedRequest.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("alice", ViewerRoles));
        using var allowed = await client.SendAsync(allowedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private static async Task<WebApplication> StartAsync(KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (mode != KaleidoAuthorizationMode.None)
        {
            builder.Services.AddTestJwtBearer();
        }
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "local",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryableHttpServerFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(LocalRecordsSource) || type == typeof(LocalRecordsView),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = mode })
            .AddHttp();

        var app = builder.Build();
        if (mode != KaleidoAuthorizationMode.None)
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapQueryable();
        app.MapGet("/ordinary", () => Results.Text("host"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public sealed class LocalRecord : IQueryContext
    {
        [Filterable(FilterOperator.Equals)]
        [Sortable]
        public int Id { get; init; }

        public string Code { get; init; } = string.Empty;
    }

    public sealed class ProjectedRecord
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Local records", Description = "Local test records", DefaultSortField = nameof(LocalRecord.Id))]
    [KaleidoAuthorization(Roles = "reader")]
    [Pageable(DefaultSize = 2, MaxSize = 5)]
    public sealed class LocalRecordsSource : IQuerySource<LocalRecord>
    {
        public IQueryable<LocalRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[]
            {
                new LocalRecord { Id = 3, Code = "three" },
                new LocalRecord { Id = 1, Code = "one" },
                new LocalRecord { Id = 2, Code = "two" },
            }.AsQueryable();
    }

    [QueryView(DisplayName = "Local view", Description = "Projected local records", DefaultSortField = nameof(LocalRecord.Id))]
    [KaleidoAuthorization(Roles = "viewer")]
    [Pageable(DefaultSize = 1, MaxSize = 3)]
    public sealed class LocalRecordsView : IQueryViewSource<LocalRecordsSource, LocalRecord, ProjectedRecord>
    {
        public IQueryable<ProjectedRecord> CreateView(IQueryable<LocalRecord> query, QueryExecutionContext executionContext) =>
            query.Select(x => new ProjectedRecord { Id = x.Id, Code = x.Code });
    }
}
