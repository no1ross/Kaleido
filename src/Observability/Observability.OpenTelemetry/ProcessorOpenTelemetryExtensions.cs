using Kaleido.Processor.Observability;

namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// Registers only the Process runtime's telemetry with an OpenTelemetry pipeline the consumer
/// manages. <c>AddKaleidoInstrumentation()</c> registers it together with the rest of Kaleido's.
/// </summary>
public static class ProcessorOpenTelemetryExtensions
{
    /// <summary>
    /// Registers the Process runtime's <c>ActivitySource</c>
    /// (<see cref="ProcessorTelemetry.ActivitySourceName"/>).
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddKaleidoProcessorInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            ProcessorTelemetry.ActivitySourceName);
    }

    /// <summary>
    /// Registers the Process runtime's <c>Meter</c> (<see cref="ProcessorTelemetry.MeterName"/>),
    /// without the tuned histogram bucket boundaries that <c>AddKaleidoInstrumentation()</c> adds.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MeterProviderBuilder AddKaleidoProcessorInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            ProcessorTelemetry.MeterName);
    }
}
