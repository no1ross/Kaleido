using Kaleido.Http;

namespace Kaleido.Observability.OpenTelemetry;

public static class HttpOpenTelemetryExtensions
{
    public static MeterProviderBuilder AddKaleidoHttpInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            KaleidoHttpTelemetry.MeterName);
    }
}
