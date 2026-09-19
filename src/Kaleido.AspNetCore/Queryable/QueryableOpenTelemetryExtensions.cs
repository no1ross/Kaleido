using Kaleido.Queryable.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableOpenTelemetryExtensions
{
    public static TracerProviderBuilder AddKaleidoQueryableInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            QueryableTelemetry.ActivitySourceName);
    }

    public static MeterProviderBuilder AddKaleidoQueryableInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            QueryableTelemetry.MeterName);
    }
}
