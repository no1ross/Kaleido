using System.Net;

namespace Kaleido.Http.Client.Process;

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
