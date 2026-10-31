using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Mcp.Tools;

// #452: the guided-lookup state is protected with Data Protection, so every MCP instance behind a load
// balancer needs the same key ring. Kaleido cannot see the other instances; what it can see is that
// the host configured no key storage of its own, so Data Protection uses its default location, which
// is this machine's or this container's (Azure App Service shares it between instances). Outside
// Development that is reported once at startup; a form answered on an instance without the key is
// reported when it happens (McpObservability.StateRefused).
internal sealed partial class McpKeyRingCheck(
    IHostEnvironment environment,
    IOptions<KeyManagementOptions> keyManagement,
    ILogger<McpKeyRingCheck> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && keyManagement.Value.XmlRepository is null)
        {
            LogNoSharedKeyRing(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: no Data Protection key storage is configured, so the guided-lookup forms are protected with keys kept where Data Protection keeps them by default (this machine or container). If the MCP adapter runs on several instances, a form answered on another instance is refused: configure AddDataProtection().SetApplicationName(...).PersistKeysTo...() the same on every instance (docs/guide/deployment.md). A single instance can ignore this.")]
    private static partial void LogNoSharedKeyRing(ILogger logger);
}
