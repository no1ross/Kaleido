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

// #520: a refused query names the field and what was expected, never what the caller sent. A filter
// value may be a member's name or identifier: it must reach neither the 400 body nor the Warning log.
public sealed class ValidationMessageTests : IAsyncLifetime
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
                        Assemblies = [typeof(ValidationMessageTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(ValidationMessageTests).Namespace,
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

public sealed class VmMember : IQueryContext
{
    [Filterable(FilterOperator.Equals)]
    public Guid MemberId { get; init; }

    [Sortable]
    public string Name { get; init; } = string.Empty;
}

[QuerySource(DisplayName = "Members", Description = "Members, to refuse queries against.")]
public sealed class VmMemberSource : IQuerySource<VmMember>
{
    public IQueryable<VmMember> CreateQuery(QueryExecutionContext executionContext) => Array.Empty<VmMember>().AsQueryable();
}
