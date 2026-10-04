using System.Text.Json;

namespace Kaleido.Http.FunctionalTests.Processor.Infrastructure;

internal static class ProcessHttpJson
{
    public static Task<T?> ReadAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(KaleidoJsonOptions.Options, cancellationToken);
}
