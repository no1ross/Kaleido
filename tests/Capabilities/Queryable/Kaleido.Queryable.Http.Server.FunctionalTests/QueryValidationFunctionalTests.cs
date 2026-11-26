using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Http.Server;
using Kaleido.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Http.Server.FunctionalTests.Validation;

public sealed class QueryValidationFunctionalTests
{
    private const string SourceUrl = "/validation/1/queryable/validationmembersource/query";
    private const string Marker = "Zq7Marker-Ava-1987-03-14";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static TheoryData<string, string, string> Refusals => new()
    {
        { "a filter value", $$"""{ "query": { "filter": { "condition": { "field":"MemberId", "operator":"equals", "values":["{{Marker}}"] } } } }""", "a valid Guid" },
        { "a filter operator", $$"""{ "query": { "filter": { "condition": { "field":"MemberId", "operator":"{{Marker}}", "values":[] } } } }""", "Expected one of: equals" },
        { "a logical operator", $$"""{ "query": { "filter": { "group": { "operator":"{{Marker}}", "filters":[] } } } }""", "Expected 'and' or 'or'" },
        { "a sort direction", $$"""{ "query": { "sort": [ { "field":"Name", "direction":"{{Marker}}" } ] } }""", "Expected 'ascending' or 'descending'" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusal_NamesExpectedInputWithoutEchoingTheSentValue(string what, string json, string guidance)
    {
        var (app, logs) = await StartAsync();
        await using (app)
        {
            using var response = await PostAsync(app, json);
            var body = await response.Content.ReadAsStringAsync(Cancel);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(body);
            var error = document.RootElement.GetProperty("errors")[0];
            Assert.StartsWith("qry_", error.GetProperty("code").GetString(), StringComparison.Ordinal);
            Assert.Contains(guidance, error.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.False(body.Contains(Marker, StringComparison.Ordinal), $"{what}: rejected input escaped in the 400 body.");
            Assert.Contains(logs, entry => entry.Level == LogLevel.Warning && entry.Text.Contains("Validation failed [qry_", StringComparison.Ordinal));
            Assert.False(logs.Any(entry => entry.Text.Contains(Marker, StringComparison.Ordinal)), $"{what}: rejected input escaped into a log.");
        }
    }

    public static TheoryData<string, string> InvalidValues => new()
    {
        { """{ "field":"Status", "operator":"equals", "values":["Retired"] }""", "qry_invalid_filter_value" },
        { """{ "field":"Status", "operator":"equals", "values":[999] }""", "qry_invalid_filter_value" },
        { """{ "field":"Level", "operator":"equals", "values":[300] }""", "qry_invalid_filter_value" },
        { """{ "field":"Big", "operator":"equals", "values":[18446744073709551616] }""", "qry_invalid_filter_value" },
        { """{ "field":"Mid", "operator":"equals", "values":[-1] }""", "qry_invalid_filter_value" },
        { """{ "field":"Level", "operator":"999", "values":[1] }""", "qry_unsupported_operator" },
    };

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public async Task InvalidTypedValueOrOperator_ReturnsItsStableCode(string condition, string code)
    {
        var (app, _) = await StartAsync();
        await using (app)
        {
            using var response = await PostAsync(app, $$$"""{ "query": { "filter": { "condition": {{{condition}}} } } }""");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
            Assert.Equal(code, document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("""{ "query": { "filter": { "group": { "operator":"999", "filters":[ { "condition": { "field":"Level", "operator":"equals", "values":[1] } } ] } } } }""", "qry_invalid_logical_operator")]
    [InlineData("""{ "query": { "sort": [ { "field":"Name", "direction":"999" } ] } }""", "qry_invalid_sort_direction")]
    public async Task UndefinedGroupOrSortOperator_ReturnsAClientError(string json, string code)
    {
        var (app, _) = await StartAsync();
        await using (app)
        {
            using var response = await PostAsync(app, json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
            Assert.Equal(code, document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("Mid", "4294967295")]
    [InlineData("Big", "18446744073709551615")]
    public async Task LargestUnsignedValue_ReachesTheQuery(string field, string value)
    {
        var (app, _) = await StartAsync();
        await using (app)
        {
            using var response = await PostAsync(app, $$$"""{ "query": { "filter": { "condition": { "field":"{{{field}}}", "operator":"equals", "values":[{{{value}}}] } } } }""");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
            Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        }
    }

    private static async Task<(WebApplication App, ConcurrentQueue<(LogLevel Level, string Text)> Logs)> StartAsync()
    {
        var logs = new ConcurrentQueue<(LogLevel Level, string Text)>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(new CapturingLoggerProvider(logs)));
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(store => store.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "validation",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryValidationFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(ValidationMemberSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp();
        var app = builder.Build();
        app.MapQueryable();
        await app.StartAsync(Cancel);
        return (app, logs);
    }

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, string json)
    {
        using var client = app.GetTestClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(SourceUrl, content, Cancel);
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<(LogLevel Level, string Text)> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(logs);
        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, string Text)> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            logs.Enqueue((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception.Message)));
    }

    public enum MemberStatus { Active, Inactive }

    public sealed class ValidationMember : IQueryContext
    {
        [Filterable(FilterOperator.Equals)]
        public Guid MemberId { get; init; }
        [Sortable]
        public string Name { get; init; } = string.Empty;
        [Filterable(FilterOperator.Equals)]
        public MemberStatus Status { get; init; }
        [Filterable(FilterOperator.Equals)]
        public byte Level { get; init; }
        [Filterable(FilterOperator.Equals)]
        public uint Mid { get; init; }
        [Filterable(FilterOperator.Equals)]
        public ulong Big { get; init; }
    }

    [QuerySource(DisplayName = "Members", Description = "Member records for query validation")]
    public sealed class ValidationMemberSource : IQuerySource<ValidationMember>
    {
        public IQueryable<ValidationMember> CreateQuery(QueryExecutionContext executionContext) => new[]
        {
            new ValidationMember { Name = "Ava", Mid = uint.MaxValue, Big = ulong.MaxValue },
            new ValidationMember { Name = "Ben", Mid = 1, Big = 1 },
        }.AsQueryable();
    }
}
