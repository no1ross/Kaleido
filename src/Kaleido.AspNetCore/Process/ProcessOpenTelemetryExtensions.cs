using Kaleido.Process.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Process.AspNetCore;

public static class ProcessOpenTelemetryExtensions
{
    public static TracerProviderBuilder AddKaleidoProcessInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            ProcessTelemetry.ActivitySourceName);
    }

    public static MeterProviderBuilder AddKaleidoProcessInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            ProcessTelemetry.MeterName);
    }
}
