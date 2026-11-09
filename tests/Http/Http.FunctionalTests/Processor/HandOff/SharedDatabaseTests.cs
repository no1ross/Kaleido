using System.Net;
using Kaleido.Client.Processor;
using Kaleido.Http.FunctionalTests.Processor.HandOff.Origin;
using Kaleido.Http.FunctionalTests.Processor.HandOff.Target;
using Kaleido.Http.Processor;
using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Platform.Processor;
using Kaleido.Platform.Registry;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.HandOff;

// #552: services, and versions of one service, on one SQLite database. Every record is scoped by the
// service and its version, so a hand-off keeps the process id with a row on each side, a process of one
// version is not found by another, and an idempotency key does not replay across versions.
public sealed class SharedDatabaseTests : IAsyncLifetime
{
    private const string Version1 = "1";
    private const string Version2 = "2";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-shared-{Guid.NewGuid():N}.db");
    private readonly List<IHost> _hosts = [];
    private TestControlRegistry? _registry;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private string ConnectionString => $"Data Source={_databasePath}";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

        if (_registry is not null)
        {
            await _registry.DisposeAsync();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    // The origin's process and the target's, under one id in one database: each its own row.
    [Fact]
    public async Task AHandOff_OnOneDatabase_KeepsEachSidesProcessUnderTheSameId()
    {
        var target = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, serviceVersion: null, targetServer: null);
        var origin = await StartAsync(HandOffFixture.OriginService, typeof(ReturnOrderStep).Namespace!, serviceVersion: null, target.GetTestServer());

        var placed = await ReadAsync<StepExecutionResponse>(await PostAsync(origin, HandOffFixture.OriginService, nameof(PlaceOrderStep), new { }, processId: null));
        var handedOff = await ReadAsync<StepExecutionResponse>(await PostAsync(origin, HandOffFixture.OriginService, nameof(ReturnOrderStep), new { next = ReturnAnswers.Ordinary }, placed.ProcessId));
        var inspected = await ReadAsync<StepExecutionResponse>(await PostAsync(target, HandOffFixture.TargetService, nameof(InspectReturnStep), new { }, placed.ProcessId));

        Assert.Equal(HandOffFixture.TargetService, handedOff.TargetProcessorName);
        Assert.Equal(StepExecutionStatus.Completed, inspected.Status);
        Assert.Equal(
            [HandOffFixture.OriginService, HandOffFixture.TargetService],
            await ProcessorsOfAsync(placed.ProcessId));
        Assert.Equal(ProcessExecutionState.HandOff, (await StateAsync(origin, HandOffFixture.OriginService, placed.ProcessId)).State);
    }

