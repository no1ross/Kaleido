using Kaleido.Queryable.Observability;

namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// Registers only the Queryable runtime's telemetry with an OpenTelemetry pipeline the consumer
/// manages. <c>AddKaleidoInstrumentation()</c> registers it together with the rest of Kaleido's.
/// </summary>
public static class QueryableOpenTelemetryExtensions
{
    /// <summary>
    /// Registers the Queryable runtime's <c>ActivitySource</c>
    /// (<see cref="QueryableTelemetry.ActivitySourceName"/>).
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddKaleidoQueryableInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            QueryableTelemetry.ActivitySourceName);
    }

    /// <summary>
    /// Registers the Queryable runtime's <c>Meter</c> (<see cref="QueryableTelemetry.MeterName"/>),
    /// without the tuned histogram bucket boundaries that <c>AddKaleidoInstrumentation()</c> adds.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MeterProviderBuilder AddKaleidoQueryableInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            QueryableTelemetry.MeterName);
    }
}
