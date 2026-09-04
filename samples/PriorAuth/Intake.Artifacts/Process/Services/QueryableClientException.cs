using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Samples.PriorAuth.Intake.Process.Models;
using System.Net;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

public sealed class QueryableClientException : Exception
{
    public QueryableClientException(
        string message,
        HttpStatusCode statusCode,
        IReadOnlyList<QueryError>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public QueryableClientException(
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