    // A process belongs to the version that created it: version 2 does not find version 1's.
    [Fact]
    public async Task AProcessOfVersion1_IsNotFoundByVersion2()
    {
        var v1 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version1, targetServer: null);
        var v2 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version2, targetServer: null);
        var opened = await ReadAsync<StepExecutionResponse>(await PostAsync(v1, HandOffFixture.TargetService, nameof(OpenReturnStep), new { next = ReturnAnswers.Ordinary }, processId: null));

        using var onVersion2 = await PostAsync(v2, HandOffFixture.TargetService, nameof(InspectReturnStep), new { }, opened.ProcessId);
        var onVersion1 = await ReadAsync<StepExecutionResponse>(await PostAsync(v1, HandOffFixture.TargetService, nameof(InspectReturnStep), new { }, opened.ProcessId));

        Assert.Equal(HttpStatusCode.BadRequest, onVersion2.StatusCode);
        Assert.Contains(ProcessorErrorCodes.ProcessNotFound, await onVersion2.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(StepExecutionStatus.Completed, onVersion1.Status);
        Assert.Equal([Version1], await VersionsOfAsync(opened.ProcessId));
    }

    [Fact]
    public async Task ARegistryDrivenClient_RoutesEachVersionToItsOwnHost()
    {
        var v1 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version1, targetServer: null);
        var v2 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version2, targetServer: null);
        var opened = await ReadAsync<StepExecutionResponse>(await PostAsync(v1, HandOffFixture.TargetService, nameof(OpenReturnStep), new { next = ReturnAnswers.Ordinary }, processId: null));
        var gateway = new VersionRouter(v1.GetTestServer().CreateHandler(), v2.GetTestServer().CreateHandler());
        var services = new ServiceCollection();
        services.AddSingleton<IKaleidoRegistrySource>(v2.Services.GetRequiredService<IKaleidoRegistrySource>());
        services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://gateway.test/",
        }).Build(), new()
        {
            ServiceName = "client",
            ServiceVersion = "1",
            AuthorizationMode = KaleidoAuthorizationMode.None,
            Assemblies = [typeof(KaleidoServiceOptions).Assembly],
            TypeFilter = _ => false,
        }).AddInMemoryStores().AddHttpClients();
        services.AddHttpClient(KaleidoClientTransport.ClientName).ConfigurePrimaryHttpMessageHandler(() => gateway);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>();

        var fromV1 = await clients.GetClient(HandOffFixture.TargetService, Version1).GetProcessStateAsync(opened.ProcessId, Cancel);
        var fromV2 = await clients.GetClient(HandOffFixture.TargetService, Version2).GetProcessStateAsync(opened.ProcessId, Cancel);

        Assert.Equal(opened.ProcessId, fromV1?.ProcessId);
        Assert.Null(fromV2);
        Assert.Equal(["/returns/1", "/returns/2"], gateway.RoutedTo);
    }

    // The same key and request sent to another version runs there: a key never replays across versions.
    [Fact]
    public async Task AnIdempotencyKey_ReplaysOnItsVersion_NotOnAnother()
    {
        var v1 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version1, targetServer: null);
        var v2 = await StartAsync(HandOffFixture.TargetService, typeof(OpenReturnStep).Namespace!, Version2, targetServer: null);
        var key = Guid.NewGuid().ToString();
        var body = new { next = ReturnAnswers.Nothing };

        using var first = await PostAsync(v1, HandOffFixture.TargetService, nameof(OpenReturnStep), body, processId: null, key);
        using var again = await PostAsync(v1, HandOffFixture.TargetService, nameof(OpenReturnStep), body, processId: null, key);
        using var onVersion2 = await PostAsync(v2, HandOffFixture.TargetService, nameof(OpenReturnStep), body, processId: null, key);

        Assert.True(again.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.False(onVersion2.Headers.Contains(KaleidoIdempotencyHeaders.Replayed));
        Assert.NotEqual(
            (await first.Content.ReadFromJsonAsync<StepExecutionResponse>(KaleidoJsonOptions.Options, Cancel))!.ProcessId,
            (await onVersion2.Content.ReadFromJsonAsync<StepExecutionResponse>(KaleidoJsonOptions.Options, Cancel))!.ProcessId);
    }

    private sealed class VersionRouter(HttpMessageHandler first, HttpMessageHandler second) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker v1 = new(first);
        private readonly HttpMessageInvoker v2 = new(second);
        public List<string> RoutedTo { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath;
            if (path?.StartsWith("/returns/1/", StringComparison.Ordinal) == true)
            {
                RoutedTo.Add("/returns/1");
                return v1.SendAsync(request, cancellationToken);
            }

            if (path?.StartsWith("/returns/2/", StringComparison.Ordinal) == true)
            {
                RoutedTo.Add("/returns/2");
                return v2.SendAsync(request, cancellationToken);
            }

            throw new HttpRequestException($"No gateway route for '{path}'.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                v1.Dispose();
                v2.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private async Task<IHost> StartAsync(string serviceName, string modelNamespace, string? serviceVersion, TestServer? targetServer)
    {
        _registry ??= await TestControlRegistry.StartAsync();
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var settings = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Kaleido:BaseUrl"] = "http://registry.test/" };

                    services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new()
                    {
                        ServiceName = serviceName,
                        ServiceVersion = serviceVersion ?? Version1,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(SharedDatabaseTests).Assembly],
                        TypeFilter = type => string.Equals(type.Namespace, modelNamespace, StringComparison.Ordinal),
                    })
                        .AddSqliteContextStore(ConnectionString)
                        .AddSqliteOutboxStore(ConnectionString)
                        .AddSqliteIdempotencyStore(ConnectionString)
                        .AddHttp()
                        .AddHttpClients()
                        .AddControlRegistryClient();
                    services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => _registry!.CreateHandler());

                    if (targetServer is not null)
                    {
                        services.AddHttpClient(KaleidoClientTransport.ClientName).ConfigurePrimaryHttpMessageHandler(targetServer.CreateHandler);
                    }
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(host);
        await _registry.WaitForRegistrationAsync(host);

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);

        return host;
    }

    private static async Task<HttpResponseMessage> PostAsync(IHost host, string serviceName, string stepName, object processStep, Guid? processId, string? idempotencyKey = null)
    {
        var version = host.Services.GetRequiredService<KaleidoServiceOptions>().ServiceVersion ?? throw new InvalidOperationException("The host has no service version.");
        using var request = new HttpRequestMessage(HttpMethod.Post, ProcessContractUrls.ExecuteStep(serviceName, version, stepName.ToLowerInvariant()))
        {
            Content = JsonContent.Create(new { processStep }, options: KaleidoJsonOptions.Options),
        };

        if (processId is { } id)
        {
            request.Headers.Add(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, idempotencyKey);
        }

        return await host.GetTestClient().SendAsync(request, Cancel);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, Cancel)
                ?? throw new InvalidOperationException("The response had no body.");
        }
    }

    private static async Task<ProcessStateResponse> StateAsync(IHost host, string serviceName, Guid processId)
    {
        var version = host.Services.GetRequiredService<KaleidoServiceOptions>().ServiceVersion ?? throw new InvalidOperationException("The host has no service version.");
        return await host.GetTestClient().GetFromJsonAsync<ProcessStateResponse>(ProcessContractUrls.ProcessState(serviceName, version, processId), KaleidoJsonOptions.Options, Cancel)
            ?? throw new InvalidOperationException("No state.");
    }

    private Task<List<string>> ProcessorsOfAsync(Guid processId) => ColumnAsync("ProcessorName", processId);

    private Task<List<string>> VersionsOfAsync(Guid processId) => ColumnAsync("ProcessorVersion", processId);

    // The process rows stored under one process id, in the shared database.
    private async Task<List<string>> ColumnAsync(string column, Guid processId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM ProcessContexts WHERE upper(ProcessId) = upper($id) ORDER BY {column}";
        command.Parameters.AddWithValue("$id", processId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        var values = new List<string>();

        while (await reader.ReadAsync(Cancel))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
