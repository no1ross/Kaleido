using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class McpKeyRingCheckTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<ILogger<McpKeyRingCheck>> logger = new();

    public McpKeyRingCheckTests() =>
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private McpKeyRingCheck CreateSut(string environment, IXmlRepository? repository)
    {
        var host = new Mock<IHostEnvironment>();
        host.Setup(x => x.EnvironmentName).Returns(environment);

        return new McpKeyRingCheck(host.Object, Options.Create(new KeyManagementOptions { XmlRepository = repository }), logger.Object);
    }

    private IEnumerable<(LogLevel Level, string Message)> Logs() =>
        logger.Invocations
            .Where(x => x.Method.Name == nameof(ILogger.Log))
            .Select(x => ((LogLevel)x.Arguments[0], ((Delegate)x.Arguments[4]).DynamicInvoke(x.Arguments[2], x.Arguments[3]) as string ?? string.Empty));

    // #452: outside Development, a host without key storage of its own is told how to share the key ring.
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task StartAsync_WithoutConfiguredKeyStorage_OutsideDevelopment_Warns(string environment)
    {
        await CreateSut(environment, repository: null).StartAsync(TestContext.Current.CancellationToken);

        var log = Assert.Single(Logs());
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("PersistKeysTo", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WithConfiguredKeyStorage_SaysNothing()
    {
        await CreateSut("Production", Mock.Of<IXmlRepository>()).StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Logs());
    }

    [Fact]
    public async Task StartAsync_InDevelopment_SaysNothing()
    {
        await CreateSut("Development", repository: null).StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Logs());
    }

    [Fact]
    public async Task StopAsync_DoesNothing()
    {
        await CreateSut("Production", repository: null).StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(Logs());
    }
}
