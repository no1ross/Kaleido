using Kaleido.Queryable.AspNetCore.Contracts;
using System.Net;

namespace Kaleido.Queryable.AspNetCore.Client;

public sealed class KaleidoQueryableClientException : Exception
{
    public KaleidoQueryableClientException(
        string message,
        HttpStatusCode statusCode,
        IReadOnlyList<QueryError>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public KaleidoQueryableClientException(
        string message,
        HttpStatusCode statusCode,
        Exception innerException,
        IReadOnlyList<QueryError>? errors = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyList<QueryError> Errors { get; }
}
