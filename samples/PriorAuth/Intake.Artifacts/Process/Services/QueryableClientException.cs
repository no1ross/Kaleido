using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using System.Net;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class QueryableClientException : Exception
{
    public QueryableClientException(
        string message,
        HttpStatusCode statusCode,
        IReadOnlyList<QueryableError>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public QueryableClientException(
        string message,
        HttpStatusCode statusCode,
        Exception innerException,
        IReadOnlyList<QueryableError>? errors = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
    }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyList<QueryableError> Errors { get; }
}
