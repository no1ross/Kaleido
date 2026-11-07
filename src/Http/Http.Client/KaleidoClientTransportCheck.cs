using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

// At host start, warns once per configured client whose base URL is neither https nor loopback: a call made
// for a caller carries a credential (the caller's token forwarded by default, or an exchanged one, #574), so
// that client would send it over the network in clear text (#434, ADR 0027). A warning, not a startup failure:
// a private network or a TLS-terminating sidecar can make such a URL deliberate.
internal sealed partial class KaleidoClientTransportCheck(
    KaleidoClientOptions options,
    ILogger<KaleidoClientTransportCheck> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in options.Clients)
        {
            var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl) ? entry.BaseUrl : options.BaseUrl;

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !uri.IsLoopback)
            {
                LogCredentialOverPlainConnection(logger, name, uri.GetLeftPart(UriPartial.Authority));
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido client '{ClientName}' calls {BaseUrl} without HTTPS: a caller's credential would be sent in clear text. Use an https BaseUrl (Kaleido:Clients:{ClientName}:BaseUrl).")]
    private static partial void LogCredentialOverPlainConnection(ILogger logger, string clientName, string baseUrl);
}
