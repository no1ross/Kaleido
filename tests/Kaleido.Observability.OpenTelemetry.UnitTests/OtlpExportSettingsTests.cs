using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

public sealed class OtlpExportSettingsTests : SutFixture
{
    private static readonly Uri Collector = new("http://collector.test:4318");

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();

    private static OtlpExporterOptions Applied(KaleidoOpenTelemetryOptions options, OtlpSignal signal, IConfiguration? configuration = null)
    {
        var exporter = new OtlpExporterOptions();
        options.ApplyTo(exporter, configuration ?? Configuration(), signal);
        return exporter;
    }

    [Fact]
    public void ApplyTo_WithAGrpcEndpoint_UsesItAsIs()
    {
        var exporter = Applied(new KaleidoOpenTelemetryOptions { Endpoint = Collector, Protocol = "grpc" }, OtlpSignal.Traces);

        Assert.Equal(OtlpExportProtocol.Grpc, exporter.Protocol);
        Assert.Equal(Collector, exporter.Endpoint);
    }

    [Theory]
    [InlineData("Traces", "http://collector.test:4318/v1/traces")]
    [InlineData("Metrics", "http://collector.test:4318/v1/metrics")]
    [InlineData("Logs", "http://collector.test:4318/v1/logs")]
    public void ApplyTo_WithHttpProtobuf_AppendsEachSignalsPath(string signal, string expected)
    {
        var exporter = Applied(new KaleidoOpenTelemetryOptions { Endpoint = Collector, Protocol = "http/protobuf" }, Enum.Parse<OtlpSignal>(signal));

        Assert.Equal(OtlpExportProtocol.HttpProtobuf, exporter.Protocol);
        Assert.Equal(new Uri(expected), exporter.Endpoint);
    }

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")]
    public void ApplyTo_LeavesTheEndpoint_WhenAStandardKeySetsIt(string key)
    {
        var untouched = new OtlpExporterOptions().Endpoint;

        var exporter = Applied(
            new KaleidoOpenTelemetryOptions { Endpoint = Collector },
            OtlpSignal.Traces,
            Configuration((key, "http://from-the-environment:4317")));

        Assert.Equal(untouched, exporter.Endpoint);
    }

    [Fact]
    public void ApplyTo_LeavesTheProtocolAndHeaders_WhenStandardKeysSetThem()
    {
        var defaults = new OtlpExporterOptions();

        var exporter = Applied(
            new KaleidoOpenTelemetryOptions { Protocol = "http/protobuf", Headers = "api-key=configured" },
            OtlpSignal.Metrics,
            Configuration(("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc"), ("OTEL_EXPORTER_OTLP_HEADERS", "api-key=environment")));

        Assert.Equal(defaults.Protocol, exporter.Protocol);
        Assert.Equal(defaults.Headers, exporter.Headers);
    }

    [Fact]
    public void ApplyTo_SetsTheHeaders()
    {
        var exporter = Applied(new KaleidoOpenTelemetryOptions { Headers = "api-key=secret" }, OtlpSignal.Logs);

        Assert.Equal("api-key=secret", exporter.Headers);
    }

    [Fact]
    public void ApplyTo_WithNothingConfigured_ChangesNothing()
    {
        var defaults = new OtlpExporterOptions();

        var exporter = Applied(new KaleidoOpenTelemetryOptions(), OtlpSignal.Traces);

        Assert.Equal(defaults.Endpoint, exporter.Endpoint);
        Assert.Equal(defaults.Protocol, exporter.Protocol);
        Assert.Equal(defaults.Headers, exporter.Headers);
    }

    [Theory]
    [InlineData("Protocol", "http/json")]
    [InlineData("Headers", "no-equals-sign")]
    [InlineData("ResourceAttributes", "=value-without-key")]
    public void Validate_RejectsSettingsTheExporterCannotUse(string setting, string value)
    {
        var options = setting switch
        {
            "Protocol" => new KaleidoOpenTelemetryOptions { Protocol = value },
            "Headers" => new KaleidoOpenTelemetryOptions { Headers = value },
            _ => new KaleidoOpenTelemetryOptions { ResourceAttributes = value },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => options.Validate());

        Assert.Equal(OpenTelemetryErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsTheDocumentedForms()
    {
        var options = new KaleidoOpenTelemetryOptions
        {
            Protocol = " HTTP/Protobuf ",
            Headers = "a=1, b=2",
            ResourceAttributes = "deployment.environment=Development,service.namespace=priorauth",
        };

        options.Validate();
    }

    [Fact]
    public void ConfigureResource_DescribesTheService_WithItsKaleidoNameAndTheConfiguredVersionAndAttributes()
    {
        var instanceId = Guid.NewGuid();
        var resource = ResourceBuilder.CreateEmpty();

        new KaleidoOpenTelemetryOptions
        {
            ServiceVersion = "1.2.3",
            ResourceAttributes = "deployment.environment=Development",
        }.ConfigureResource(resource, new KaleidoServiceOptions { ServiceName = "intake", InstanceId = instanceId });

        var attributes = resource.Build().Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("intake", attributes["service.name"]);
        Assert.Equal("1.2.3", attributes["service.version"]);
        Assert.Equal(instanceId.ToString(), attributes["service.instance.id"]);
        Assert.Equal("Development", attributes["deployment.environment"]);
    }

    [Fact]
    public void ConfigureResource_WithNothingConfigured_HasOnlyTheServiceIdentity()
    {
        var resource = ResourceBuilder.CreateEmpty();

        new KaleidoOpenTelemetryOptions().ConfigureResource(resource, new KaleidoServiceOptions { ServiceName = "intake" });

        var attributes = resource.Build().Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("intake", attributes["service.name"]);
        Assert.False(attributes.ContainsKey("service.version"));
    }
}
