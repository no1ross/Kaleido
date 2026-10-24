using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

// At host start, when an IKaleidoCallCredentialProvider decides the calls' Authorization (a caller's
// token forwarded or exchanged), warns once per configured client whose base URL is neither https nor
// loopback: that client would send a caller's credential over the network in clear text (#434,
// ADR 0027). A warning, not a startup failure: a private network or a TLS-terminating sidecar can make
// such a URL deliberate. Checked at start, not at registration: the provider can be registered after
// AddHttpClients(), and registration has no logger.
internal sealed partial class KaleidoClientTransportCheck(
    KaleidoClientOptions options,
    IEnumerable<IKaleidoCallCredentialProvider> providers,
    ILogger<KaleidoClientTransportCheck> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!providers.Any())
        {
            return Task.CompletedTask;
        }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido client '{ClientName}' calls {BaseUrl} without HTTPS while call credentials are configured: a caller's credential would be sent in clear text. Use an https BaseUrl (Kaleido:Clients:{ClientName}:BaseUrl).")]
    private static partial void LogCredentialOverPlainConnection(ILogger logger, string clientName, string baseUrl);
}
