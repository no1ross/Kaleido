using System.Net;

namespace Kaleido.Process.AspNetCore.Client;

public sealed class KaleidoProcessClientException : Exception
{
    public KaleidoProcessClientException(
        string message,
        HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public KaleidoProcessClientException(
        string message,
        HttpStatusCode statusCode,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
