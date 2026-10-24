using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Http.Client;

internal static class KaleidoClientFailureExtensions
{
    // The one failure mapping of the Kaleido clients (#444): any non-success answer becomes a
    // KaleidoHttpClientException. A Kaleido error body ({"errors":[...]}) is read for every status, so
    // the called service's codes survive a 403, 404 or 422 too; a body that is not one (HTML from a
    // proxy, plain text, empty) never escapes as a JsonException.
    // 409 -> httpclient_conflict, 400 with errors -> httpclient_validation_failed, anything else ->
    // httpclient_request_failed (with the service's errors when it sent any).
    internal static async Task<KaleidoHttpClientException> ToClientFailureAsync(
        this HttpResponseMessage response,
        string description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var errors = await ReadErrorsAsync(response, cancellationToken);

        if (errors.Count == 0)
        {
            return new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                string.Create(CultureInfo.InvariantCulture, $"{description} with status code {(int)response.StatusCode} ({response.StatusCode})."),
                response.StatusCode);
        }

        var code = response.StatusCode switch
        {
            HttpStatusCode.Conflict => HttpClientErrorCodes.Conflict,
            HttpStatusCode.BadRequest => HttpClientErrorCodes.ValidationFailed,
            _ => HttpClientErrorCodes.RequestFailed,
        };

        return new KaleidoHttpClientException(
            code,
            $"{description}: {string.Join(' ', errors.Select(e => e.Message))}",
            response.StatusCode,
            errors);
    }

    private static async Task<IReadOnlyList<KaleidoError>> ReadErrorsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, cancellationToken);

            return body?.Errors ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            // Not a Kaleido error body: the status alone describes the failure.
            return [];
        }
    }
}
