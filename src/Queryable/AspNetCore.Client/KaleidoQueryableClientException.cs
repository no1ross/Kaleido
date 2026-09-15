using System.Net;

namespace Kaleido.Queryable.AspNetCore.Client;

public sealed class KaleidoQueryableClientException : Exception
{
    public KaleidoQueryableClientException(
        string message,
        HttpStatusCode statusCode,
        IReadOnlyList<KaleidoError>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public KaleidoQueryableClientException(
        string message,
        HttpStatusCode statusCode,
        Exception innerException,
        IReadOnlyList<KaleidoError>? errors = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyList<KaleidoError> Errors { get; }
}
