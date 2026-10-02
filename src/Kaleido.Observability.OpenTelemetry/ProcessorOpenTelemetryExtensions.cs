using Kaleido.Processor.Observability;

namespace Kaleido.Observability.OpenTelemetry;

public static class ProcessorOpenTelemetryExtensions
{
    public static TracerProviderBuilder AddKaleidoProcessorInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            ProcessorTelemetry.ActivitySourceName);
    }

    public static MeterProviderBuilder AddKaleidoProcessorInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            ProcessorTelemetry.MeterName);
    }
}
