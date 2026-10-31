using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Http.Queryable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.FunctionalTests.Queryable.Validation;

// What a refused query answers, through a real host:
// - #520: it names the field and what was expected, never what the caller sent. A filter value may be a
//   member's name or identifier: it must reach neither the 400 body nor the Warning log.
// - #519: every input the query cannot use is a stable 4xx with its code, never a 500 or a silent Or;
//   and the whole range of an unsigned field reaches the query.
public sealed class QueryValidationTests : IAsyncLifetime
{
    private const string ServiceName = "validation";
    private const string Marker = "Zq7Marker-Ava-1987-03-14";

    private readonly ConcurrentQueue<(LogLevel Level, string Text)> _logs = new();
    private IHost? _host;
    private HttpClient _client = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(new CapturingLoggerProvider(_logs)));
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = ServiceName,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(QueryValidationTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(QueryValidationTests).Namespace,
                    })
                        .AddInMemoryStores()
                        .AddHttp();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }
    }

    public static TheoryData<string, string, string> Refusals => new()
    {
        { "a filter value", $$"""{ "query": { "filter": { "condition": { "field":"MemberId", "operator":"equals", "values":["{{Marker}}"] } } } }""", "a valid Guid" },
        { "a filter operator", $$"""{ "query": { "filter": { "condition": { "field":"MemberId", "operator":"{{Marker}}", "values":[] } } } }""", "Expected one of: equals" },
        { "a logical operator", $$"""{ "query": { "filter": { "group": { "operator":"{{Marker}}", "filters":[] } } } }""", "Expected 'and' or 'or'" },
        { "a sort direction", $$"""{ "query": { "sort": [ { "field":"Name", "direction":"{{Marker}}" } ] } }""", "Expected 'ascending' or 'descending'" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task ARefusal_NamesWhatWasExpected_ButNotWhatWasSent(string what, string body, string guidance)
    {
        using var response = await _client.PostAsync(
            QueryableContractUrls.QuerySourceQuery(ServiceName, nameof(VmMemberSource)),
            new StringContent(body, Encoding.UTF8, "application/json"),
            Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(text);
        var error = document.RootElement.GetProperty("errors")[0];
        Assert.StartsWith("qry_", error.GetProperty("code").GetString(), StringComparison.Ordinal);
        Assert.Contains(guidance, error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(text.Contains(Marker, StringComparison.Ordinal), $"{what}: the 400 body echoes the value: {text}");

        var warnings = _logs.Where(x => x.Level == LogLevel.Warning).Select(x => x.Text).ToArray();
        Assert.Contains(warnings, x => x.Contains("Validation failed [qry_", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs, x => x.Text.Contains(Marker, StringComparison.Ordinal));
    }

    public static TheoryData<string, string> Refused => new()
    {
        { """{ "field":"Status", "operator":"equals", "values":["Retired"] }""", "qry_invalid_filter_value" },
        { """{ "field":"Status", "operator":"equals", "values":[999] }""", "qry_invalid_filter_value" },
        { """{ "field":"Level", "operator":"equals", "values":[300] }""", "qry_invalid_filter_value" },
        { """{ "field":"Big", "operator":"equals", "values":[18446744073709551616] }""", "qry_invalid_filter_value" },
        { """{ "field":"Mid", "operator":"equals", "values":[-1] }""", "qry_invalid_filter_value" },
        { """{ "field":"Level", "operator":"999", "values":[1] }""", "qry_unsupported_operator" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task AnInputTheQueryCannotUse_IsAStable400_WithItsCode(string condition, string code)
    {
        var (status, error) = await QueryAsync($$$"""{ "query": { "filter": { "condition": {{{condition}}} } } }""");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(code, error);
    }

    [Theory]
    [InlineData("""{ "query": { "filter": { "group": { "operator":"999", "filters":[ { "condition": { "field":"Level", "operator":"equals", "values":[1] } } ] } } } }""", "qry_invalid_logical_operator")]
    [InlineData("""{ "query": { "sort": [ { "field":"Name", "direction":"999" } ] } }""", "qry_invalid_sort_direction")]
    public async Task AnUndefinedLogicalOperatorOrSortDirection_IsA400_NotASilentOrOrA500(string body, string code)
    {
        var (status, error) = await QueryAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(code, error);
    }

    [Theory]
    [InlineData("Mid", "4294967295")]
    [InlineData("Big", "18446744073709551615")]
    public async Task TheLargestUnsignedValue_ReachesTheQuery(string field, string value)
    {
        using var response = await _client.PostAsync(
            QueryableContractUrls.QuerySourceQuery(ServiceName, nameof(VmMemberSource)),
            new StringContent($$$"""{ "query": { "filter": { "condition": { "field":"{{{field}}}", "operator":"equals", "values":[{{{value}}}] } } } }""", Encoding.UTF8, "application/json"),
            Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
    }

    private async Task<(HttpStatusCode Status, string? Code)> QueryAsync(string body)
    {
        using var response = await _client.PostAsync(
            QueryableContractUrls.QuerySourceQuery(ServiceName, nameof(VmMemberSource)),
            new StringContent(body, Encoding.UTF8, "application/json"),
            Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return (response.StatusCode, text);
        }

        using var document = JsonDocument.Parse(text);
        return (response.StatusCode, document.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
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
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            logs.Enqueue((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception.Message)));
    }
}

public enum VmStatus
{
    Active,
    Inactive,
}

public sealed class VmMember : IQueryContext
{
    [Filterable(FilterOperator.Equals)]
    public Guid MemberId { get; init; }

    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    public VmStatus Status { get; init; }

    [Filterable(FilterOperator.Equals)]
    public byte Level { get; init; }

    [Filterable(FilterOperator.Equals)]
    public uint Mid { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ulong Big { get; init; }
}

[QuerySource(DisplayName = "Members", Description = "Members, to query and to refuse queries against.")]
public sealed class VmMemberSource : IQuerySource<VmMember>
{
    public IQueryable<VmMember> CreateQuery(QueryExecutionContext executionContext) =>
        new[]
        {
            new VmMember { Name = "Ava", Mid = uint.MaxValue, Big = ulong.MaxValue },
            new VmMember { Name = "Ben", Mid = 1, Big = 1 },
        }.AsQueryable();
}
