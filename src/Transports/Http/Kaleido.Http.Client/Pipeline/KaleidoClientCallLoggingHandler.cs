using System.Net;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Pipeline;

internal sealed partial class KaleidoClientCallLoggingHandler(ILogger<KaleidoClientCallLoggingHandler> logger) : DelegatingHandler
{
    internal static readonly HttpRequestOptionsKey<string> TargetService = new("Kaleido.TargetService");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var service = request.Options.TryGetValue(TargetService, out var target) ? target.Sanitize() ?? "unknown" : "unknown";
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        LogSending(logger, request.Method, path, service);
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified)
        {
            LogRemoteNonSuccess(logger, service, (int)response.StatusCode, response.StatusCode, request.Method, path);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {Method} {Path} through Kaleido client to service '{ServiceName}'.")]
    private static partial void LogSending(ILogger logger, HttpMethod method, string path, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote Kaleido service '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Path}.")]
    private static partial void LogRemoteNonSuccess(ILogger logger, string serviceName, int statusCode, HttpStatusCode statusName, HttpMethod method, string path);
}
