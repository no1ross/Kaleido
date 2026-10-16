using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Http.Mcp;
using Kaleido.Processor.Observability;
using Kaleido.Provider.SQLite;
using Kaleido.Queryable.Observability;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

[Collection(TelemetryProbe.Collection)]
public sealed class KaleidoObservabilityOpenTelemetryExtensionsTests
    : SutFixture
{
    // A source/meter the host instrumentation listens to (ASP.NET Core traces, runtime metrics).
    private const string AspNetCoreSource = "Microsoft.AspNetCore";
    private const string RuntimeMeter = "OpenTelemetry.Instrumentation.Runtime";

    private static ServiceProvider BuildHost(
        Action<KaleidoOpenTelemetryOptions>? configure = null,
        IDictionary<string, string?>? configuration = null)
    {
        // No collector listens in tests: a short OTLP timeout keeps the flush on dispose quick.
        var settings = new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_TIMEOUT"] = "100",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        };

        foreach (var (key, value) in configuration ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services
            .AddKaleido(
                config,
                o =>
                {
                    o.ServiceName = "telemetry-test";
                    o.Assemblies = [typeof(KaleidoObservabilityOpenTelemetryExtensionsTests).Assembly];
                })
            .AddOpenTelemetry(configure);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // The providers start when they are resolved (normally by the host).
        _ = provider.GetService<TracerProvider>();
        _ = provider.GetService<MeterProvider>();

        return provider;
    }

    private static bool ExportsLogs(IServiceProvider provider) =>
        provider.GetServices<ILoggerProvider>().Any(x => x.GetType().Name == "OpenTelemetryLoggerProvider");

    [Fact]
    public void AddKaleidoInstrumentation_ListensToEveryKaleidoActivitySource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddKaleidoInstrumentation().Build();

        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
        Assert.True(TelemetryProbe.IsTraced(QueryableTelemetry.ActivitySourceName));
        Assert.True(TelemetryProbe.IsTraced(SqliteTelemetry.ActivitySourceName));
        Assert.True(TelemetryProbe.IsTraced(McpTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddKaleidoInstrumentation_ListensToEveryKaleidoMeter()
    {
        using var provider = TelemetryProbe.BuildMeterProvider(b => b.AddKaleidoInstrumentation());

        Assert.True(TelemetryProbe.IsMetered(ProcessorTelemetry.MeterName));
        Assert.True(TelemetryProbe.IsMetered(QueryableTelemetry.MeterName));
        Assert.True(TelemetryProbe.IsMetered(KaleidoEventTelemetry.MeterName));
        Assert.True(TelemetryProbe.IsMetered(SqliteTelemetry.MeterName));
        Assert.True(TelemetryProbe.IsMetered(McpTelemetry.MeterName));
    }

    [Fact]
    public void AddOpenTelemetry_ByDefault_ExportsLogsTracesAndMetricsWithTheHostInstrumentation()
    {
        using var provider = BuildHost();

        Assert.True(ExportsLogs(provider));
        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
        Assert.True(TelemetryProbe.IsTraced(AspNetCoreSource));
        Assert.True(TelemetryProbe.IsMetered(RuntimeMeter));
    }

    [Fact]
    public void AddOpenTelemetry_WithoutLogs_StillExportsTracesAndMetrics()
    {
        using var provider = BuildHost(o => o.Logs = false);

        Assert.False(ExportsLogs(provider));
        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
        Assert.True(TelemetryProbe.IsMetered(ProcessorTelemetry.MeterName));
    }

    [Fact]
    public void AddOpenTelemetry_WithoutTracesAndMetrics_OnlyExportsLogs()
    {
        using var provider = BuildHost(o =>
        {
            o.Traces = false;
            o.Metrics = false;
        });

        Assert.True(ExportsLogs(provider));
        Assert.False(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
        Assert.False(TelemetryProbe.IsMetered(ProcessorTelemetry.MeterName));
    }

    [Fact]
    public void AddOpenTelemetry_WithoutHostInstrumentation_KeepsOnlyKaleidosOwnTelemetry()
    {
        using var provider = BuildHost(o => o.HostInstrumentation = false);

        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
        Assert.False(TelemetryProbe.IsTraced(AspNetCoreSource));
        Assert.False(TelemetryProbe.IsMetered(RuntimeMeter));
    }

    [Fact]
    public void AddOpenTelemetry_WithEverySignalOff_RegistersNothing()
    {
        using var provider = BuildHost(o =>
        {
            o.Logs = false;
            o.Traces = false;
            o.Metrics = false;
        });

        Assert.False(ExportsLogs(provider));
        Assert.Null(provider.GetService<TracerProvider>());
        Assert.Null(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void AddOpenTelemetry_ReadsTheOptionsFromConfiguration()
    {
        using var provider = BuildHost(configuration: new Dictionary<string, string?>
        {
            ["Kaleido:OpenTelemetry:Logs"] = "false",
        });

        Assert.False(ExportsLogs(provider));
        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddOpenTelemetry_TheConfigureDelegateWinsOverConfiguration()
    {
        using var provider = BuildHost(
            o => o.Logs = true,
            new Dictionary<string, string?> { ["Kaleido:OpenTelemetry:Logs"] = "false" });

        Assert.True(ExportsLogs(provider));
    }

    private static Dictionary<string, object> ResourceOf(IServiceProvider provider) =>
        provider.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(x => x.Key, x => x.Value);

    [Fact]
    public void AddOpenTelemetry_NamesTheResourceAfterTheKaleidoService_WithTheConfiguredVersionAndAttributes()
    {
        using var provider = BuildHost(configuration: new Dictionary<string, string?>
        {
            ["Kaleido:OpenTelemetry:ServiceVersion"] = "1.0.0",
            ["Kaleido:OpenTelemetry:ResourceAttributes"] = "deployment.environment=Development",
        });

        var resource = ResourceOf(provider);
        Assert.Equal("telemetry-test", resource["service.name"]);
        Assert.Equal("1.0.0", resource["service.version"]);
        Assert.Equal("Development", resource["deployment.environment"]);
        Assert.True(Guid.TryParse(resource["service.instance.id"].ToString(), out _));
    }

    [Fact]
    public void AddOpenTelemetry_TheStandardServiceNameKey_WinsOverTheKaleidoServiceName()
    {
        using var provider = BuildHost(configuration: new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "from-the-deployment",
        });

        Assert.Equal("from-the-deployment", ResourceOf(provider)["service.name"]);
    }

    [Fact]
    public void AddOpenTelemetry_WithAnUnknownProtocol_FailsAtStartup()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            BuildHost(configuration: new Dictionary<string, string?> { ["Kaleido:OpenTelemetry:Protocol"] = "http/json" }));

        Assert.Equal(OpenTelemetryErrorCodes.InvalidOptions, exception.Code);
    }

    [Fact]
    public void AddOpenTelemetry_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOpenTelemetry());
    }
}
