using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Registration;

// At host start, warns when the gateway URL is neither https nor loopback: a call made for a caller
// carries a credential (forwarded or exchanged, #574) and must not silently use clear text (#434).
// A private network or a TLS-terminating sidecar can make such a URL deliberate.
internal sealed partial class KaleidoClientTransportCheck(
    KaleidoClientOptions options,
    ILogger<KaleidoClientTransportCheck> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !uri.IsLoopback)
        {
            LogCredentialOverPlainConnection(logger, KaleidoClientTransport.ClientName, uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido client '{ClientName}' calls {BaseUrl} without HTTPS: a caller's credential would be sent in clear text. Use an https Kaleido:BaseUrl for the gateway.")]
    private static partial void LogCredentialOverPlainConnection(ILogger logger, string clientName, string baseUrl);
}
