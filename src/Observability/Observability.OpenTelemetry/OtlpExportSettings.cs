using Kaleido.Exceptions;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;

namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// Applies <see cref="KaleidoOpenTelemetryOptions"/> to an OTLP exporter and a resource. The standard
/// <c>OTEL_*</c> keys (environment variables or configuration keys) win over the Kaleido settings; the
/// SDK reads them itself, so a setting is only applied when its standard key is absent.
/// </summary>
internal static class OtlpExportSettings
{
    private const string Grpc = "grpc";
    private const string HttpProtobuf = "http/protobuf";

    /// <summary>Rejects settings the exporter cannot use, at startup.</summary>
    /// <exception cref="KaleidoConfigurationException">The protocol is not <c>grpc</c> or <c>http/protobuf</c>, or a key/value list is malformed.</exception>
    public static void Validate(this KaleidoOpenTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ = ParseProtocol(options.Protocol);
        _ = ParsePairs(options.Headers, nameof(options.Headers));
        _ = ParsePairs(options.ResourceAttributes, nameof(options.ResourceAttributes));
    }

    public static void ApplyTo(
        this KaleidoOpenTelemetryOptions options,
        OtlpExporterOptions exporter,
        IConfiguration configuration,
        OtlpSignal signal)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var name = signal.ToString().ToUpperInvariant();

        if (ParseProtocol(options.Protocol) is { } protocol && !IsSet(configuration, "PROTOCOL", name))
        {
            exporter.Protocol = protocol;
        }

        if (options.Endpoint is { } endpoint && !IsSet(configuration, "ENDPOINT", name))
        {
            // The SDK appends the signal path only to an endpoint it read from OTEL_*.
            exporter.Endpoint = exporter.Protocol == OtlpExportProtocol.HttpProtobuf
                ? new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/v1/" + signal.ToString().ToLowerInvariant())
                : endpoint;
        }

        if (!string.IsNullOrWhiteSpace(options.Headers) && !IsSet(configuration, "HEADERS", name))
        {
            exporter.Headers = options.Headers;
        }
    }

    /// <summary>The resource: Kaleido's service identity (its <c>ServiceName</c> and <c>InstanceId</c>), then the configured attributes; <c>OTEL_SERVICE_NAME</c> and <c>OTEL_RESOURCE_ATTRIBUTES</c> are applied last and win.</summary>
    public static void ConfigureResource(
        this KaleidoOpenTelemetryOptions options,
        ResourceBuilder resource,
        KaleidoServiceOptions service)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(service);

        resource.AddService(
            serviceName: service.ServiceName,
            serviceVersion: options.ServiceVersion,
            autoGenerateServiceInstanceId: false,
            serviceInstanceId: service.InstanceId.ToString());

        var attributes = ParsePairs(options.ResourceAttributes, nameof(options.ResourceAttributes));

        if (attributes.Count > 0)
        {
            resource.AddAttributes(attributes.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)));
        }

        resource.AddEnvironmentVariableDetector();
    }

    private static OtlpExportProtocol? ParseProtocol(string? protocol) =>
        string.IsNullOrWhiteSpace(protocol)
            ? null
            : protocol.Trim().ToLowerInvariant() switch
            {
                Grpc => OtlpExportProtocol.Grpc,
                HttpProtobuf => OtlpExportProtocol.HttpProtobuf,
                _ => throw new KaleidoConfigurationException(
                    OpenTelemetryErrorCodes.InvalidOptions,
                    $"Kaleido:OpenTelemetry:Protocol '{protocol}' is not supported; use '{Grpc}' or '{HttpProtobuf}'."),
            };

    // "key1=value1,key2=value2", as OTEL_RESOURCE_ATTRIBUTES and OTEL_EXPORTER_OTLP_HEADERS.
    private static List<KeyValuePair<string, string>> ParsePairs(string? value, string setting)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var pairs = new List<KeyValuePair<string, string>>();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                throw new KaleidoConfigurationException(
                    OpenTelemetryErrorCodes.InvalidOptions,
                    $"Kaleido:OpenTelemetry:{setting} must be 'key=value' pairs separated by commas; '{part}' is not.");
            }

            pairs.Add(new(part[..separator].Trim(), part[(separator + 1)..].Trim()));
        }

        return pairs;
    }

    // OTEL_EXPORTER_OTLP_{SETTING} or OTEL_EXPORTER_OTLP_{SIGNAL}_{SETTING}, as configuration or environment.
    private static bool IsSet(IConfiguration configuration, string setting, string signal) =>
        IsSet(configuration, $"OTEL_EXPORTER_OTLP_{setting}")
        || IsSet(configuration, $"OTEL_EXPORTER_OTLP_{signal}_{setting}");

    private static bool IsSet(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key] ?? Environment.GetEnvironmentVariable(key));
}
